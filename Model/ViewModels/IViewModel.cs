using Model.UnitOfWork;

namespace Model.ViewModels;

/// <summary>
/// Base interface for all ViewModels.
/// Provides access to UnitOfWork for data operations.
/// </summary>
public interface IViewModel
{
    IUnitOfWork UnitOfWork { get; }
}
