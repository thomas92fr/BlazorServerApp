using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.ViewModels;
using ViewModel.Commons.Bases;

namespace ViewModel;

/// <summary>
/// Main window ViewModel that manages the collection of open tabs.
/// Each tab is a RootViewModel with its own isolated UnitOfWork.
/// Registered as Scoped (per Blazor circuit).
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ILogger<MainWindowViewModel>? _logger;

    /// <summary>
    /// Collection of open tabs.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<IRootViewModel> _tabs = new();

    /// <summary>
    /// Currently active tab (null if Home is active).
    /// </summary>
    [ObservableProperty]
    private IRootViewModel? _activeTab;

    /// <summary>
    /// Indicates whether the Home view is active.
    /// </summary>
    [ObservableProperty]
    private bool _isHomeActive = true;

    public MainWindowViewModel(
        IUnitOfWorkFactory unitOfWorkFactory,
        ILogger<MainWindowViewModel>? logger = null)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _logger = logger;
        _logger?.LogDebug("MainWindowViewModel created");
    }

    /// <summary>
    /// Creates a new tab of the specified RootViewModel type.
    /// </summary>
    /// <typeparam name="T">The RootViewModel type (e.g., PersonListViewModel)</typeparam>
    /// <returns>The created tab instance</returns>
    public T CreateTab<T>() where T : RootViewModel
    {
        var unitOfWork = _unitOfWorkFactory.Create();

        // Create ViewModel using reflection (constructor: IUnitOfWork, ILogger?)
        var viewModel = (T)Activator.CreateInstance(typeof(T), unitOfWork, null)!;

        // Initialize if the ViewModel has Initialize method
        var initMethod = typeof(T).GetMethod("Initialize");
        initMethod?.Invoke(viewModel, null);

        Tabs.Add(viewModel);
        ActiveTab = viewModel;
        IsHomeActive = false;

        _logger?.LogInformation("Created tab: {Title} (Id: {Id})", viewModel.Title, viewModel.Id);
        return viewModel;
    }

    /// <summary>
    /// Activates the specified tab.
    /// </summary>
    public void ActivateTab(IRootViewModel tab)
    {
        if (!Tabs.Contains(tab)) return;

        ActiveTab = tab;
        IsHomeActive = false;

        _logger?.LogDebug("Activated tab: {Title}", tab.Title);
    }

    /// <summary>
    /// Activates the Home view.
    /// </summary>
    public void ActivateHome()
    {
        ActiveTab = null;
        IsHomeActive = true;

        _logger?.LogDebug("Activated Home");
    }

    /// <summary>
    /// Closes the specified tab.
    /// </summary>
    /// <param name="tab">The tab to close</param>
    /// <param name="force">If true, closes even if there are unsaved changes</param>
    /// <returns>True if the tab was closed, false if cancelled (unsaved changes)</returns>
    public bool CloseTab(IRootViewModel tab, bool force = false)
    {
        if (!Tabs.Contains(tab)) return false;

        // Check for unsaved changes
        if (tab.HasChanges && !force)
        {
            // Return false - caller should show confirmation dialog
            _logger?.LogDebug("Close cancelled: tab {Title} has unsaved changes", tab.Title);
            return false;
        }

        var index = Tabs.IndexOf(tab);
        var wasActive = ActiveTab == tab;

        Tabs.Remove(tab);

        _logger?.LogInformation("Closed tab: {Title} (Id: {Id})", tab.Title, tab.Id);

        // Activate adjacent tab or home
        if (wasActive)
        {
            if (Tabs.Count > 0)
            {
                // Activate the tab to the left, or the first tab
                var newIndex = Math.Max(0, index - 1);
                ActiveTab = Tabs[newIndex];
                IsHomeActive = false;
            }
            else
            {
                ActiveTab = null;
                IsHomeActive = true;
            }
        }

        return true;
    }

    /// <summary>
    /// Closes all tabs.
    /// </summary>
    /// <param name="force">If true, closes even if there are unsaved changes</param>
    /// <returns>True if all tabs were closed</returns>
    public bool CloseAllTabs(bool force = false)
    {
        if (!force && Tabs.Any(t => t.HasChanges))
        {
            return false;
        }

        foreach (var tab in Tabs.ToList())
        {
            tab.Dispose();
        }

        Tabs.Clear();
        ActiveTab = null;
        IsHomeActive = true;

        _logger?.LogInformation("Closed all tabs");
        return true;
    }

    /// <summary>
    /// Checks if any tab has unsaved changes.
    /// </summary>
    public bool HasAnyUnsavedChanges => Tabs.Any(t => t.HasChanges);

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var tab in Tabs)
            {
                tab.Dispose();
            }
            Tabs.Clear();
            _disposed = true;
            _logger?.LogDebug("MainWindowViewModel disposed");
        }
    }

    #endregion
}
