namespace BlazorServerApp.ViewModel.Commons.Fields;

public class DecimalFieldViewModel : FieldViewModel<decimal>
{
    public DecimalFieldViewModel(
        object? parent = null,
        Func<decimal>? getValue = null,
        Action<decimal>? setValue = null,
        Func<List<decimal>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
        IncrementCommand = new CommandViewModel(
            parent: this,
            text: "+",
            hint: "Increment value",
            execute: () => { if (!ReadOnly) Value += Step; }
        );

        DecrementCommand = new CommandViewModel(
            parent: this,
            text: "-",
            hint: "Decrement value",
            execute: () => { if (!ReadOnly) Value -= Step; }
        );
    }

    /// <summary>Format string for RadzenNumeric (e.g., "#.0000", "c", "### m2")</summary>
    public string? Format { get; set; }

    /// <summary>Increment/decrement step (default: 0.01)</summary>
    public decimal Step { get; set; } = 0.01m;

    /// <summary>Minimum allowed value</summary>
    public decimal? Min { get; set; }

    /// <summary>Maximum allowed value</summary>
    public decimal? Max { get; set; }

    public CommandViewModel IncrementCommand { get; }
    public CommandViewModel DecrementCommand { get; }
}
