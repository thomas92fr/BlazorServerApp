namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for boolean properties.
/// </summary>
public partial class BoolFieldViewModel : FieldViewModel<bool>
{
    public BoolFieldViewModel(
        object? parent = null,
        Func<bool>? getValue = null,
        Action<bool>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }
}
