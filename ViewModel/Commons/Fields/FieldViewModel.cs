using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using BlazorServerApp.Model.ViewModels;
using BlazorServerApp.Model.Entities;
using ViewModel.Commons.Bases;

namespace ViewModel.Commons.Fields;

/// <summary>
/// Generic property wrapper with validation, lazy loading, and UI metadata.
///
/// Key Features:
/// - Lazy loading: getValue() called only when Value is first accessed
/// - Validation: FluentValidation rules with Error/Warning severity
/// - UI Metadata: Label, Hint for form fields
/// - List support: Dropdown/ComboBox data via listQuery
/// </summary>
/// <typeparam name="T">Property value type</typeparam>
public partial class FieldViewModel<T> : ObservableObject, IFieldViewModel
{
    private T? _value;
    private readonly Func<T>? _getValue;
    private readonly Action<T>? _setValue;
    private readonly Func<List<T>>? _listQuery;
    private bool _isInitialized;

    // UI Metadata
    private string? _warning;
    private string? _error;
    private string? _label;
    private string? _hint;
    private bool _readOnly;
    private string? _columnWidth;
    private bool _hiddenInColumn;
    private int _columnOrder;
    private string? _formGroupHeader;
    private int _formGroupOrder;

    // Computed field support
    private bool _isComputed;
    private string[]? _notifyOnChange;

    // List support (for ComboBox/Select components)
    private bool _valueMustBeInTheList;

    public FieldViewModel(
        object? parent = null,
        Func<T>? getValue = null,
        Action<T>? setValue = null,
        Func<List<T>>? listQuery = null)
    {
        Parent = parent;
        _getValue = getValue;
        _setValue = setValue;
        _listQuery = listQuery;
    }

    public object? Parent { get; }

    /// <summary>
    /// The wrapped property value.
    /// </summary>
    public T? Value
    {
        get
        {
            // For computed fields, always recalculate (no cache)
            if (IsComputed && _getValue != null)
            {
                return _getValue();
            }

            // For normal fields, lazy loading with cache
            if (!_isInitialized && _getValue != null)
            {
                _value = _getValue();
                _isInitialized = true;
            }
            return _value;
        }
        set
        {
            // Block modifications on computed fields
            if (IsComputed)
            {
                return; // Silent return, ReadOnly already handles UI
            }

            if (ReadOnly) return;

            // Validate list constraint
            if (ValueMustBeInTheList && List != null && value != null && !List.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _value, value))
            {
                _isInitialized = true;

                // Write back to underlying model
                _setValue?.Invoke(value!);

                // Mark parent entity as modified if applicable
                if (Parent is IEntityViewModel<IEntity> entityVm)
                {
                    var entity = entityVm.Model;
                    if (entity != null)
                    {
                        entityVm.UnitOfWork.MarkAsModified(entity);
                    }
                }

                // Re-validate on change
                Validate();

                // Notify dependent computed fields
                if (NotifyOnChange != null && NotifyOnChange.Length > 0 && Parent is BaseViewModel parentVm)
                {
                    foreach (var propertyName in NotifyOnChange)
                    {
                        parentVm.RaisePropertyChanged(propertyName);
                    }
                }
            }
        }
    }

    /// <summary>
    /// List of possible values (for dropdowns).
    /// Always fetches fresh data from the query.
    /// </summary>
    public List<T>? List => _listQuery?.Invoke();

    // UI Metadata Properties

    public string? Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string? Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }

    /// <summary>
    /// If true, the field cannot be modified in the UI.
    /// Automatically returns true for computed fields (IsComputed = true).
    /// </summary>
    public bool ReadOnly
    {
        get => _readOnly || IsComputed; // Computed fields are always read-only
        set => SetProperty(ref _readOnly, value);
    }

    /// <summary>
    /// Validation error (blocks save).
    /// </summary>
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>
    /// Validation warning (does not block save).
    /// </summary>
    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    /// <summary>
    /// Column width for table display (CSS value: "150px", "20%", etc.). Null = auto.
    /// </summary>
    public string? ColumnWidth
    {
        get => _columnWidth;
        set => SetProperty(ref _columnWidth, value);
    }

    /// <summary>
    /// If true, this field is not displayed in table columns.
    /// </summary>
    public bool HiddenInColumn
    {
        get => _hiddenInColumn;
        set => SetProperty(ref _hiddenInColumn, value);
    }

    /// <summary>
    /// Display order in table columns. Lower values appear first.
    /// Default is 0.
    /// </summary>
    public int ColumnOrder
    {
        get => _columnOrder;
        set => SetProperty(ref _columnOrder, value);
    }

    /// <summary>
    /// Group header for form display. Fields with the same header are grouped together.
    /// Null or empty means no group (displayed at the top).
    /// </summary>
    public string? FormGroupHeader
    {
        get => _formGroupHeader;
        set => SetProperty(ref _formGroupHeader, value);
    }

    /// <summary>
    /// Display order of the group in the form. Lower values appear first.
    /// Default is 0.
    /// </summary>
    public int FormGroupOrder
    {
        get => _formGroupOrder;
        set => SetProperty(ref _formGroupOrder, value);
    }

    public bool ValueMustBeInTheList
    {
        get => _valueMustBeInTheList;
        set => SetProperty(ref _valueMustBeInTheList, value);
    }

    /// <summary>
    /// If true, getValue() is called on every Value access (no caching).
    /// Use for computed fields that depend on other properties.
    /// </summary>
    public bool IsComputed
    {
        get => _isComputed;
        set => SetProperty(ref _isComputed, value);
    }

    /// <summary>
    /// Array of property names (use nameof()) to notify when this field's value changes.
    /// Used to trigger recalculation of computed fields that depend on this field.
    /// </summary>
    public string[]? NotifyOnChange
    {
        get => _notifyOnChange;
        set => SetProperty(ref _notifyOnChange, value);
    }

    /// <summary>
    /// FluentValidation rules.
    /// Set this in ViewModel property initializer.
    /// </summary>
    public Action<IRuleBuilder<FieldViewModel<T>, T?>>? ValidationRules { get; set; }

    public bool HasSetValueFunction => _setValue != null;

    /// <summary>
    /// Executes FluentValidation and sets Error/Warning properties.
    /// Automatically called on Value change, or call manually.
    /// </summary>
    public void Validate()
    {
        Error = null;
        Warning = null;

        if (ValidationRules == null) return;

        // Build FluentValidation validator
        var validator = new InlineValidator<FieldViewModel<T>>();
        var ruleBuilder = validator.RuleFor(x => x.Value);
        ValidationRules(ruleBuilder);

        // Validate
        var result = validator.Validate(this);

        // Separate errors from warnings
        var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
        var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

        if (errors.Any())
        {
            Error = errors.First().ErrorMessage;
        }
        else if (warnings.Any())
        {
            Warning = warnings.First().ErrorMessage;
        }
    }

    public override string ToString()
    {
        return Label ?? typeof(T).Name;
    }
}
