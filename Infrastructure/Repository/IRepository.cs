using Infrastructure.ViewModel;
using Model.Entities;

namespace Infrastructure.Repository;

/// <summary>
/// Generic repository interface for data access and ViewModel caching.
/// Blazor Note: Registered as Scoped (per user circuit) instead of Singleton.
/// </summary>
public interface IRepository
{
    TViewModel GetViewModel<TEntity, TViewModel>(TEntity entity)
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    IEnumerable<TViewModel> GetAllViewModels<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    TViewModel GetNewViewModel<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>;

    List<ValidationError>? SaveAll();
    bool HasChanges();
    void DiscardChanges();
    void DeleteEntity<TEntity>(TEntity entity) where TEntity : class, IEntity;
    void MarkAsModified<TEntity>(TEntity entity) where TEntity : class, IEntity;

    bool AllowSaveWithErrors { get; set; }
}
