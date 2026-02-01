using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Repository;
using Infrastructure.ViewModel;
using Microsoft.Extensions.Logging;

namespace ViewModel.Bases;

/// <summary>
/// Base class for all ViewModels.
/// Combines CommunityToolkit.Mvvm with Repository pattern.
///
/// BLAZOR ADAPTATION:
/// - Still uses ObservableObject for INotifyPropertyChanged (works with Blazor via ViewModelComponentBase)
/// - Repository is Scoped per circuit (injected via constructor)
/// - IsBusy property for async operation indicators
/// </summary>
public partial class BaseViewModel : ObservableObject, IViewModel
{
    private readonly IRepository _repository;
    private readonly ILogger<BaseViewModel>? _logger;

    [ObservableProperty]
    private bool _isBusy;

    public BaseViewModel(IRepository repository, ILogger<BaseViewModel>? logger = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public IRepository Repository => _repository;
    public ILogger<BaseViewModel>? Log => _logger;

    /// <summary>
    /// Helper for async operations with busy indicator.
    /// BLAZOR NOTE: Use this for async commands to show loading state.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await operation();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing async operation");
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
