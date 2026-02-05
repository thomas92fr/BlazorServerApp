using CommunityToolkit.Mvvm.ComponentModel;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.ViewModels;
using Microsoft.Extensions.Logging;
using BlazorServerApp.Model.Entities;
using BlazorServerApp.ViewModel.Commons.Bases;
using BlazorServerApp.ViewModel.Persons;
using BlazorServerApp.ViewModel.Commons.Fields;

namespace BlazorServerApp.ViewModel;

/// <summary>
/// ViewModel for managing a list of persons.
/// Inherits from RootViewModel to provide Save/Discard commands and isolated UnitOfWork.
/// </summary>
public partial class PersonListViewModel : RootViewModel
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
            Label = "Persons",
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
        Title = "Persons";
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

    #region Overrides

    /// <summary>
    /// Override Save to refresh the collection after saving.
    /// </summary>
    public new List<ValidationError>? Save()
    {
        var errors = base.Save();

        if (errors == null || !errors.Any())
        {
            // Reload to refresh IDs (new entities get persisted IDs)
            Persons.Refresh();
        }

        return errors;
    }

    /// <summary>
    /// Override Discard to clear selection and refresh the collection.
    /// </summary>
    public new void Discard()
    {
        SelectedPerson = null;
        base.Discard();
        Persons.Refresh();
    }

    #endregion
}
