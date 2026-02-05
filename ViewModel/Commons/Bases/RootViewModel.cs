using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.ViewModels;
using BlazorServerApp.ViewModel.Commons.Fields;

namespace BlazorServerApp.ViewModel.Commons.Bases;

/// <summary>
/// Base class for root ViewModels that own a UnitOfWork.
/// Each tab should have its own RootViewModel-derived instance,
/// providing isolated data context and common commands (Save, Discard).
/// </summary>
public partial class RootViewModel : ObservableObject, IRootViewModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger? _logger;

    // Commands (lazy-initialized)
    private CommandViewModel? _saveCommand;
    private CommandViewModel? _discardCommand;

    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    private List<ValidationError>? _validationErrors;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public RootViewModel(IUnitOfWork unitOfWork, ILogger? logger = null)
    {
        Id = Guid.NewGuid().ToString();
        _unitOfWork = unitOfWork;
        _logger = logger;

        // Associate this RootViewModel with the UnitOfWork
        _unitOfWork.SetRootViewModel(this);
    }

    /// <summary>
    /// Unique identifier for this root ViewModel instance.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The UnitOfWork for data operations.
    /// </summary>
    public IUnitOfWork UnitOfWork => _unitOfWork;

    /// <summary>
    /// Returns this instance (root has no parent root).
    /// Explicit interface implementation to avoid name collision with class name.
    /// </summary>
    IRootViewModel? IViewModel.RootViewModel => this;

    /// <summary>
    /// Logger for diagnostics.
    /// </summary>
    public ILogger? Log => _logger;

    /// <summary>
    /// Indicates whether there are unsaved changes.
    /// </summary>
    public bool HasChanges => _unitOfWork.HasChanges();

    #region Commands

    /// <summary>
    /// Command to validate and save all changes.
    /// </summary>
    public CommandViewModel SaveCommand => _saveCommand ??= new CommandViewModel(
        parent: this,
        text: "Save",
        hint: "Validate and save all changes",
        execute: () =>
        {
            ValidationErrors = Save();
            NotifyCommandsCanExecuteChanged();
        },
        style: CommandStyle.Primary
    );

    /// <summary>
    /// Command to discard all unsaved changes.
    /// </summary>
    public CommandViewModel DiscardCommand => _discardCommand ??= new CommandViewModel(
        parent: this,
        text: "Discard",
        hint: "Discard all unsaved changes",
        execute: () =>
        {
            Discard();
            NotifyCommandsCanExecuteChanged();
        },
        canExecute: () => HasChanges,
        style: CommandStyle.Warning
    );

    #endregion

    #region IRootViewModel Implementation

    /// <summary>
    /// Saves all pending changes.
    /// Returns null on success, or validation errors.
    /// </summary>
    public List<ValidationError>? Save()
    {
        _logger?.LogDebug("RootViewModel.Save() called for tab {Id}", Id);

        var errors = _unitOfWork.SaveAll();

        if (errors == null || !errors.Any())
        {
            StatusMessage = "Saved successfully.";
            _logger?.LogInformation("Successfully saved all changes for tab {Id}", Id);
        }
        else
        {
            StatusMessage = null;
            _logger?.LogWarning("Save failed with {ErrorCount} validation error(s) for tab {Id}",
                errors.Count, Id);
        }

        return errors;
    }

    /// <summary>
    /// Discards all pending changes.
    /// </summary>
    public void Discard()
    {
        _logger?.LogDebug("RootViewModel.Discard() called for tab {Id}", Id);

        _unitOfWork.DiscardChanges();
        ValidationErrors = null;
        StatusMessage = "Changes discarded.";

        OnPropertyChanged(nameof(HasChanges));
        _logger?.LogInformation("All changes discarded for tab {Id}", Id);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Notifies commands to re-evaluate their CanExecute state.
    /// Call this after operations that might affect HasChanges.
    /// </summary>
    protected void NotifyCommandsCanExecuteChanged()
    {
        OnPropertyChanged(nameof(HasChanges));
        DiscardCommand.Command.NotifyCanExecuteChanged();
        SaveCommand.Command.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Public wrapper for OnPropertyChanged to allow notification from nested ViewModels.
    /// </summary>
    public void RaisePropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// Helper for async operations with busy indicator.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await operation();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing async operation");
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _unitOfWork.Dispose();
            _disposed = true;
            _logger?.LogDebug("RootViewModel {Id} disposed", Id);
        }
    }

    #endregion
}
