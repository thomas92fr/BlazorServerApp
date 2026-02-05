using System.Collections;
using System.Text.Json;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.Model.ViewModels;
using BlazorServerApp.ViewModel.Commons.Bases;
using BlazorServerApp.ViewModel.Commons.Fields;

namespace BlazorServerApp.ViewMCP.Discovery;

/// <summary>
/// Service that discovers RootViewModels and their CollectionFieldViewModels at startup.
/// Used to auto-generate MCP tools for each collection.
/// </summary>
public class ViewModelDiscoveryService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly List<CollectionMetadata> _collections;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ViewModelDiscoveryService(IUnitOfWorkFactory unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _collections = DiscoverCollections();
    }

    /// <summary>
    /// All discovered collections.
    /// </summary>
    public IReadOnlyList<CollectionMetadata> Collections => _collections;

    /// <summary>
    /// Gets all items for a collection, serialized as JSON.
    /// </summary>
    public string GetAllAsJson(CollectionMetadata metadata)
    {
        using var unitOfWork = _unitOfWorkFactory.Create();

        // Create RootViewModel instance via reflection
        // Pass null for optional ILogger parameter
        var rootVm = Activator.CreateInstance(metadata.RootViewModelType, unitOfWork, null);

        // Call Initialize() if it exists
        var initMethod = metadata.RootViewModelType.GetMethod("Initialize");
        initMethod?.Invoke(rootVm, null);

        // Get collection property
        var collectionProp = metadata.RootViewModelType.GetProperty(metadata.CollectionPropertyName);
        var collectionField = collectionProp!.GetValue(rootVm);

        // Access Collection via reflection
        var collectionProperty = collectionField!.GetType().GetProperty("Collection");
        var collection = collectionProperty!.GetValue(collectionField) as IEnumerable;

        // Serialize each item
        var items = new List<Dictionary<string, object?>>();
        foreach (var item in collection!)
        {
            items.Add(SerializeViewModel(item, metadata.Fields));
        }

        // Dispose the RootViewModel if it implements IDisposable
        if (rootVm is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return JsonSerializer.Serialize(
            new { items, count = items.Count },
            JsonOptions
        );
    }

    private List<CollectionMetadata> DiscoverCollections()
    {
        var result = new List<CollectionMetadata>();
        var viewModelAssembly = typeof(RootViewModel).Assembly;

        // Find all classes inheriting from RootViewModel
        var rootVmTypes = viewModelAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RootViewModel)));

        foreach (var rootVmType in rootVmTypes)
        {
            // Find CollectionFieldViewModel<T> properties
            var collectionProps = rootVmType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(CollectionFieldViewModel<>));

            foreach (var collProp in collectionProps)
            {
                var itemVmType = collProp.PropertyType.GetGenericArguments()[0];
                var itemName = GetItemName(itemVmType);

                result.Add(new CollectionMetadata(
                    Name: itemName,
                    PluralName: collProp.Name,
                    RootViewModelType: rootVmType,
                    CollectionPropertyName: collProp.Name,
                    ItemViewModelType: itemVmType,
                    Fields: DiscoverFields(itemVmType)
                ));
            }
        }

        return result;
    }

    private static string GetItemName(Type itemVmType)
    {
        // PersonViewModel → Person
        var name = itemVmType.Name;
        return name.EndsWith("ViewModel") ? name[..^9] : name;
    }

    private static List<FieldMetadata> DiscoverFields(Type itemVmType)
    {
        // Discover IFieldViewModel properties
        return itemVmType.GetProperties()
            .Where(p => typeof(IFieldViewModel).IsAssignableFrom(p.PropertyType))
            .Select(p => new FieldMetadata(
                Name: p.Name,
                Label: p.Name,
                TypeName: GetFieldTypeName(p.PropertyType),
                ReadOnly: false,
                ColumnOrder: 0
            ))
            .ToList();
    }

    private static string GetFieldTypeName(Type fieldType)
    {
        var name = fieldType.Name;
        if (name.Contains("String")) return "String";
        if (name.Contains("Integer")) return "Integer";
        if (name.Contains("Decimal")) return "Decimal";
        if (name.Contains("Bool")) return "Boolean";
        if (name.Contains("DateTime")) return "DateTime";
        if (name.Contains("TimeSpan")) return "TimeSpan";
        if (name.Contains("Reference")) return "Reference";
        if (name.Contains("Collection")) return "Collection";
        return "Unknown";
    }

    private Dictionary<string, object?> SerializeViewModel(object vm, IReadOnlyList<FieldMetadata> fields)
    {
        var result = new Dictionary<string, object?>();
        var vmType = vm.GetType();

        foreach (var field in fields)
        {
            var prop = vmType.GetProperty(field.Name);
            if (prop?.GetValue(vm) is IFieldViewModel fieldVm)
            {
                var rawValue = fieldVm.GetRawValue();
                result[ToCamelCase(field.Name)] = FormatValue(rawValue);
            }
        }

        return result;
    }

    private static object? FormatValue(object? rawValue) => rawValue switch
    {
        DateTime dt => dt.ToString("O"),
        TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
        _ => rawValue
    };

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
