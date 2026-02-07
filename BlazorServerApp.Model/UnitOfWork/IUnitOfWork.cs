using System.Linq.Expressions;
using BlazorServerApp.Model.Entities;
using BlazorServerApp.Model.Repositories;
using BlazorServerApp.Model.ViewModels;

namespace BlazorServerApp.Model.UnitOfWork;

/// <summary>
/// Unit of Work interface that combines:
/// - DbContext management
/// - Generic repositories access
/// - ViewModel caching
/// - Transaction support
///
/// Blazor Note: Registered as Scoped (per user circuit).
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    #region Generic Repository Access

    /// <summary>
    /// Gets a generic repository for the specified entity type.
    /// </summary>
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IEntity;

    #endregion

    #region ViewModel Management

    /// <summary>
    /// Associates this UnitOfWork with a RootViewModel.
    /// Called by RootViewModel constructor to enable IRootViewModel injection into entity ViewModels.
    /// </summary>
    void SetRootViewModel(IRootViewModel rootViewModel);

    /// <summary>
    /// Gets or creates a ViewModel for the given entity.
    /// Returns null if entity is null.
    /// </summary>
    TViewModel? GetViewModel<TEntity, TViewModel>(TEntity? entity)
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    /// <summary>
    /// Loads all entities and returns their ViewModels.
    /// </summary>
    IEnumerable<TViewModel> GetAllViewModels<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    /// <summary>
    /// Loads entities matching the filter expression and returns their ViewModels.
    /// The filter is applied server-side (EF Core → SQL) before materialization.
    /// </summary>
    IEnumerable<TViewModel> GetFilteredViewModels<TEntity, TViewModel>(
        Expression<Func<TEntity, bool>> filter)
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    /// <summary>
    /// Creates a new entity and returns its ViewModel.
    /// Entity is not persisted until SaveAll() is called.
    /// </summary>
    TViewModel GetNewViewModel<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    /// <summary>
    /// Marks an entity for deletion.
    /// Not actually deleted until SaveAll() is called.
    /// </summary>
    void DeleteEntity<TEntity>(TEntity entity) where TEntity : class, IEntity;

    /// <summary>
    /// Marks an entity as modified for change tracking.
    /// </summary>
    void MarkAsModified<TEntity>(TEntity entity) where TEntity : class, IEntity;

    #endregion

    #region Change Tracking and Persistence

    /// <summary>
    /// Validates all ViewModels and saves changes to database.
    /// Returns null on success, or list of ValidationError on failure.
    /// </summary>
    List<ValidationError>? SaveAll();

    /// <summary>
    /// Saves all changes to the database asynchronously.
    /// Returns null on success, or validation errors if any.
    /// </summary>
    Task<List<ValidationError>?> SaveChangesAsync();

    /// <summary>
    /// Checks if there are any pending changes.
    /// </summary>
    bool HasChanges();

    /// <summary>
    /// Discards all pending changes.
    /// </summary>
    void DiscardChanges();

    /// <summary>
    /// Allows saving even if there are validation errors.
    /// </summary>
    bool AllowSaveWithErrors { get; set; }

    #endregion

    #region Transaction Support

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync();

    #endregion
}
