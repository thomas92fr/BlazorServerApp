using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.Repository;
using Model.ViewModel;
using Microsoft.Extensions.Logging;
using Model.Entities;
using ViewModel.Commons.Bases;
using ViewModel.Persons;

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
        LoadPersonsCommand.Execute(null);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Loads all persons from repository.
    /// Auto-selects first person if none is currently selected.
    /// </summary>
    [RelayCommand]
    private void LoadPersons()
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

        UpdateHasChanges();
        Log?.LogInformation("Loaded {Count} person(s)", Persons.Count);
    }

    /// <summary>
    /// Creates a new person and adds it to the list.
    /// Automatically selects the new person and clears messages.
    /// </summary>
    [RelayCommand]
    private void AddPerson()
    {
        Log?.LogDebug("Creating new person");

        var newPerson = Repository.GetNewViewModel<Person, PersonViewModel>();
        Persons.Add(newPerson);
        SelectedPerson = newPerson;

        // Clear messages
        ValidationErrors = null;
        SaveSuccessMessage = null;

        UpdateHasChanges();
        Log?.LogInformation("Added new person (Id: {Id})", newPerson.Id.Value);
    }

    /// <summary>
    /// Selects a person for detail view and clears messages.
    /// </summary>
    /// <param name="person">Person to select.</param>
    [RelayCommand]
    private void SelectPerson(PersonViewModel person)
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
    [RelayCommand]
    private void DeleteSelectedPerson()
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
        SelectedPerson = Persons.FirstOrDefault();

        // Clear messages
        ValidationErrors = null;
        SaveSuccessMessage = null;

        UpdateHasChanges();
        Log?.LogInformation("Deleted person: {PersonName} (Id: {Id})", personName, personId);
    }

    /// <summary>
    /// Validates and saves all changes to the repository.
    /// Shows validation errors or success message.
    /// Reloads persons on success to refresh IDs for new entities.
    /// </summary>
    [RelayCommand]
    private void SaveAll()
    {
        Log?.LogDebug("Attempting to save all changes");

        ValidationErrors = Repository.SaveAll();

        if (ValidationErrors == null || !ValidationErrors.Any())
        {
            SaveSuccessMessage = "All changes saved successfully!";
            Log?.LogInformation("Successfully saved all changes");

            // Reload to refresh IDs (new entities get persisted IDs)
            LoadPersons();
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

        UpdateHasChanges();
    }

    /// <summary>
    /// Discards all unsaved changes.
    /// Reloads persons from repository original state.
    /// </summary>
    [RelayCommand]
    private void DiscardChanges()
    {
        Log?.LogDebug("Discarding all changes");

        Repository.DiscardChanges();
        LoadPersons();

        ValidationErrors = null;
        SaveSuccessMessage = "Changes discarded.";

        UpdateHasChanges();
        Log?.LogInformation("All changes discarded");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Updates HasChanges property from repository.
    /// Called after operations that might affect change state.
    /// </summary>
    private void UpdateHasChanges()
    {
        HasChanges = Repository.HasChanges();
    }

    #endregion
}
