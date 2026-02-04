using Microsoft.Extensions.DependencyInjection;
using Model;

namespace ViewModel;

/// <summary>
/// Extension methods for configuring ViewModel layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds ViewModel layer services and its dependencies (Model layer) to the DI container.
    /// All ViewModels are registered as Scoped (shared per circuit in Blazor).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string for the Model layer.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddViewModels(this IServiceCollection services, string connectionString)
    {
        // Add Model layer dependencies first
        services.AddModel(connectionString);

        // Register ViewModels as Scoped (shared per circuit, not per component)
        services.AddScoped<CounterViewModel>();
        services.AddScoped<WeatherForecastViewModel>();
        services.AddScoped<PersonListViewModel>();

        return services;
    }
}
