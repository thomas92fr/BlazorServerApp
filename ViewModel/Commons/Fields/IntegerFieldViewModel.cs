using CommunityToolkit.Mvvm.Input;
using ViewModel.Commons.Fields;

namespace ViewModel.Commons.Fields;

/// <summary>
/// FieldViewModel specialized for integer properties.
/// Includes Increment/Decrement commands for numeric inputs.
/// BLAZOR USAGE: <InputNumber @bind-Value="ViewModel.Age.Value" />
/// </summary>
public partial class IntegerFieldViewModel : FieldViewModel<int>
{
    public IntegerFieldViewModel(
        object? parent = null,
        Func<int>? getValue = null,
        Action<int>? setValue = null,
        Func<List<int>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
    }

    /// <summary>
    /// Increments the value by 1.
    /// BLAZOR USAGE: <button @onclick="ViewModel.Age.IncrementCommand.Execute">+</button>
    /// </summary>
    [RelayCommand]
    private void Increment()
    {
        if (!ReadOnly)
        {
            Value++;
        }
    }

    /// <summary>
    /// Decrements the value by 1.
    /// </summary>
    [RelayCommand]
    private void Decrement()
    {
        if (!ReadOnly)
        {
            Value--;
        }
    }
}
