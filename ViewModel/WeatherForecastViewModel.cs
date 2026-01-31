using CommunityToolkit.Mvvm.ComponentModel;
using Model;
using Model.Services;

namespace ViewModel
{
    public partial class WeatherForecastViewModel : ObservableObject
    {
        private readonly IWeatherForecastService _forecastService;

        [ObservableProperty]
        private WeatherForecast[]? forecasts;

        [ObservableProperty]
        private bool isLoading;

        public WeatherForecastViewModel(IWeatherForecastService forecastService)
        {
            _forecastService = forecastService;
        }

        public async Task LoadForecastsAsync()
        {
            IsLoading = true;
            Forecasts = await _forecastService.GetForecastAsync(DateTime.Now);
            IsLoading = false;
        }
    }
}
