using CommunityToolkit.Mvvm.ComponentModel;
using Model.UnitOfWork;
using Model.ViewModels;
using Microsoft.Extensions.Logging;

namespace ViewModel.Commons.Bases;

/// <summary>
/// Base class for all ViewModels.
/// Combines CommunityToolkit.Mvvm with UnitOfWork pattern.
///
/// BLAZOR ADAPTATION:
/// - Still uses ObservableObject for INotifyPropertyChanged (works with Blazor via ViewModelComponentBase)
/// - UnitOfWork is Scoped per circuit (injected via constructor)
/// - IsBusy property for async operation indicators
/// </summary>
public partial class BaseViewModel : ObservableObject, IViewModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BaseViewModel>? _logger;

    [ObservableProperty]
    private bool _isBusy;

    public BaseViewModel(IUnitOfWork unitOfWork, ILogger<BaseViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public IUnitOfWork UnitOfWork => _unitOfWork;
    public ILogger<BaseViewModel>? Log => _logger;

    /// <summary>
    /// Public wrapper for OnPropertyChanged to allow notification from nested FieldViewModels.
    /// Used by FieldViewModel.NotifyOnChange to trigger recalculation of computed fields.
    /// Automatically validates FieldViewModel properties after raising PropertyChanged.
    /// </summary>
    public void RaisePropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);

        // For computed fields, trigger validation after recalculation
        var property = GetType().GetProperty(propertyName);
        if (property != null)
        {
            var value = property.GetValue(this);
            if (value is IFieldViewModel fieldViewModel)
            {
                fieldViewModel.Validate();
            }
        }
    }

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
