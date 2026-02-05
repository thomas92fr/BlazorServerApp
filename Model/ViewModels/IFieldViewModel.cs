namespace Model.ViewModels;

/// <summary>
/// Interface for field-level ViewModels (properties with metadata).
/// BLAZOR NOTE: Error/Warning properties trigger validation display in UI.
/// </summary>
public interface IFieldViewModel
{
    object? Parent { get; }
    string? Label { get; set; }
    string? Hint { get; set; }
    bool ReadOnly { get; set; }
    string? Error { get; }
    string? Warning { get; }
    bool HasSetValueFunction { get; }
    void Validate();

    /// <summary>
    /// Column width for table display (CSS value: "150px", "20%", etc.). Null = auto.
    /// </summary>
    string? ColumnWidth { get; set; }

    /// <summary>
    /// If true, this field is not displayed in table columns.
    /// </summary>
    bool HiddenInColumn { get; set; }
}
