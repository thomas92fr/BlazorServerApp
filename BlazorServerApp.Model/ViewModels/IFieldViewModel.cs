namespace BlazorServerApp.Model.ViewModels;

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

    /// <summary>
    /// If true, this field is not displayed in the UI (form or table).
    /// </summary>
    bool HiddenInUI { get; set; }

    /// <summary>
    /// Display order in table columns. Lower values appear first.
    /// Default is 0.
    /// </summary>
    int ColumnOrder { get; set; }

    /// <summary>
    /// Group header for form display. Fields with the same header are grouped together.
    /// Null or empty means no group (displayed at the top).
    /// </summary>
    string? FormGroupHeader { get; set; }

    /// <summary>
    /// Display order of the group in the form. Lower values appear first.
    /// Default is 0.
    /// </summary>
    int FormGroupOrder { get; set; }

    /// <summary>
    /// Gets the raw value for serialization purposes.
    /// </summary>
    object? GetRawValue();
}
