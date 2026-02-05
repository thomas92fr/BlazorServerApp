using Model.UnitOfWork;

namespace Model.ViewModels;

/// <summary>
/// Base interface for all ViewModels.
/// Provides access to UnitOfWork for data operations.
/// </summary>
public interface IViewModel
{
    /// <summary>
    /// The UnitOfWork for data operations.
    /// </summary>
    IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// The root ViewModel for this tab (null for legacy ViewModels or the root itself).
    /// </summary>
    IRootViewModel? RootViewModel { get; }
}
