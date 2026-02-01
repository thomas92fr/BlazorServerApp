namespace Model.Entities;

/// <summary>
/// Base interface for all entities.
/// Provides unique identifier for caching and equality comparison.
/// </summary>
public interface IEntity
{
    int Id { get; set; }
}
