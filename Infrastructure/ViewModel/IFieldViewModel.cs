namespace Infrastructure.ViewModel;

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
}
