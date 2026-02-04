namespace ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for string properties.
/// </summary>
public partial class StringFieldViewModel : FieldViewModel<string>
{
    public StringFieldViewModel(
        object? parent = null,
        Func<string>? getValue = null,
        Action<string>? setValue = null,
        Func<List<string>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
    }
}
