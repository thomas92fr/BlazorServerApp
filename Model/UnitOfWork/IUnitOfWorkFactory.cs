namespace Model.UnitOfWork;

/// <summary>
/// Factory for creating UnitOfWork instances.
/// Used to create independent UnitOfWork for each tab/RootViewModel.
/// </summary>
public interface IUnitOfWorkFactory
{
    /// <summary>
    /// Creates a new UnitOfWork instance with its own DbContext.
    /// </summary>
    IUnitOfWork Create();
}
