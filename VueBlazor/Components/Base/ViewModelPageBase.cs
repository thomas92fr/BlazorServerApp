using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace VueBlazor.Components.Base;

/// <summary>
/// Base class for routable pages that use injected ViewModels.
/// Automatically refreshes UI when ViewModel properties change.
///
/// BLAZOR KEY PATTERN:
/// Unlike child components that receive ViewModels as [Parameter],
/// routable pages need [Inject] for dependency injection.
/// This base class combines injection with PropertyChanged subscription.
///
/// USAGE:
/// @inherits ViewModelPageBase<PersonListViewModel>
/// (No need for @inject - this base class handles it)
/// </summary>
/// <typeparam name="TViewModel">ViewModel type implementing INotifyPropertyChanged</typeparam>
public abstract class ViewModelPageBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : class, INotifyPropertyChanged
{
    /// <summary>
    /// ViewModel injected via DI.
    /// Protected so derived pages can access it.
    /// </summary>
    [Inject]
    protected TViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// Subscribe to ViewModel's PropertyChanged events.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Called when ViewModel property changes.
    /// BLAZOR NOTE: InvokeAsync ensures StateHasChanged runs on UI thread.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Unsubscribe from ViewModel events to prevent memory leaks.
    /// </summary>
    public virtual void Dispose()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
