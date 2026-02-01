using System.Collections;
using System.Collections.Concurrent;
using Model.Factory;
using Model.ViewModel;
using Microsoft.Extensions.Logging;
using Model.Entities;

namespace Model.Repository;

/// <summary>
/// In-memory repository implementation with ViewModel caching.
///
/// BLAZOR ADAPTATION NOTES:
/// - Lifetime: Scoped (per user circuit) instead of WPF's Singleton
/// - Thread safety: SemaphoreSlim still needed for async operations in Blazor
/// - Change tracking: Custom in-memory tracking instead of EF Core ChangeTracker
/// - Future: Can be replaced with EF Core version without changing consuming code
/// </summary>
public class InMemoryRepository : IRepository
{
    // In-memory storage: Type → List of entities
    private readonly ConcurrentDictionary<Type, object> _storage = new();

    // ViewModel cache: Type → Dictionary<Entity, ViewModel>
    private readonly ConcurrentDictionary<Type, object> _viewModelCaches = new();

    // Factory cache: ViewModel Type → Factory instance
    private readonly ConcurrentDictionary<Type, object> _factories = new();

    // Change tracking: Modified/Added/Deleted entities
    private readonly HashSet<object> _modifiedEntities = new();
    private readonly HashSet<object> _addedEntities = new();
    private readonly HashSet<object> _deletedEntities = new();

    // Async safety: One operation at a time (critical for Blazor async workflows)
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly ILogger<InMemoryRepository>? _logger;

    public bool AllowSaveWithErrors { get; set; }

    public InMemoryRepository(ILogger<InMemoryRepository>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates the in-memory storage for a given entity type.
    /// </summary>
    private List<TEntity> GetStorage<TEntity>() where TEntity : class, IEntity
    {
        return (List<TEntity>)_storage.GetOrAdd(
            typeof(TEntity),
            _ => new List<TEntity>()
        );
    }

    /// <summary>
    /// Gets or creates the ViewModel cache for a given entity type.
    /// Uses EntityEqualityComparer to handle unpersisted entities (Id=0).
    /// </summary>
    private ConcurrentDictionary<TEntity, TViewModel> GetViewModelCache<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        return (ConcurrentDictionary<TEntity, TViewModel>)_viewModelCaches.GetOrAdd(
            typeof(TEntity),
            _ => new ConcurrentDictionary<TEntity, TViewModel>(
                new EntityEqualityComparer<TEntity>()
            )
        );
    }

    /// <summary>
    /// Gets or creates a ViewModel for the given entity.
    /// BLAZOR NOTE: ViewModels are cached per circuit, ensuring one instance per entity per user.
    /// </summary>
    public TViewModel GetViewModel<TEntity, TViewModel>(TEntity entity)
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        if (entity == null)
        {
            _logger?.LogWarning("GetViewModel called with null entity");
            return null!;
        }

        var cache = GetViewModelCache<TEntity, TViewModel>();

