using FluentValidation;
using Model.UnitOfWork;
using Microsoft.Extensions.Logging;
using ViewModel.Commons.Bases;
using ViewModel.Commons.Fields;

namespace ViewModel;

/// <summary>
/// Counter ViewModel using advanced FieldViewModel pattern.
/// </summary>
public partial class CounterViewModel : BaseViewModel
{
    private IntegerFieldViewModel? _currentCountField;
    private int _currentCountValue = 0; // Store the actual counter value

    /// <summary>
    /// Command to increment the counter.
    /// </summary>
    public CommandViewModel IncrementCountCommand { get; }

    /// <summary>
    /// Command to reset the counter to zero.
    /// </summary>
    public CommandViewModel ResetCountCommand { get; }

    public CounterViewModel(
        IUnitOfWork unitOfWork,
        ILogger<CounterViewModel>? logger = null
    ) : base(unitOfWork, logger)
    {
        IncrementCountCommand = new CommandViewModel(
            parent: this,
            text: "Click me",
            hint: "Increment the counter by 1",
            execute: IncrementCount,
            style: CommandStyle.Primary
        );

        ResetCountCommand = new CommandViewModel(
            parent: this,
            text: "Reset",
            hint: "Reset the counter to zero",
            execute: ResetCount,
            style: CommandStyle.Default
        );
    }

    /// <summary>
    /// Current count with validation.
    /// </summary>
    public IntegerFieldViewModel CurrentCount => _currentCountField ??= new IntegerFieldViewModel(
        parent: this,
        getValue: () => _currentCountValue,
        setValue: value => _currentCountValue = value)
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
    /// </summary>
    private void IncrementCount()
    {
        CurrentCount.Value++;
        OnPropertyChanged(nameof(CurrentCount)); // Notify UI that CurrentCount changed
        Log?.LogDebug("Counter incremented to {Count}", CurrentCount.Value);
    }

    /// <summary>
    /// Resets the counter to zero.
    /// </summary>
    private void ResetCount()
    {
        CurrentCount.Value = 0;
        OnPropertyChanged(nameof(CurrentCount)); // Notify UI that CurrentCount changed
        Log?.LogInformation("Counter reset");
    }
}
