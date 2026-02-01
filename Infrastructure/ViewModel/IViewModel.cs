using Infrastructure.Repository;

namespace Infrastructure.ViewModel;

/// <summary>
/// Base interface for all ViewModels.
/// Provides access to Repository for data operations.
/// </summary>
public interface IViewModel
{
    IRepository Repository { get; }
}
