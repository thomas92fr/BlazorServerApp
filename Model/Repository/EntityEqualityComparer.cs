using Model.Entities;

namespace Model.Repository;

/// <summary>
/// Comparator for entities in cache.
/// Handles entities with Id=0 (not yet persisted) by assigning temporary negative IDs.
/// WPF Pattern: Unchanged - works identically in Blazor.
/// </summary>
public class EntityEqualityComparer<TEntity> : IEqualityComparer<TEntity>
    where TEntity : IEntity
{
    private static int _tempIdCounter = -1;
    private readonly Dictionary<TEntity, int> _tempIds = new();

    public bool Equals(TEntity? x, TEntity? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        int xId = GetEntityId(x);
        int yId = GetEntityId(y);
        return xId == yId;
    }

    public int GetHashCode(TEntity obj)
    {
        return obj is null ? 0 : GetEntityId(obj);
    }

    private int GetEntityId(TEntity entity)
    {
        // If entity has a persisted ID, use it
        if (entity.Id != 0) return entity.Id;

        // Otherwise assign a temporary negative ID
        if (!_tempIds.TryGetValue(entity, out int tempId))
        {
            tempId = Interlocked.Decrement(ref _tempIdCounter);
            _tempIds[entity] = tempId;
        }
        return tempId;
    }
}
