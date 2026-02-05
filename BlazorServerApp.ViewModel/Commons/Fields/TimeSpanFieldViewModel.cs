namespace BlazorServerApp.ViewModel.Commons.Fields;

public class TimeSpanFieldViewModel : FieldViewModel<TimeSpan>
{
    public TimeSpanFieldViewModel(
        object? parent = null,
        Func<TimeSpan>? getValue = null,
        Action<TimeSpan>? setValue = null,
        Func<List<TimeSpan>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
    }

    /// <summary>Show the picker inline (default: false)</summary>
    public bool Inline { get; set; }

    /// <summary>Show days in the picker (default: true)</summary>
    public bool ShowDays { get; set; } = true;

    /// <summary>Show seconds in the picker (default: true)</summary>
    public bool ShowSeconds { get; set; } = true;

    /// <summary>Placeholder text when value is empty</summary>
    public string? Placeholder { get; set; }
}
