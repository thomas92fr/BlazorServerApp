namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for password properties.
/// Masks the value in MCP serialization via GetRawValue().
/// </summary>
public partial class PasswordFieldViewModel : FieldViewModel<string>
{
    public PasswordFieldViewModel(
        object? parent = null,
        Func<string>? getValue = null,
        Action<string>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    /// <summary>
    /// Returns a masked value to avoid exposing passwords via MCP tools.
    /// </summary>
    public override object? GetRawValue() =>
        string.IsNullOrEmpty(Value) ? null : "***";
}
