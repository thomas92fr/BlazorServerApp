using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Model.Data;
using Model.Services;
using Model.UnitOfWork;

namespace Model;

/// <summary>
/// Extension methods for configuring Model layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Model layer services to the dependency injection container.
    /// Configures Entity Framework Core with SQLite, UnitOfWork, and Services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string (e.g., "Data Source=App.db").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModel(this IServiceCollection services, string connectionString)
    {
        // Configure Entity Framework Core with SQLite
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.UseLazyLoadingProxies();
        });

        // UnitOfWork is Scoped (per user circuit in Blazor)
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        // Register services
        services.AddSingleton<IWeatherForecastService, WeatherForecastService>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations to the database.
    /// Call this during application startup.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
