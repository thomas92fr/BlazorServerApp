namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for integer properties displayed as a slider.
/// </summary>
public class IntegerSliderFieldViewModel : FieldViewModel<int>
{
    public IntegerSliderFieldViewModel(
        object? parent = null,
        Func<int>? getValue = null,
        Action<int>? setValue = null
    ) : base(parent, getValue, setValue, listQuery: null)
    {
    }

    /// <summary>
    /// Minimum value of the slider. Default is 0.
    /// </summary>
    public int Min { get; set; } = 0;

    /// <summary>
    /// Maximum value of the slider. Default is 100.
    /// </summary>
    public int Max { get; set; } = 100;

    /// <summary>
    /// Step increment of the slider. Default is 1.
    /// </summary>
    public int Step { get; set; } = 1;
}
