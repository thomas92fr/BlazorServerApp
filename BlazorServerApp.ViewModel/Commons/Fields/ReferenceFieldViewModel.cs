using BlazorServerApp.Model.ViewModels;

namespace BlazorServerApp.ViewModel.Commons.Fields;

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

    /// <summary>
    /// Gets the ID of the referenced entity for serialization.
    /// Returns the entity ID instead of the full ViewModel.
    /// </summary>
    public override object? GetRawValue()
    {
        var vm = Value;
        if (vm == null) return null;

        // Get Model.Id via reflection on IEntityViewModel<TEntity>
        var entityVmInterface = vm.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IEntityViewModel<>));

        if (entityVmInterface != null)
        {
            var modelProp = vm.GetType().GetProperty("Model");
            var model = modelProp?.GetValue(vm);
            var idProp = model?.GetType().GetProperty("Id");
            return idProp?.GetValue(model);
        }

        return vm.ToString();
    }
}
