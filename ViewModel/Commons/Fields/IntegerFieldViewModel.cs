using ViewModel.Commons.Fields;

namespace ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for integer properties.
/// Includes Increment/Decrement commands for numeric inputs.
/// </summary>
public class IntegerFieldViewModel : FieldViewModel<int>
{
    public IntegerFieldViewModel(
        object? parent = null,
        Func<int>? getValue = null,
        Action<int>? setValue = null,
        Func<List<int>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
        IncrementCommand = new CommandViewModel(
            parent: this,
            text: "+",
            hint: "Increment value",
            execute: () => { if (!ReadOnly) Value++; }
        );

        DecrementCommand = new CommandViewModel(
            parent: this,
            text: "-",
            hint: "Decrement value",
            execute: () => { if (!ReadOnly) Value--; }
        );
    }

    /// <summary>
    /// Increments the value by 1.
    /// </summary>
    public CommandViewModel IncrementCommand { get; }

    /// <summary>
    /// Decrements the value by 1.
    /// </summary>
    public CommandViewModel DecrementCommand { get; }
}
