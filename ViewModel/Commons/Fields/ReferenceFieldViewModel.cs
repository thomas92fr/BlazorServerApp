namespace ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for entity reference (navigation) properties.
/// T is the ViewModel type (e.g. PersonViewModel), not the entity.
/// Renders as a dropdown/select in the UI via ReferenceFieldView.
/// </summary>
/// <typeparam name="T">The referenced ViewModel type.</typeparam>
public class ReferenceFieldViewModel<T> : FieldViewModel<T> where T : class
{
    /// <summary>
    /// Custom function to format an entity for display in the dropdown.
    /// Defaults to ToString() if not set.
    /// </summary>
    public Func<T, string>? DisplaySelector { get; set; }

    public ReferenceFieldViewModel(
        object? parent = null,
        Func<T?>? getValue = null,
        Action<T?>? setValue = null,
        Func<List<T>>? listQuery = null
    ) : base(
        parent,
        getValue: getValue != null ? () => getValue()! : null,
        setValue: setValue != null ? value => setValue(value) : null,
        listQuery: listQuery
    ) { }

    /// <summary>
    /// Returns the display text for an entity item.
    /// Uses DisplaySelector if set, otherwise falls back to ToString().
    /// </summary>
    public string GetDisplayText(T? item)
    {
        if (item == null) return string.Empty;
        return DisplaySelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
    }
}
