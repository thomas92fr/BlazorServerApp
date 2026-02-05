using BlazorServerApp.ViewMCP.Discovery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BlazorServerApp.ViewMCP;

/// <summary>
/// Extension methods for configuring MCP server services and endpoints.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds MCP server services to the dependency injection container.
    /// Requires ViewModel layer to be configured first (for IUnitOfWorkFactory).
    /// </summary>
    public static IServiceCollection AddViewMcp(this IServiceCollection services)
    {
        // Register discovery services
        services.AddSingleton<ViewModelDiscoveryService>();
        services.AddSingleton<DynamicToolRegistrar>();

        // Configure MCP server options to add dynamic tools
        services.AddSingleton<IConfigureOptions<McpServerOptions>, ConfigureDynamicTools>();

        services.AddMcpServer()
            .WithHttpTransport();

        return services;
    }

    /// <summary>
    /// Maps MCP server endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapViewMcp(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp("/mcp");
        return endpoints;
    }
}

/// <summary>
/// Configures McpServerOptions to add dynamically discovered tools.
/// </summary>
internal class ConfigureDynamicTools : IConfigureOptions<McpServerOptions>
{
    private readonly DynamicToolRegistrar _registrar;

    public ConfigureDynamicTools(DynamicToolRegistrar registrar)
    {
        _registrar = registrar;
    }

    public void Configure(McpServerOptions options)
    {
        // Ensure ToolCollection is initialized
        options.ToolCollection ??= [];

        // Add dynamically discovered tools
        foreach (var tool in _registrar.CreateTools())
        {
            options.ToolCollection.Add(tool);
        }
    }
}
