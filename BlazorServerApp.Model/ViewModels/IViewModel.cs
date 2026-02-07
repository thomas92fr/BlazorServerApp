namespace BlazorServerApp.Model.ViewModels;

/// <summary>
/// Base interface for all ViewModels.
/// Provides access to the root ViewModel for tab context.
/// </summary>
public interface IViewModel
{
    /// <summary>
    /// The root ViewModel for this tab (null for legacy ViewModels or the root itself).
    /// </summary>
    IRootViewModel? RootViewModel { get; }
}
