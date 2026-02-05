using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Model.Data;
using Model.UnitOfWork;

namespace Model;

/// <summary>
/// Extension methods for configuring Model layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Model layer services to the dependency injection container.
    /// Configures Entity Framework Core with SQLite and UnitOfWorkFactory for tab-based architecture.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string (e.g., "Data Source=App.db").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModel(this IServiceCollection services, string connectionString)
    {
        // Configure Entity Framework Core with SQLite
        // Use AddDbContextFactory for creating isolated DbContext instances (for tab-based architecture)
        services.AddDbContextFactory<ApplicationDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.UseLazyLoadingProxies();
        });

        // UnitOfWorkFactory creates isolated UnitOfWork instances for each tab/RootViewModel
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();

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
