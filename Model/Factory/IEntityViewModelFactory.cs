using Model.Repository;
using Model.ViewModel;
using Model.Entities;

namespace Model.Factory;

/// <summary>
/// Factory interface for creating ViewModels from entities.
/// Convention: {EntityName}ViewModelFactory in ViewModel namespace.
/// </summary>
public interface IEntityViewModelFactory<TEntity, TViewModel>
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    TViewModel Create(TEntity entity, IRepository repository);
}
