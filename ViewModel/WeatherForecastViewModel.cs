using CommunityToolkit.Mvvm.Input;
using Model.Repositories;
using Microsoft.Extensions.Logging;
using Model;
using Model.Services;
using ViewModel.Commons.Bases;

namespace ViewModel;

/// <summary>
/// Weather Forecast ViewModel using Repository pattern.
///
/// BLAZOR MIGRATION NOTES:
/// - Now inherits from BaseViewModel
/// - Uses Repository for entity caching (when EF Core added)
/// - Demonstrates loading data into Repository
/// - IsBusy property from BaseViewModel for loading indicators
/// </summary>
public partial class WeatherForecastViewModel : BaseViewModel
{
    private readonly IWeatherForecastService _forecastService;
    private List<WeatherForecast>? _forecasts;

    public WeatherForecastViewModel(
        IRepository repository,
        IWeatherForecastService forecastService,
        ILogger<WeatherForecastViewModel>? logger = null
    ) : base(repository, logger)
    {
        _forecastService = forecastService;
    }

    /// <summary>
    /// All loaded weather forecasts.
    /// BLAZOR BINDING: @foreach (var forecast in ViewModel.Forecasts)
    /// </summary>
    public IEnumerable<WeatherForecast>? Forecasts => _forecasts;

    /// <summary>
    /// Loads weather forecasts from service.
    /// BLAZOR LIFECYCLE: Call in OnInitializedAsync()
    /// </summary>
    [RelayCommand]
    private async Task LoadForecasts()
    {
        await ExecuteAsync(async () =>
        {
            Log?.LogInformation("Loading weather forecasts...");
            var forecasts = await _forecastService.GetForecastAsync(DateTime.Now);

            _forecasts = forecasts.ToList();

            // Assign IDs for entity tracking
            int id = 1;
            foreach (var forecast in _forecasts)
            {
                forecast.Id = id++;
            }

            OnPropertyChanged(nameof(Forecasts));
            Log?.LogInformation("Loaded {Count} forecasts", _forecasts.Count);
        });
    }
}
