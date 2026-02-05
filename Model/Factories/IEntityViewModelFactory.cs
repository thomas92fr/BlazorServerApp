using Model.UnitOfWork;
using Model.ViewModels;
using Model.Entities;

namespace Model.Factories;

/// <summary>
/// Factory interface for creating ViewModels from entities.
/// Convention: {EntityName}ViewModelFactory in ViewModel namespace.
/// Supports both IRootViewModel (new) and IUnitOfWork (legacy) patterns.
/// </summary>
public interface IEntityViewModelFactory<TEntity, TViewModel>
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    /// <summary>
    /// Creates a ViewModel with access to the root ViewModel.
    /// Preferred method for new code - provides full tab context.
    /// </summary>
    TViewModel Create(TEntity entity, IRootViewModel rootViewModel);

    /// <summary>
    /// Legacy method: Creates a ViewModel with direct UnitOfWork access.
    /// For backward compatibility with ViewModels that don't need root context.
    /// </summary>
    TViewModel Create(TEntity entity, IUnitOfWork unitOfWork);
}
