using Model.Entities;

namespace Model.Repositories;

/// <summary>
/// Generic repository interface for entity-specific data access.
/// Used internally by UnitOfWork for CRUD operations.
/// </summary>
public interface IGenericRepository<TEntity> where TEntity : class, IEntity
{
    /// <summary>
    /// Gets an entity by its ID.
    /// </summary>
    TEntity? GetById(int id);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    IEnumerable<TEntity> GetAll();

    /// <summary>
    /// Adds a new entity (not persisted until SaveChanges).
    /// </summary>
    void Add(TEntity entity);

    /// <summary>
    /// Marks an entity for update (EF Core tracks automatically).
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Marks an entity for deletion (not persisted until SaveChanges).
    /// </summary>
    void Delete(TEntity entity);

    /// <summary>
    /// Checks if an entity with the given ID exists.
    /// </summary>
    bool Exists(int id);
}
