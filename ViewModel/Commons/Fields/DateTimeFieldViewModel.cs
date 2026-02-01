namespace ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for DateTime properties.
/// BLAZOR BINDING: <InputDate @bind-Value="ViewModel.StartDate.Value" />
/// </summary>
public partial class DateTimeFieldViewModel : FieldViewModel<DateTime>
{
    public DateTimeFieldViewModel(
        object? parent = null,
        Func<DateTime>? getValue = null,
        Action<DateTime>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    public override string ToString()
    {
        return Value.ToString("dd/MM/yyyy HH:mm");
    }
}
