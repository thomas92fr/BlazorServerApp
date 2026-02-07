namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for boolean properties displayed as a toggle switch.
/// Inherits from BoolFieldViewModel; the only difference is the UI rendering (RadzenSwitch vs RadzenCheckBox).
/// </summary>
public partial class BoolSwitchFieldViewModel : BoolFieldViewModel
{
    public BoolSwitchFieldViewModel(
        object? parent = null,
        Func<bool>? getValue = null,
        Action<bool>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }
}
