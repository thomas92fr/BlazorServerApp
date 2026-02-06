namespace BlazorServerApp.ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for file upload.
/// Value stores a combined format: "filename\nbase64dataurl".
/// The View splits/combines transparently.
/// </summary>
public partial class FileFieldViewModel : FieldViewModel<string>
{
    private string? _accept;
    private const char Separator = '\n';

    public FileFieldViewModel(
        object? parent = null,
        Func<string>? getValue = null,
        Action<string>? setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    /// <summary>
    /// Accepted file types filter (e.g., "image/*", ".pdf,.docx").
    /// If null, all file types are accepted.
    /// </summary>
    public string? Accept
    {
        get => _accept;
        set => SetProperty(ref _accept, value);
    }

    /// <summary>
    /// Returns the file name for serialization.
    /// </summary>
    public override object? GetRawValue() => ExtractFileName(Value);

    /// <summary>
    /// Extracts the file name from the combined "filename\ndataurl" format.
    /// </summary>
    public static string? ExtractFileName(string? combined)
    {
        if (string.IsNullOrEmpty(combined)) return null;
        var idx = combined.IndexOf(Separator);
        return idx >= 0 ? combined[..idx] : null;
    }

    /// <summary>
    /// Extracts the data URL from the combined "filename\ndataurl" format.
    /// </summary>
    public static string? ExtractDataUrl(string? combined)
    {
        if (string.IsNullOrEmpty(combined)) return null;
        var idx = combined.IndexOf(Separator);
        return idx >= 0 ? combined[(idx + 1)..] : combined;
    }

    /// <summary>
    /// Combines a file name and data URL into the storage format.
    /// </summary>
    public static string Combine(string? fileName, string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return string.Empty;
        return string.IsNullOrEmpty(fileName) ? dataUrl : $"{fileName}{Separator}{dataUrl}";
    }
}
