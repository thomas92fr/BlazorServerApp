using System.Collections;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using BlazorServerApp.Model.Data;
using BlazorServerApp.Model.Entities;
using BlazorServerApp.Model.Factories;
using BlazorServerApp.Model.Repositories;
using BlazorServerApp.Model.ViewModels;

namespace BlazorServerApp.Model.UnitOfWork;

/// <summary>
/// Unit of Work implementation with EF Core.
/// Combines DbContext, repositories, and ViewModel caching.
///
/// BLAZOR ADAPTATION NOTES:
/// - Lifetime: Scoped (per user circuit)
/// - Thread safety: SemaphoreSlim for async operations
/// - Change tracking: Uses EF Core's ChangeTracker
/// - ViewModel cache: Caches ViewModels per entity
/// - RootViewModel: Optional reference for tab-based architecture
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UnitOfWork>? _logger;

    // Generic repositories cache: Type -> Repository instance
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    // ViewModel cache: Entity Type -> Dictionary<Entity, ViewModel>
    private readonly ConcurrentDictionary<Type, object> _viewModelCaches = new();

    // Factory cache: ViewModel Type -> Factory instance
    private readonly ConcurrentDictionary<Type, object> _factories = new();

    // Async safety: One operation at a time
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Transaction management
    private IDbContextTransaction? _transaction;

    // Optional reference to the root ViewModel (for tab-based architecture)
    private IRootViewModel? _rootViewModel;

    public bool AllowSaveWithErrors { get; set; }
    private bool _disposed;

    public UnitOfWork(ApplicationDbContext context, ILogger<UnitOfWork>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    #region IUnitOfWork - Repository Access

    /// <summary>
    /// Gets or creates a generic repository for the specified entity type.
    /// </summary>
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IEntity
    {
        var type = typeof(TEntity);
        return (IGenericRepository<TEntity>)_repositories.GetOrAdd(type,
            _ => new GenericRepository<TEntity>(_context));
    }

    #endregion

    #region ViewModel Caching

    /// <summary>
    /// Associates this UnitOfWork with a RootViewModel.
    /// Called by RootViewModel constructor to enable IRootViewModel injection into entity ViewModels.
    /// </summary>
    public void SetRootViewModel(IRootViewModel rootViewModel)
    {
        _rootViewModel = rootViewModel;
        _logger?.LogDebug("UnitOfWork associated with RootViewModel {Id}", rootViewModel.Id);
    }

    /// <summary>
    /// Gets the ViewModel cache for a given entity type.
    /// Uses EntityEqualityComparer for proper handling of new entities (Id=0).
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
    /// If a RootViewModel is set, uses it for factory creation (preferred).
    /// Otherwise falls back to passing this UnitOfWork directly (legacy).
    /// </summary>
    public TViewModel? GetViewModel<TEntity, TViewModel>(TEntity? entity)
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        if (entity == null) return null;

        var cache = GetViewModelCache<TEntity, TViewModel>();
        return cache.GetOrAdd(entity, e =>
        {
            var factory = GetOrCreateFactory<TEntity, TViewModel>();

            // Use IRootViewModel if available, otherwise fall back to IUnitOfWork
            var viewModel = _rootViewModel != null
                ? factory.Create(e, _rootViewModel)
                : factory.Create(e, this);

            _logger?.LogDebug("Created new ViewModel for {EntityType} with Id={Id}",
                typeof(TEntity).Name, e.Id);
            return viewModel;
        });
    }

    /// <summary>
    /// Loads all entities from database and returns their ViewModels.
    /// </summary>
    public IEnumerable<TViewModel> GetAllViewModels<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        _semaphore.Wait();
        try
        {
            var entities = _context.Set<TEntity>().ToList()
                .Where(e => !e.Deleted).ToList();
            return entities.Select(e => GetViewModel<TEntity, TViewModel>(e)!).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Creates a new entity and returns its ViewModel.
    /// Entity is added to EF Core's change tracker but not saved until SaveChanges.
    /// </summary>
    public TViewModel GetNewViewModel<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        _semaphore.Wait();
        try
        {
            var entity = Activator.CreateInstance<TEntity>();
            entity.Id = 0; // New entity marker

            _context.Set<TEntity>().Add(entity);
            _logger?.LogDebug("Created new {EntityType} entity", typeof(TEntity).Name);

            return GetViewModel<TEntity, TViewModel>(entity)!;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Soft-deletes an entity by setting Deleted = true.
    /// New entities (Added state) are detached entirely and never persisted.
    /// Existing entities are marked as Modified to persist Deleted = true on SaveChanges.
    /// </summary>
    public void DeleteEntity<TEntity>(TEntity entity) where TEntity : class, IEntity
    {
        _semaphore.Wait();
        try
        {
            entity.Deleted = true;

            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Added)
            {
                // New entity never persisted → detach completely, no INSERT
                entry.State = EntityState.Detached;
            }
            else
            {
                // Existing entity → mark Modified to persist Deleted = true
                if (entry.State == EntityState.Detached)
                {
                    _context.Set<TEntity>().Attach(entity);
                }
                entry.State = EntityState.Modified;
            }

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

            _logger?.LogDebug("Soft-deleted {EntityType} with Id={Id} (State={State})",
                typeof(TEntity).Name, entity.Id, entry.State);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Marks an entity as modified in EF Core's change tracker.
    /// </summary>
    public void MarkAsModified<TEntity>(TEntity entity) where TEntity : class, IEntity
    {
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }
    }

    #endregion

    #region Save and Change Tracking

    /// <summary>
    /// Validates all ViewModels and saves changes to database.
    /// Returns null on success, or list of ValidationError on failure.
    /// </summary>
    public List<ValidationError>? SaveAll()
    {
        return SaveAllAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async version of SaveAll with validation.
    /// </summary>
    public async Task<List<ValidationError>?> SaveChangesAsync()
    {
        return await SaveAllAsync();
    }

    private async Task<List<ValidationError>?> SaveAllAsync()
    {
        var allErrors = new List<ValidationError>();

        await _semaphore.WaitAsync();
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

            // Safety net: detach new entities marked as deleted (should not be persisted)
            foreach (var entry in _context.ChangeTracker.Entries<IEntity>().ToList())
            {
                if (entry.State == EntityState.Added && entry.Entity.Deleted)
                {
                    entry.State = EntityState.Detached;
                    _logger?.LogDebug("Detached new entity {Type} marked as deleted before save",
                        entry.Entity.GetType().Name);
                }
            }

            await _context.SaveChangesAsync();
            _logger?.LogInformation("SaveChanges completed successfully");
            return null; // Success
        }
        catch (DbUpdateException ex)
        {
            _logger?.LogError(ex, "Database update failed");
            allErrors.Add(new ValidationError
            {
                ViewModel = null!,
                PropertyName = "Database",
                ErrorMessage = ex.InnerException?.Message ?? ex.Message
            });
            return allErrors;
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
                if (field != null && !string.IsNullOrEmpty(field.Error))
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
    /// Checks if there are any pending changes in EF Core's change tracker.
    /// </summary>
    public bool HasChanges()
    {
        _semaphore.Wait();
        try
        {
            return _context.ChangeTracker.HasChanges();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Discards all pending changes by reloading entities from database.
    /// </summary>
    public void DiscardChanges()
    {
        _semaphore.Wait();
        try
        {
            // Reload all tracked entities to their original values
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        entry.Reload();
                        break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                }
            }

            // Clear ViewModel caches (they might hold stale data)
            _viewModelCaches.Clear();

            _logger?.LogInformation("DiscardChanges completed");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

    #region Transaction Support

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
        _logger?.LogDebug("Transaction started");
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
            _logger?.LogDebug("Transaction committed");
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
            _logger?.LogDebug("Transaction rolled back");
        }
    }

    #endregion

    #region Factory Discovery

    /// <summary>
    /// Gets or creates a Factory for the given ViewModel type.
    /// First looks for a custom {EntityName}ViewModelFactory by convention.
    /// Falls back to DefaultEntityViewModelFactory if no custom factory exists.
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

            if (factoryType != null)
            {
                _logger?.LogDebug("Using custom factory: {FactoryType}", factoryType.Name);
                return Activator.CreateInstance(factoryType)!;
            }

            // Fallback: use generic default factory
            _logger?.LogDebug("Using DefaultEntityViewModelFactory for {ViewModelType}", vmType.Name);
            return new DefaultEntityViewModelFactory<TEntity, TViewModel>();
        });
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
            _semaphore.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
            await _context.DisposeAsync();
            _semaphore.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}
