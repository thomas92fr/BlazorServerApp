using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(DependencyInjection).Assembly);

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
