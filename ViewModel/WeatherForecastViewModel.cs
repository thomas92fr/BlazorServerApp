using Model.UnitOfWork;
using Microsoft.Extensions.Logging;
using Model;
using Model.Services;
using ViewModel.Commons.Bases;
using ViewModel.Commons.Fields;

namespace ViewModel;

/// <summary>
/// Weather Forecast ViewModel using Repository pattern.
///
/// BLAZOR MIGRATION NOTES:
/// - Now inherits from BaseViewModel
/// - Uses Repository for entity caching (when EF Core added)
/// - Demonstrates loading data into Repository
/// - IsBusy property from BaseViewModel for loading indicators
/// - Commands now use AsyncCommandViewModel pattern for consistent UI binding
/// </summary>
public partial class WeatherForecastViewModel : BaseViewModel
{
    private readonly IWeatherForecastService _forecastService;
    private List<WeatherForecast>? _forecasts;

    /// <summary>
    /// Command to load weather forecasts from service.
    /// </summary>
    public CommandViewModel LoadForecastsCommand { get; }

    public WeatherForecastViewModel(
        IUnitOfWork unitOfWork,
        IWeatherForecastService forecastService,
        ILogger<WeatherForecastViewModel>? logger = null
    ) : base(unitOfWork, logger)
    {
        _forecastService = forecastService;

        LoadForecastsCommand = new CommandViewModel(
            parent: this,
            text: "Load Forecasts",
            hint: "Fetch weather forecasts from the service",
            executeAsync: LoadForecasts,
            style: CommandStyle.Primary
        );
    }

    /// <summary>
    /// All loaded weather forecasts.
    /// BLAZOR BINDING: @foreach (var forecast in ViewModel.Forecasts)
    /// </summary>
    public IEnumerable<WeatherForecast>? Forecasts => _forecasts;

    /// <summary>
    /// Loads weather forecasts from service.
    /// </summary>
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
