namespace Model.ViewModel;

/// <summary>
/// Represents a validation error from SaveAll().
/// BLAZOR NOTE: Display these in a validation summary component.
/// </summary>
public class ValidationError
{
    public IViewModel ViewModel { get; set; } = null!;
    public string PropertyName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{PropertyName}: {ErrorMessage}";
    }
}
