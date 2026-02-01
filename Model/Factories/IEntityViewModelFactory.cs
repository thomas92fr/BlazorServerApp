using Model.Repositories;
using Model.ViewModels;
using Model.Entities;

namespace Model.Factories;

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
