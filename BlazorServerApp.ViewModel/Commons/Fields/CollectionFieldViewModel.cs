using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using BlazorServerApp.Model.ViewModels;

namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel for managing collections of ViewModels.
/// Renders as an HTML table in the UI via CollectionFieldView.
///
/// Key Features:
/// - ObservableCollection with change notification
/// - Lazy loading via Query function
/// - Add/Update/Delete permissions
/// - Column definitions auto-generated from IFieldViewModel properties
/// </summary>
/// <typeparam name="T">The ViewModel type for items in the collection.</typeparam>
public partial class CollectionFieldViewModel<T> : ObservableObject, ICollectionFieldViewModel
    where T : class
{
    private ObservableCollection<T>? _collection;
    private T? _selectedItem;
    private T? _lastClickedItem; // Anchor for Shift+Click range selection
    private ObservableCollection<T>? _selectedItems;
    private readonly Func<IEnumerable<T>>? _query;
    private bool _isInitialized;
    private List<PropertyInfo>? _columnProperties;

    // UI Metadata
    private string? _label;
    private string? _hint;
    private bool _readOnly;
    private string? _error;
    private string? _warning;
    private string? _columnWidth;
    private bool _hiddenInColumn;
    private bool _hiddenInUI;
    private int _columnOrder;
    private string? _formGroupHeader;
    private int _formGroupOrder;

    // Permission flags
    private bool _allowAdd = true;
    private bool _allowUpdate = true;
    private bool _allowDelete = true;
    private bool _allowMultiSelect = false;
    private bool _allowInlineEdit = false;

    // Commands
    private CommandViewModel? _addCommand;
    private CommandViewModel<T>? _deleteCommand;
    private CommandViewModel? _deleteSelectedCommand;
    private CommandViewModel? _refreshCommand;
    private CommandViewModel? _selectAllCommand;
    private CommandViewModel? _clearSelectionCommand;

    public CollectionFieldViewModel(
        object? parent = null,
        Func<IEnumerable<T>>? query = null)
    {
        Parent = parent;
        _query = query;
        BuildColumnProperties();
    }

    public object? Parent { get; }

    #region Collection Property

    /// <summary>
    /// The observable collection of ViewModels.
    /// Lazy-loaded from Query on first access.
    /// </summary>
    public ObservableCollection<T> Collection
    {
        get
        {
            if (!_isInitialized && _query != null)
            {
                _collection = new ObservableCollection<T>(_query());
                _collection.CollectionChanged += OnCollectionChanged;
                _isInitialized = true;
            }
            return _collection ??= new ObservableCollection<T>();
        }
    }

    /// <summary>
    /// Number of items in the collection.
    /// </summary>
    public int Count => Collection.Count;

    /// <summary>
    /// Currently selected item (for single selection mode).
    /// </summary>
    public T? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    /// <summary>
    /// Currently selected items (for multi-selection mode).
    /// </summary>
    public ObservableCollection<T> SelectedItems
    {
        get => _selectedItems ??= new ObservableCollection<T>();
    }

    /// <summary>
    /// Number of selected items.
    /// </summary>
    public int SelectedCount => SelectedItems.Count;

    /// <summary>
    /// Returns true if the item is currently selected.
    /// </summary>
    public bool IsSelected(T item)
    {
        if (AllowMultiSelect)
        {
            return SelectedItems.Contains(item);
        }
        return Equals(item, SelectedItem);
    }

    #endregion

    #region UI Metadata

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

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    public string? ColumnWidth
    {
        get => _columnWidth;
        set => SetProperty(ref _columnWidth, value);
    }

    public bool HiddenInColumn
    {
        get => _hiddenInColumn;
        set => SetProperty(ref _hiddenInColumn, value);
    }

    public bool HiddenInUI
    {
        get => _hiddenInUI;
        set => SetProperty(ref _hiddenInUI, value);
    }

    public int ColumnOrder
    {
        get => _columnOrder;
        set => SetProperty(ref _columnOrder, value);
    }

    public string? FormGroupHeader
    {
        get => _formGroupHeader;
        set => SetProperty(ref _formGroupHeader, value);
    }

    public int FormGroupOrder
    {
        get => _formGroupOrder;
        set => SetProperty(ref _formGroupOrder, value);
    }

    public bool HasSetValueFunction => false;

    #endregion

    #region Permissions

    public bool AllowAdd
    {
        get => _allowAdd && !ReadOnly;
        set => SetProperty(ref _allowAdd, value);
    }

    public bool AllowUpdate
    {
        get => _allowUpdate && !ReadOnly;
        set => SetProperty(ref _allowUpdate, value);
    }

    public bool AllowDelete
    {
        get => _allowDelete && !ReadOnly;
        set => SetProperty(ref _allowDelete, value);
    }

    /// <summary>
    /// If true, multiple items can be selected at once.
    /// </summary>
    public bool AllowMultiSelect
    {
        get => _allowMultiSelect;
        set => SetProperty(ref _allowMultiSelect, value);
    }

    /// <summary>
    /// If true, cells can be edited inline by clicking on them.
    /// </summary>
    public bool AllowInlineEdit
    {
        get => _allowInlineEdit && !ReadOnly;
        set => SetProperty(ref _allowInlineEdit, value);
    }

    #endregion

    #region Column Discovery

    /// <summary>
    /// List of IFieldViewModel properties visible as columns.
    /// Auto-generated by reflection on T, sorted by ColumnOrder.
    /// </summary>
    public IReadOnlyList<IFieldViewModel> ColumnFields
    {
        get
        {
            if (Collection.Count == 0 || _columnProperties == null)
                return Array.Empty<IFieldViewModel>();

            var firstItem = Collection.First();
            return _columnProperties
                .Select(p => p.GetValue(firstItem) as IFieldViewModel)
                .Where(f => f != null && !f.HiddenInColumn)
                .OrderBy(f => f!.ColumnOrder)
                .ToList()!;
        }
    }

    /// <summary>
    /// Property infos for column rendering (used by view), sorted by ColumnOrder.
    /// </summary>
    public IReadOnlyList<PropertyInfo> ColumnProperties
    {
        get
        {
            if (Collection.Count == 0 || _columnProperties == null)
                return new List<PropertyInfo>();

            var firstItem = Collection.First();
            return _columnProperties
                .Select(p => new { Property = p, Field = p.GetValue(firstItem) as IFieldViewModel })
                .Where(x => x.Field != null && !x.Field.HiddenInColumn)
                .OrderBy(x => x.Field!.ColumnOrder)
                .Select(x => x.Property)
                .ToList();
        }
    }

    private void BuildColumnProperties()
    {
        _columnProperties = typeof(T)
            .GetProperties()
            .Where(p => typeof(IFieldViewModel).IsAssignableFrom(p.PropertyType))
            .ToList();
    }

    #endregion

    #region CRUD Delegates

    /// <summary>
    /// Function to create a new item.
    /// </summary>
    public Func<T>? CreateItem { get; set; }

    /// <summary>
    /// Callback after an item is added (e.g., add Model to DbSet).
    /// </summary>
    public Action<T>? OnItemAdded { get; set; }

    /// <summary>
    /// Callback when an item is deleted (e.g., delete entity from UnitOfWork).
    /// </summary>
    public Action<T>? OnItemDeleted { get; set; }

    #endregion

    #region Commands

    /// <summary>
    /// Command to add a new item to the collection.
    /// </summary>
    public CommandViewModel AddCommand => _addCommand ??= new CommandViewModel(
        parent: this,
        text: "Add",
        hint: "Add a new item",
        execute: Add,
        canExecute: () => AllowAdd && CreateItem != null,
        style: CommandStyle.Success
    );

    /// <summary>
    /// Command to delete an item from the collection.
    /// </summary>
    public CommandViewModel<T> DeleteCommand => _deleteCommand ??= new CommandViewModel<T>(
        parent: this,
        text: "Delete",
        hint: "Delete this item",
        execute: Remove,
        canExecute: item => AllowDelete && item != null,
        style: CommandStyle.Danger
    );

    /// <summary>
    /// Command to refresh the collection from the query.
    /// </summary>
    public CommandViewModel RefreshCommand => _refreshCommand ??= new CommandViewModel(
        parent: this,
        text: "Refresh",
        hint: "Reload the collection",
        execute: Refresh,
        style: CommandStyle.Info
    );

    /// <summary>
    /// Command to delete all selected items.
    /// </summary>
    public CommandViewModel DeleteSelectedCommand => _deleteSelectedCommand ??= new CommandViewModel(
        parent: this,
        text: "Delete Selected",
        hint: "Delete all selected items",
        execute: RemoveSelected,
        canExecute: () => AllowDelete && AllowMultiSelect && SelectedItems.Count > 0,
        style: CommandStyle.Danger
    );

    /// <summary>
    /// Command to select all items.
    /// </summary>
    public CommandViewModel SelectAllCommand => _selectAllCommand ??= new CommandViewModel(
        parent: this,
        text: "Select All",
        hint: "Select all items",
        execute: SelectAll,
        canExecute: () => AllowMultiSelect,
        style: CommandStyle.Default
    );

    /// <summary>
    /// Command to clear selection.
    /// </summary>
    public CommandViewModel ClearSelectionCommand => _clearSelectionCommand ??= new CommandViewModel(
        parent: this,
        text: "Clear Selection",
        hint: "Clear all selections",
        execute: ClearSelection,
        canExecute: () => AllowMultiSelect && SelectedItems.Count > 0,
        style: CommandStyle.Default
    );

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Adds a new item to the collection.
    /// </summary>
    public void Add()
    {
        if (!AllowAdd || CreateItem == null) return;

        var newItem = CreateItem();
        Collection.Add(newItem);
        OnItemAdded?.Invoke(newItem);
        Select(newItem);

        // Notify UI of collection change
        OnPropertyChanged(nameof(Collection));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ColumnFields));

        Validate();
    }

    /// <summary>
    /// Removes an item from the collection.
    /// </summary>
    public void Remove(T item)
    {
        if (!AllowDelete || item == null) return;

        OnItemDeleted?.Invoke(item);
        Collection.Remove(item);

        if (SelectedItem == item)
        {
            SelectedItem = Collection.FirstOrDefault();
        }

        // Notify UI of collection change
        OnPropertyChanged(nameof(Collection));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ColumnFields));

        Validate();
    }

    /// <summary>
    /// Refreshes the collection from the query.
    /// </summary>
    public void Refresh()
    {
        if (_query == null) return;

        if (_collection != null)
        {
            _collection.CollectionChanged -= OnCollectionChanged;
        }

        _collection = new ObservableCollection<T>(_query());
        _collection.CollectionChanged += OnCollectionChanged;
        _isInitialized = true;

        // Clear selections
        SelectedItems.Clear();
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));

        OnPropertyChanged(nameof(Collection));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ColumnFields));

        if (SelectedItem != null && !Collection.Contains(SelectedItem))
        {
            SelectedItem = Collection.FirstOrDefault();
        }

        Validate();
    }

    #endregion

    #region Multi-Select Operations

    /// <summary>
    /// Toggles the selection state of an item (for multi-select mode with Ctrl+Click).
    /// </summary>
    public void ToggleSelection(T item)
    {
        if (item == null) return;

        if (SelectedItems.Contains(item))
        {
            SelectedItems.Remove(item);
        }
        else
        {
            SelectedItems.Add(item);
        }

        // Update anchor and selected item
        _lastClickedItem = item;
        SelectedItem = SelectedItems.LastOrDefault();

        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Selects a single item (clears other selections in multi-select mode).
    /// </summary>
    public void Select(T item)
    {
        if (item == null) return;

        if (AllowMultiSelect)
        {
            SelectedItems.Clear();
            SelectedItems.Add(item);
            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(SelectedCount));
        }

        _lastClickedItem = item;
        SelectedItem = item;
    }

    /// <summary>
    /// Selects a range of items from the last clicked item to the specified item (for Shift+Click).
    /// </summary>
    public void SelectRange(T item)
    {
        if (item == null || !AllowMultiSelect) return;

        // If no previous anchor, just select the item
        if (_lastClickedItem == null)
        {
            Select(item);
            return;
        }

        var startIndex = Collection.IndexOf(_lastClickedItem);
        var endIndex = Collection.IndexOf(item);

        if (startIndex == -1 || endIndex == -1)
        {
            Select(item);
            return;
        }

        // Ensure start <= end
        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        // Clear current selection and select the range
        SelectedItems.Clear();
        for (int i = startIndex; i <= endIndex; i++)
        {
            SelectedItems.Add(Collection[i]);
        }

        SelectedItem = item;
        // Keep the original anchor for subsequent Shift+Clicks

        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Selects all items in the collection.
    /// </summary>
    public void SelectAll()
    {
        if (!AllowMultiSelect) return;

        SelectedItems.Clear();
        foreach (var item in Collection)
        {
            SelectedItems.Add(item);
        }

        SelectedItem = SelectedItems.LastOrDefault();

        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Clears all selections.
    /// </summary>
    public void ClearSelection()
    {
        SelectedItems.Clear();
        SelectedItem = null;

        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Removes all selected items from the collection.
    /// </summary>
    public void RemoveSelected()
    {
        if (!AllowDelete || SelectedItems.Count == 0) return;

        var itemsToRemove = SelectedItems.ToList();
        foreach (var item in itemsToRemove)
        {
            OnItemDeleted?.Invoke(item);
            Collection.Remove(item);
        }

        SelectedItems.Clear();
        SelectedItem = Collection.FirstOrDefault();

        OnPropertyChanged(nameof(Collection));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ColumnFields));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));

        Validate();
    }

    #endregion

    #region Validation

    /// <summary>
    /// FluentValidation rules for the collection.
    /// Example: Minimum/maximum item count.
    /// </summary>
    public Action<IRuleBuilder<CollectionFieldViewModel<T>, ObservableCollection<T>>>? ValidationRules { get; set; }

    /// <summary>
    /// Validates the collection (e.g., min/max count).
    /// </summary>
    public void Validate()
    {
        Error = null;
        Warning = null;

        if (ValidationRules == null) return;

        var validator = new InlineValidator<CollectionFieldViewModel<T>>();
        var ruleBuilder = validator.RuleFor(x => x.Collection);
        ValidationRules(ruleBuilder);

        var result = validator.Validate(this);

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

    #endregion

    #region Private Helpers

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(ColumnFields));
    }

    #endregion

    public override string ToString()
    {
        return Label ?? $"Liste ({Label})";
    }

    /// <summary>
    /// Gets the collection count for serialization purposes.
    /// </summary>
    public object? GetRawValue() => Count;
}
