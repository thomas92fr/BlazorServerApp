using Infrastructure.Repository;
using Infrastructure.ViewModel;
using Model.Entities;

namespace Infrastructure.Factory;

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
