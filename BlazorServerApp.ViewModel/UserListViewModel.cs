using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.Entities;
using BlazorServerApp.ViewModel.Commons.Bases;
using BlazorServerApp.ViewModel.Commons.Fields;
using BlazorServerApp.ViewModel.Users;
using Microsoft.Extensions.Logging;

namespace BlazorServerApp.ViewModel;

/// <summary>
/// ViewModel for managing a list of users.
/// Inherits from RootViewModel to provide Save/Discard commands and isolated UnitOfWork.
/// </summary>
public partial class UserListViewModel : RootViewModel
{
    #region Observable Properties

    /// <summary>
    /// Collection of all users.
    /// </summary>
    private CollectionFieldViewModel<UserViewModel>? _usersField;

    public CollectionFieldViewModel<UserViewModel> Users =>
        _usersField ??= new CollectionFieldViewModel<UserViewModel>(
            parent: this,
            query: _ => UnitOfWork.GetAllViewModels<User, UserViewModel>())
        {
            Label = "Users",
            AllowAdd = true,
            AllowDelete = true,
            AllowMultiSelect = true,
            AllowInlineEdit = true,
            CreateItem = () => UnitOfWork.GetNewViewModel<User, UserViewModel>(),
            OnItemAdded = vm => { },
            OnItemDeleted = vm => UnitOfWork.DeleteEntity(vm.Model)
        };

    /// <summary>
    /// Currently selected user for detail view.
    /// </summary>
    public UserViewModel? SelectedUser
    {
        get => Users.SelectedItem;
        set
        {
            if (Users.SelectedItem != value)
            {
                Users.SelectedItem = value;
                OnPropertyChanged(nameof(SelectedUser));
            }
        }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of UserListViewModel.
    /// </summary>
    public UserListViewModel(
        IUnitOfWork unitOfWork,
        ILogger<UserListViewModel>? logger = null
    ) : base(unitOfWork, logger)
    {
        Title = "Users";

        OnSaved = () => Users.Refresh();
        OnDiscarded = () =>
        {
            SelectedUser = null;
            Users.Refresh();
        };

        Log?.LogDebug("UserListViewModel created");
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the ViewModel by loading users.
    /// </summary>
    public void Initialize()
    {
        Log?.LogInformation("Initializing UserListViewModel");
        if (Users.Collection.Any() && SelectedUser == null)
        {
            SelectedUser = Users.Collection.First();
        }
    }

    #endregion
}
