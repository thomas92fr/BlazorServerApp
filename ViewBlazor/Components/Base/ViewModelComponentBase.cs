using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace ViewBlazor.Components.Base;

/// <summary>
/// Base component that automatically calls StateHasChanged when ViewModel properties change.
///
/// BLAZOR KEY PATTERN:
/// WPF uses DataBinding which automatically updates UI on INotifyPropertyChanged.
/// Blazor requires explicit StateHasChanged() calls.
/// This base class bridges the gap by subscribing to PropertyChanged events.
///
/// USAGE:
/// @inherits ViewModelComponentBase<PersonViewModel>
/// @code {
///     [Parameter] public PersonViewModel ViewModel { get; set; }
/// }
/// </summary>
/// <typeparam name="TViewModel">ViewModel type implementing INotifyPropertyChanged</typeparam>
public abstract class ViewModelComponentBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : INotifyPropertyChanged
{
    private TViewModel? _viewModel;

    [Parameter]
    public TViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (!ReferenceEquals(_viewModel, value))
            {
                // Unsubscribe from old ViewModel
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _viewModel = value;

                // Subscribe to new ViewModel
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                }
            }
        }
    }

    /// <summary>
    /// Called when ViewModel property changes.
    /// BLAZOR NOTE: InvokeAsync ensures StateHasChanged runs on UI thread.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
