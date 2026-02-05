namespace BlazorServerApp.ViewMCP.Discovery;

/// <summary>
/// Metadata about a discovered CollectionFieldViewModel.
/// </summary>
public record CollectionMetadata(
    string Name,                           // "Person" (singular, derived from ItemViewModelType)
    string PluralName,                     // "Persons" (collection property name)
    Type RootViewModelType,                // typeof(PersonListViewModel)
    string CollectionPropertyName,         // "Persons"
    Type ItemViewModelType,                // typeof(PersonViewModel)
    IReadOnlyList<FieldMetadata> Fields
);

/// <summary>
/// Metadata about a field in a ViewModel.
/// </summary>
public record FieldMetadata(
    string Name,
    string? Label,
    string TypeName,
    bool ReadOnly,
    int ColumnOrder
);
