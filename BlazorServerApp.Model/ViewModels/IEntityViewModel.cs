using BlazorServerApp.Model.Entities;

namespace BlazorServerApp.Model.ViewModels;

/// <summary>
/// Interface for ViewModels bound to entities.
/// TEntity: The domain model type this ViewModel wraps.
/// </summary>
public interface IEntityViewModel<TEntity> : IViewModel where TEntity : class, IEntity
{
    TEntity Model { get; }
}
