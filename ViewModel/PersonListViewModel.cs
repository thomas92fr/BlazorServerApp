using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.Repositories;
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
    /// List of all persons.
    /// </summary>
    [ObservableProperty]
    private List<PersonViewModel> _persons = new();

    /// <summary>
    /// Currently selected person for detail view.
    /// </summary>
    [ObservableProperty]
    private PersonViewModel? _selectedPerson;

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

    #region Commands

    /// <summary>
    /// Command to load all persons from repository.
    /// </summary>
    public CommandViewModel LoadPersonsCommand { get; }

    /// <summary>
    /// Command to create a new person.
    /// </summary>
    public CommandViewModel AddPersonCommand { get; }

    /// <summary>
    /// Command to select a person for detail view.
    /// </summary>
    public CommandViewModel<PersonViewModel> SelectPersonCommand { get; }

    /// <summary>
    /// Command to delete the currently selected person.
    /// </summary>
    public CommandViewModel DeletePersonCommand { get; }

    /// <summary>
    /// Command to validate and save all changes.
    /// </summary>
    public CommandViewModel SaveAllCommand { get; }

    /// <summary>
    /// Command to discard all unsaved changes.
    /// </summary>
    public CommandViewModel DiscardChangesCommand { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of PersonListViewModel.
    /// </summary>
    /// <param name="repository">Repository for data access.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PersonListViewModel(
        IRepository repository,
        ILogger<PersonListViewModel>? logger = null
    ) : base(repository, logger)
    {
        Log?.LogDebug("PersonListViewModel created");

        // Initialize commands without parameter
        LoadPersonsCommand = new CommandViewModel(
            parent: this,
            text: "Load Persons",
            hint: "Reload all persons from repository",
            execute: LoadPersonsInternal,
            style: CommandStyle.Info
        );

        AddPersonCommand = new CommandViewModel(
            parent: this,
            text: "Add Person",
            hint: "Create a new person",
            execute: AddPersonInternal,
            style: CommandStyle.Success
        );

        DeletePersonCommand = new CommandViewModel(
            parent: this,
            text: "Delete Person",
            hint: "Delete the currently selected person",
            execute: DeleteSelectedPersonInternal,
            style: CommandStyle.Danger
        );

        SaveAllCommand = new CommandViewModel(
            parent: this,
            text: "Save All",
            hint: "Validate and save all changes to the repository",
            execute: SaveAllInternal,
            style: CommandStyle.Primary
        );

        DiscardChangesCommand = new CommandViewModel(
            parent: this,
            text: "Discard Changes",
            hint: "Discard all unsaved changes",
            execute: DiscardChangesInternal,
            canExecute: () => HasChanges,
            style: CommandStyle.Warning
        );

        // Initialize commands with parameter
        SelectPersonCommand = new CommandViewModel<PersonViewModel>(
            parent: this,
            text: "Select",
            hint: "Select this person for detail view",
            execute: SelectPersonInternal,
            style: CommandStyle.Default
        );
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the ViewModel by loading persons from repository.
    /// Call this from OnInitialized() in the Blazor component.
    /// </summary>
    public void Initialize()
    {
        Log?.LogInformation("Initializing PersonListViewModel");
        LoadPersonsCommand.Command.Execute(null);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Loads all persons from repository.
    /// Auto-selects first person if none is currently selected.
    /// </summary>
    private void LoadPersonsInternal()
    {
        Log?.LogDebug("Loading persons from repository");

        Persons = Repository.GetAllViewModels<Person, PersonViewModel>().ToList();

        // Auto-select first person if list is not empty and no person is selected
        if (Persons.Any() && SelectedPerson == null)
        {
            SelectedPerson = Persons.First();
            Log?.LogDebug("Auto-selected first person: {PersonName} (Id: {Id})",
                SelectedPerson.Name.Value, SelectedPerson.Id.Value);
        }

        Log?.LogInformation("Loaded {Count} person(s)", Persons.Count);
    }

    /// <summary>
    /// Creates a new person and adds it to the list.
    /// Automatically selects the new person and clears messages.
    /// </summary>
    private void AddPersonInternal()
    {
        Log?.LogDebug("Creating new person");

        var newPerson = Repository.GetNewViewModel<Person, PersonViewModel>();
        Persons.Add(newPerson);
        SelectedPerson = newPerson;

        // Clear messages
        ValidationErrors = null;
        SaveSuccessMessage = null;

        Log?.LogInformation("Added new person (Id: {Id})", newPerson.Id.Value);
    }

    /// <summary>
    /// Selects a person for detail view and clears messages.
    /// </summary>
    /// <param name="person">Person to select.</param>
    private void SelectPersonInternal(PersonViewModel person)
    {
        if (person == null)
        {
            Log?.LogWarning("Attempted to select null person");
            return;
        }

        SelectedPerson = person;

        // Clear messages when switching persons
        ValidationErrors = null;
        SaveSuccessMessage = null;

        Log?.LogDebug("Selected person: {PersonName} (Id: {Id})",
        person.Name.Value, person.Id.Value);
    }

    /// <summary>
    /// Deletes the currently selected person.
    /// Selects the first remaining person after deletion.
    /// </summary>
    private void DeleteSelectedPersonInternal()
    {
        if (SelectedPerson == null)
        {
            Log?.LogWarning("Attempted to delete null selected person");
            return;
        }

        var personName = SelectedPerson.Name.Value;
        var personId = SelectedPerson.Id.Value;

        Log?.LogDebug("Deleting person: {PersonName} (Id: {Id})", personName, personId);

        Repository.DeleteEntity(SelectedPerson.Model);
        Persons.Remove(SelectedPerson);
        OnPropertyChanged(nameof(Persons)); // Notify UI that collection changed
        SelectedPerson = Persons.FirstOrDefault();

        // Clear messages
        ValidationErrors = null;
        SaveSuccessMessage = null;

        Log?.LogInformation("Deleted person: {PersonName} (Id: {Id})", personName, personId);
    }

    /// <summary>
    /// Validates and saves all changes to the repository.
    /// Shows validation errors or success message.
    /// Reloads persons on success to refresh IDs for new entities.
    /// </summary>
    private void SaveAllInternal()
    {
        Log?.LogDebug("Attempting to save all changes");

        ValidationErrors = Repository.SaveAll();

        if (ValidationErrors == null || !ValidationErrors.Any())
        {
            SaveSuccessMessage = "All changes saved successfully!";
            Log?.LogInformation("Successfully saved all changes");

            // Reload to refresh IDs (new entities get persisted IDs)
            LoadPersonsInternal();
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

        Repository.DiscardChanges();
        SelectedPerson= null;
        LoadPersonsInternal();

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
        HasChanges = Repository.HasChanges();

        // Notify commands that depend on HasChanges to re-evaluate CanExecute
        DiscardChangesCommand.Command.NotifyCanExecuteChanged();
        SaveAllCommand.Command.NotifyCanExecuteChanged();
    }

    #endregion
}
