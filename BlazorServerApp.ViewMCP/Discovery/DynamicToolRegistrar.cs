using ModelContextProtocol.Server;

namespace BlazorServerApp.ViewMCP.Discovery;

/// <summary>
/// Registers MCP tools dynamically for each discovered collection.
/// </summary>
public class DynamicToolRegistrar
{
    private readonly ViewModelDiscoveryService _discoveryService;

    public DynamicToolRegistrar(ViewModelDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Creates MCP tools for all discovered collections.
    /// </summary>
    public IEnumerable<McpServerTool> CreateTools()
    {
        foreach (var collection in _discoveryService.Collections)
        {
            yield return CreateGetAllTool(collection);
        }
    }

    private McpServerTool CreateGetAllTool(CollectionMetadata collection)
    {
        // Create a "GetAllPersons" tool for the Persons collection
        var fieldNames = string.Join(", ", collection.Fields.Select(f => f.Name.ToLowerInvariant()));

        // Capture collection in closure for the delegate
        var metadata = collection;

        return McpServerTool.Create(
            method: () => _discoveryService.GetAllAsJson(metadata),
            options: new McpServerToolCreateOptions
            {
                Name = $"GetAll{collection.PluralName}",
                Description = $"Gets all {collection.PluralName.ToLowerInvariant()} from the database. " +
                              $"Returns JSON array with fields: {fieldNames}."
            }
        );
    }
}
