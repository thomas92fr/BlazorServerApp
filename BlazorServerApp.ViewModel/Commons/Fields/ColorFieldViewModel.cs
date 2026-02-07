namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for color properties.
/// Stores colors as strings (e.g., "rgb(68, 58, 110)", "#443A6E").
/// Uses RadzenColorPicker in the UI layer.
/// </summary>
public partial class ColorFieldViewModel : FieldViewModel<string>
{
    private bool _showHSV = true;
    private bool _showRGBA = true;
    private bool _showColors = true;
    private bool _showButton;

    public ColorFieldViewModel(
        object? parent = null,
        Func<string>? getValue = null,
        Action<string>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    /// <summary>
    /// Show the HSV picker panel. Default is true.
    /// </summary>
    public bool ShowHSV
    {
        get => _showHSV;
        set => SetProperty(ref _showHSV, value);
    }

    /// <summary>
    /// Show the RGBA picker panel. Default is true.
    /// </summary>
    public bool ShowRGBA
    {
        get => _showRGBA;
        set => SetProperty(ref _showRGBA, value);
    }

    /// <summary>
    /// Show predefined colors panel. Default is true.
    /// </summary>
    public bool ShowColors
    {
        get => _showColors;
        set => SetProperty(ref _showColors, value);
    }

    /// <summary>
    /// Show the OK button. Default is false.
    /// </summary>
    public bool ShowButton
    {
        get => _showButton;
        set => SetProperty(ref _showButton, value);
    }
}
