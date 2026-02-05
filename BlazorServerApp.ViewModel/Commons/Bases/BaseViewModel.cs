using CommunityToolkit.Mvvm.ComponentModel;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.ViewModels;
using Microsoft.Extensions.Logging;

namespace BlazorServerApp.ViewModel.Commons.Bases;

/// <summary>
/// Base class for all ViewModels.
/// Combines CommunityToolkit.Mvvm with UnitOfWork pattern.
///
/// - Uses ObservableObject for INotifyPropertyChanged
/// - Supports both IRootViewModel (new) and IUnitOfWork (legacy) patterns
/// - IsBusy property for async operation indicators
/// </summary>
public partial class BaseViewModel : ObservableObject, IViewModel
{
    private readonly IRootViewModel? _rootViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BaseViewModel>? _logger;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// New constructor: receives IRootViewModel for full tab context access.
    /// </summary>
    public BaseViewModel(IRootViewModel rootViewModel, ILogger<BaseViewModel>? logger = null)
    {
        _rootViewModel = rootViewModel;
        _unitOfWork = rootViewModel.UnitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Legacy constructor: receives IUnitOfWork directly.
    /// </summary>
    public BaseViewModel(IUnitOfWork unitOfWork, ILogger<BaseViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The root ViewModel for this tab (null for legacy ViewModels).
    /// </summary>
    public IRootViewModel? RootViewModel => _rootViewModel;

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
