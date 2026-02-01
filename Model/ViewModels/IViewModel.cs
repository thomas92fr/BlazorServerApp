using Model.Repositories;

namespace Model.ViewModels;

/// <summary>
/// Base interface for all ViewModels.
/// Provides access to Repository for data operations.
/// </summary>
public interface IViewModel
{
    IRepository Repository { get; }
}
