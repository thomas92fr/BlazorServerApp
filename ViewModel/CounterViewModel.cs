using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Model.Repository;
using Microsoft.Extensions.Logging;
using ViewModel.Commons.Bases;
using ViewModel.Fields;

namespace ViewModel;

/// <summary>
/// Counter ViewModel using advanced FieldViewModel pattern.
///
/// BLAZOR MIGRATION NOTES:
/// - CurrentCount is now IntegerFieldViewModel (was simple int property)
/// - Validation added: count cannot be negative or exceed 1000
/// - Demonstrates how to migrate simple ViewModels to advanced pattern
/// </summary>
public partial class CounterViewModel : BaseViewModel
{
    private IntegerFieldViewModel? _currentCountField;

    public CounterViewModel(
        IRepository repository,
        ILogger<CounterViewModel>? logger = null
    ) : base(repository, logger)
    {
    }

    /// <summary>
    /// Current count with validation.
    /// BLAZOR BINDING: @bind="ViewModel.CurrentCount.Value"
    /// DISPLAY LABEL: @ViewModel.CurrentCount.Label
    /// SHOW ERROR: @ViewModel.CurrentCount.Error
    /// </summary>
    public IntegerFieldViewModel CurrentCount => _currentCountField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => 0, // Initial value
        setValue: value => { /* No persistence needed for counter */ })
    {
        Label = "Current Count",
        Hint = "Number of button clicks",
        ValidationRules = rules => rules
            .GreaterThanOrEqualTo(0).WithMessage("Count cannot be negative.")
                .WithSeverity(Severity.Error)
            .LessThanOrEqualTo(1000).WithMessage("Count cannot exceed 1000.")
                .WithSeverity(Severity.Error)
            .Must(count => count <= 100).WithMessage("Count is getting high!")
                .WithSeverity(Severity.Warning)
    };

    /// <summary>
    /// Increments the counter.
    /// BLAZOR USAGE: <button @onclick="ViewModel.IncrementCountCommand.Execute">Click me</button>
    /// </summary>
    [RelayCommand]
    private void IncrementCount()
    {
        CurrentCount.Value++;
        Log?.LogDebug("Counter incremented to {Count}", CurrentCount.Value);
    }

    /// <summary>
    /// Resets the counter to zero.
    /// NEW FEATURE: Demonstrates additional command.
    /// </summary>
    [RelayCommand]
    private void ResetCount()
    {
        CurrentCount.Value = 0;
        Log?.LogInformation("Counter reset");
    }
}
