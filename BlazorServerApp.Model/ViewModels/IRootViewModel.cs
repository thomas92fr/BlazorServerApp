using BlazorServerApp.Model.UnitOfWork;

namespace BlazorServerApp.Model.ViewModels;

/// <summary>
/// Interface for root ViewModels that own a UnitOfWork.
/// Each tab in the UI should have its own IRootViewModel instance,
/// providing isolated data context and common commands (Save, Discard).
/// </summary>
public interface IRootViewModel : IViewModel, IDisposable
{
    /// <summary>
    /// Unique identifier for this root ViewModel instance.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display title for the tab.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Indicates whether there are unsaved changes.
    /// </summary>
    bool HasChanges { get; }

    /// <summary>
    /// Saves all pending changes.
    /// Returns null on success, or validation errors.
    /// </summary>
    List<ValidationError>? Save();

    /// <summary>
    /// Discards all pending changes.
    /// </summary>
    void Discard();

    /// <summary>
    /// Callback executed after Save completes successfully.
    /// </summary>
    Action? OnSaved { get; set; }

    /// <summary>
    /// Callback executed after Discard completes.
    /// </summary>
    Action? OnDiscarded { get; set; }
}
