using CommunityToolkit.Mvvm.ComponentModel;
using Model.UnitOfWork;
using Model.ViewModels;
using Microsoft.Extensions.Logging;
using Model.Entities;
using ViewModel.Commons.Bases;
using ViewModel.Persons;
using ViewModel.Commons.Fields;

namespace ViewModel;

/// <summary>
/// ViewModel for managing a list of persons.
/// Extracts all business logic from PersonList.razor following MVVM pattern.
/// </summary>
public partial class PersonListViewModel : BaseViewModel
{
    #region Observable Properties

    /// <summary>
    /// Collection of all persons (using CollectionFieldViewModel).
    /// </summary>
    private CollectionFieldViewModel<PersonViewModel>? _personsField;

    public CollectionFieldViewModel<PersonViewModel> Persons =>
        _personsField ??= new CollectionFieldViewModel<PersonViewModel>(
            parent: this,
            query: () => UnitOfWork.GetAllViewModels<Person, PersonViewModel>())
        {
            Label = "Personnes",
            AllowAdd = true,
            AllowDelete = true,
            AllowMultiSelect = true,
            CreateItem = () => UnitOfWork.GetNewViewModel<Person, PersonViewModel>(),
            OnItemAdded = vm => { }, // Already tracked by UnitOfWork
            OnItemDeleted = vm => UnitOfWork.DeleteEntity(vm.Model)
        };

    /// <summary>
    /// Currently selected person for detail view.
    /// </summary>
    public PersonViewModel? SelectedPerson
    {
        get => Persons.SelectedItem;
        set
        {
            if (Persons.SelectedItem != value)
            {
                Persons.SelectedItem = value;
                OnPropertyChanged(nameof(SelectedPerson));
            }
        }
    }

    /// <summary>
    /// Validation errors from last save attempt.
    /// Null or empty if save was successful.
    /// </summary>
    [ObservableProperty]
    private List<ValidationError>? _validationErrors;

    /// <summary>
    /// Success message after successful save or discard operation.
    /// </summary>
    [ObservableProperty]
    private string? _saveSuccessMessage;

    /// <summary>
    /// Indicates whether there are unsaved changes in the repository.
    /// </summary>
    [ObservableProperty]
    private bool _hasChanges;

    #endregion

    #region Commands (lazy-initialized)

    private CommandViewModel? _saveAllCommand;
    private CommandViewModel? _discardChangesCommand;

    /// <summary>
    /// Command to validate and save all changes.
    /// </summary>
    public CommandViewModel SaveAllCommand => _saveAllCommand ??= new CommandViewModel(
        parent: this,
        text: "Save All",
        hint: "Validate and save all changes to the repository",
        execute: SaveAllInternal,
        style: CommandStyle.Primary
    );

    /// <summary>
    /// Command to discard all unsaved changes.
    /// </summary>
    public CommandViewModel DiscardChangesCommand => _discardChangesCommand ??= new CommandViewModel(
        parent: this,
        text: "Discard Changes",
        hint: "Discard all unsaved changes",
        execute: DiscardChangesInternal,
        canExecute: () => HasChanges,
        style: CommandStyle.Warning
    );

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of PersonListViewModel.
    /// </summary>
    /// <param name="unitOfWork">UnitOfWork for data access.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PersonListViewModel(
        IUnitOfWork unitOfWork,
        ILogger<PersonListViewModel>? logger = null
    ) : base(unitOfWork, logger)
    {
        Log?.LogDebug("PersonListViewModel created");
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the ViewModel by loading persons from repository.
    /// </summary>
    public void Initialize()
    {
        Log?.LogInformation("Initializing PersonListViewModel");
        // Collection is lazy-loaded on first access
        // Auto-select first person if available
        if (Persons.Collection.Any() && SelectedPerson == null)
        {
            SelectedPerson = Persons.Collection.First();
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Validates and saves all changes to the repository.
    /// Shows validation errors or success message.
    /// Reloads persons on success to refresh IDs for new entities.
    /// </summary>
    private void SaveAllInternal()
    {
        Log?.LogDebug("Attempting to save all changes");

        ValidationErrors = UnitOfWork.SaveAll();

        if (ValidationErrors == null || !ValidationErrors.Any())
        {
            SaveSuccessMessage = "All changes saved successfully!";
            Log?.LogInformation("Successfully saved all changes");

            // Reload to refresh IDs (new entities get persisted IDs)
            Persons.Refresh();
        }
        else
        {
            SaveSuccessMessage = null;
            Log?.LogWarning("Save failed with {ErrorCount} validation error(s)",
                ValidationErrors.Count);

            foreach (var error in ValidationErrors)
            {
                Log?.LogDebug("Validation error: {Property} - {Message}",
                    error.PropertyName, error.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Discards all unsaved changes.
    /// Reloads persons from repository original state.
    /// </summary>
    private void DiscardChangesInternal()
    {
        Log?.LogDebug("Discarding all changes");

        UnitOfWork.DiscardChanges();
        SelectedPerson = null;
        Persons.Refresh();

        ValidationErrors = null;
        SaveSuccessMessage = "Changes discarded.";

        Log?.LogInformation("All changes discarded");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Updates HasChanges property from repository.
    /// Called after operations that might affect change state.
    /// Notifies commands that depend on HasChanges to re-evaluate their CanExecute.
    /// </summary>
    private void UpdateHasChanges()
    {
        HasChanges = UnitOfWork.HasChanges();

        // Notify commands that depend on HasChanges to re-evaluate CanExecute
        DiscardChangesCommand.Command.NotifyCanExecuteChanged();
        SaveAllCommand.Command.NotifyCanExecuteChanged();
    }

    #endregion
}
