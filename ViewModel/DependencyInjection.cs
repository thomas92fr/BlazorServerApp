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
    /// RootViewModels (tabs) are created via IUnitOfWorkFactory, not registered in DI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string for the Model layer.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddViewModels(this IServiceCollection services, string connectionString)
    {
        // Add Model layer dependencies (includes IUnitOfWorkFactory)
        services.AddModel(connectionString);

        // MainWindowViewModel manages tabs - scoped to Blazor circuit
        services.AddScoped<MainWindowViewModel>();

        // RootViewModels (like PersonListViewModel) are created via MainWindowViewModel.CreateTab<T>()
        // when opening a new tab, not registered in DI

        return services;
    }
}
