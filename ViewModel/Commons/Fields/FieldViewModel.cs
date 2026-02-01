using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using Infrastructure.ViewModel;
using Model.Entities;

namespace ViewModel.Commons.Fields;

/// <summary>
/// Generic property wrapper with validation, lazy loading, and UI metadata.
///
/// WPF PATTERN: Unchanged - works identically in Blazor.
/// BLAZOR INTEGRATION: Binds to Value property in Razor components.
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

    // List support (for ComboBox/Select components)
    private bool _valueMustBeInTheList;
    private List<T>? _cachedList;

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
    /// BLAZOR BINDING: @bind="ViewModel.PropertyName.Value"
    /// </summary>
    public T? Value
    {
        get
        {
            // Lazy loading: Load on first access
            if (!_isInitialized && _getValue != null)
            {
                _value = _getValue();
                _isInitialized = true;
            }
            return _value;
        }
        set
        {
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
                        entityVm.Repository.MarkAsModified(entity);
                    }
                }

                // Re-validate on change
                Validate();
            }
        }
    }

    /// <summary>
    /// List of possible values (for dropdowns).
    /// BLAZOR USAGE: <select> or <InputSelect> binds to this.
    /// </summary>
    public List<T>? List
    {
        get
        {
            if (_cachedList == null && _listQuery != null)
            {
                _cachedList = _listQuery();
            }
            return _cachedList;
        }
    }

    /// <summary>
    /// Refreshes the list from the query.
    /// BLAZOR NOTE: Call this when underlying data changes (e.g., after SaveAll).
    /// </summary>
    public void RefreshList()
    {
        if (_listQuery != null)
        {
            _cachedList = _listQuery();
            OnPropertyChanged(nameof(List));
        }
    }

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

    public bool ReadOnly
    {
        get => _readOnly;
        set => SetProperty(ref _readOnly, value);
    }

    /// <summary>
    /// Validation error (blocks save).
    /// BLAZOR DISPLAY: Show in red below input field.
    /// </summary>
    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>
    /// Validation warning (does not block save).
    /// BLAZOR DISPLAY: Show in orange/yellow below input field.
    /// </summary>
    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    public bool ValueMustBeInTheList
    {
        get => _valueMustBeInTheList;
        set => SetProperty(ref _valueMustBeInTheList, value);
    }

    /// <summary>
    /// FluentValidation rules.
    /// Set this in ViewModel property initializer.
    /// </summary>
    public Action<IRuleBuilder<FieldViewModel<T>, T?>>? ValidationRules { get; set; }

    public bool HasSetValueFunction => _setValue != null;

    /// <summary>
    /// Executes FluentValidation and sets Error/Warning properties.
    /// BLAZOR NOTE: Automatically called on Value change, or call manually.
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