        return cache.GetOrAdd(entity, e =>
        {
            var factory = GetOrCreateFactory<TEntity, TViewModel>();
            var viewModel = factory.Create(e, this);
            _logger?.LogDebug("Created new ViewModel for {EntityType} with Id={Id}",
                typeof(TEntity).Name, e.Id);
            return viewModel;
        });
    }

    /// <summary>
    /// Loads all entities and returns their ViewModels.
    /// BLAZOR NOTE: Async-safe with SemaphoreSlim.
    /// </summary>
    public IEnumerable<TViewModel> GetAllViewModels<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        _semaphore.Wait();
        try
        {
            var storage = GetStorage<TEntity>();
            return storage
                .Where(e => !_deletedEntities.Contains(e))
                .Select(e => GetViewModel<TEntity, TViewModel>(e))
                .ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Creates a new entity and returns its ViewModel.
    /// BLAZOR NOTE: Entity not persisted until SaveAll() is called.
    /// </summary>
    public TViewModel GetNewViewModel<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        _semaphore.Wait();
        try
        {
            var entity = Activator.CreateInstance<TEntity>();
            entity.Id = 0; // Not persisted yet

            var storage = GetStorage<TEntity>();
            storage.Add(entity);
            _addedEntities.Add(entity);

            _logger?.LogDebug("Created new {EntityType} entity", typeof(TEntity).Name);

            return GetViewModel<TEntity, TViewModel>(entity);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Validates all ViewModels and persists changes if no errors exist.
    /// BLAZOR NOTE: This is where FluentValidation errors are collected.
    /// Returns null on success, or list of ValidationError on failure.
    /// </summary>
    public List<ValidationError>? SaveAll()
    {
        var allErrors = new List<ValidationError>();

        _semaphore.Wait();
        try
        {
            if (!AllowSaveWithErrors)
            {
                // Collect validation errors from all ViewModels in cache
                foreach (var cache in _viewModelCaches.Values)
                {
                    var valuesProperty = cache.GetType().GetProperty("Values");
                    var viewModels = valuesProperty?.GetValue(cache) as IEnumerable;

                    if (viewModels != null)
                    {
                        foreach (var viewModel in viewModels)
                        {
                            if (viewModel is IViewModel vm)
                            {
                                allErrors.AddRange(GetErrors(vm));
                            }
                        }
                    }
                }

                if (allErrors.Any())
                {
                    _logger?.LogWarning("SaveAll blocked: {ErrorCount} validation errors found",
                        allErrors.Count);
                    return allErrors;
                }
            }

            // Assign IDs to new entities
            foreach (var entity in _addedEntities.OfType<IEntity>())
            {
                if (entity.Id == 0)
                {
                    var entityType = entity.GetType();
                    if (_storage.TryGetValue(entityType, out var storage))
                    {
                        var storageList = storage as System.Collections.IList;
                        var maxId = 0;
                        if (storageList != null)
                        {
                            foreach (var item in storageList)
                            {
                                if (item is IEntity e && e.Id > maxId)
                                {
                                    maxId = e.Id;
                                }
                            }
                        }
                        entity.Id = maxId + 1;
                    }
                }
            }

            // Clear change tracking
            _modifiedEntities.Clear();
            _addedEntities.Clear();

            // Remove deleted entities from storage
            foreach (var deleted in _deletedEntities.OfType<IEntity>())
            {
                var storageType = deleted.GetType();
                if (_storage.TryGetValue(storageType, out var storage))
                {
                    var removeMethod = storage.GetType().GetMethod("Remove");
                    removeMethod?.Invoke(storage, new[] { deleted });
                }
            }
            _deletedEntities.Clear();

            _logger?.LogInformation("SaveAll completed successfully");
            return null; // Success
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Collects validation errors from a ViewModel's FieldViewModels.
    /// </summary>
    private List<ValidationError> GetErrors(IViewModel viewModel)
    {
        var errors = new List<ValidationError>();
        var properties = viewModel.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (typeof(IFieldViewModel).IsAssignableFrom(property.PropertyType))
            {
                var field = property.GetValue(viewModel) as IFieldViewModel;
                if (field != null &&
                    !string.IsNullOrEmpty(field.Error) &&
                    field.HasSetValueFunction)
                {
                    errors.Add(new ValidationError
                    {
                        ViewModel = viewModel,
                        PropertyName = field.ToString() ?? property.Name,
                        ErrorMessage = field.Error
                    });
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks if any changes exist (added/modified/deleted).
    /// </summary>
    public bool HasChanges()
    {
        _semaphore.Wait();
        try
        {
            return _modifiedEntities.Any() ||
                   _addedEntities.Any() ||
                   _deletedEntities.Any();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Discards all pending changes.
    /// BLAZOR NOTE: In-memory version is simpler than EF Core's Reload().
    /// </summary>
    public void DiscardChanges()
    {
        _semaphore.Wait();
        try
        {
            // Remove added entities from storage
            foreach (var added in _addedEntities.OfType<IEntity>())
            {
                var storageType = added.GetType();
                if (_storage.TryGetValue(storageType, out var storage))
                {
                    var removeMethod = storage.GetType().GetMethod("Remove");
                    removeMethod?.Invoke(storage, new[] { added });
                }
            }

            // Clear change tracking
            _modifiedEntities.Clear();
            _addedEntities.Clear();
            _deletedEntities.Clear();

            _logger?.LogInformation("DiscardChanges completed");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Marks an entity for deletion.
    /// BLAZOR NOTE: Not actually removed until SaveAll() is called.
    /// </summary>
    public void DeleteEntity<TEntity>(TEntity entity) where TEntity : class, IEntity
    {
        _semaphore.Wait();
        try
        {
            _deletedEntities.Add(entity);
            _addedEntities.Remove(entity);
            _modifiedEntities.Remove(entity);

            // Remove from ViewModel cache
            if (_viewModelCaches.TryGetValue(typeof(TEntity), out var cache))
            {
                var tryRemoveMethod = cache.GetType().GetMethod("TryRemove",
                    new[] { typeof(TEntity), typeof(object).MakeByRefType() });
                if (tryRemoveMethod != null)
                {
                    var parameters = new object?[] { entity, null };
                    tryRemoveMethod.Invoke(cache, parameters);
                }
            }

            _logger?.LogDebug("Marked {EntityType} with Id={Id} for deletion",
                typeof(TEntity).Name, entity.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets or creates a Factory for the given ViewModel type.
    /// Uses reflection to find {EntityName}ViewModelFactory by convention.
    /// </summary>
    private IEntityViewModelFactory<TEntity, TViewModel> GetOrCreateFactory<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        var key = typeof(TViewModel);
        return (IEntityViewModelFactory<TEntity, TViewModel>)_factories.GetOrAdd(key, _ =>
        {
            var entityType = typeof(TEntity);
            var vmType = typeof(TViewModel);

            // Convention: {EntityName}ViewModelFactory in ViewModel namespace
            var factoryTypeName = $"{vmType.Namespace}.{entityType.Name}ViewModelFactory";

            var factoryType = Type.GetType(factoryTypeName);
            if (factoryType == null)
            {
                // Try searching in all loaded assemblies
                factoryType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == factoryTypeName);
            }

            if (factoryType == null)
            {
                var errorMsg = $"Factory not found: {factoryTypeName}. " +
                    $"Create a class implementing IEntityViewModelFactory<{entityType.Name}, {vmType.Name}>";
                _logger?.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            _logger?.LogDebug("Created factory: {FactoryType}", factoryType.Name);
            return Activator.CreateInstance(factoryType)!;
        });
    }

    /// <summary>
    /// Tracks entity as modified (for change detection).
    /// Called by ViewModels when properties change.
    /// </summary>
    public void MarkAsModified<TEntity>(TEntity entity) where TEntity : class, IEntity
    {
        if (!_addedEntities.Contains(entity))
        {
            _modifiedEntities.Add(entity);
        }
    }
}
