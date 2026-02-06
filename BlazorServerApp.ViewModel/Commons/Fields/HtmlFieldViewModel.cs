namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for HTML rich-text content.
/// Uses RadzenHtmlEditor in the UI layer.
/// </summary>
public partial class HtmlFieldViewModel : FieldViewModel<string>
{
    private string _height = "450px";
    private string? _uploadUrl;

    public HtmlFieldViewModel(
        object? parent = null,
        Func<string>? getValue = null,
        Action<string>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    /// <summary>
    /// CSS height for the HTML editor (e.g., "450px", "300px").
    /// Default is "450px".
    /// </summary>
    public string Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    /// <summary>
    /// Optional upload URL for image uploads in the editor.
    /// If null, the upload button is not shown.
    /// </summary>
    public string? UploadUrl
    {
        get => _uploadUrl;
        set => SetProperty(ref _uploadUrl, value);
    }
}
