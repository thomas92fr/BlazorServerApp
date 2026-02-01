# Implémentation MVVM - Guide de Réutilisation

## Table des Matières
1. [Introduction](#introduction)
2. [Système de Cache pour ViewModels](#système-de-cache-pour-viewmodels)
3. [FieldViewModel - Système de Propriétés Typées](#fieldviewmodel---système-de-propriétés-typées)
4. [Pattern Factory pour ViewModels](#pattern-factory-pour-viewmodels)
5. [Système de Validation](#système-de-validation)
6. [ViewModelTemplateSelector - Résolution Automatique des Vues](#viewmodeltemplateselector---résolution-automatique-des-vues)
7. [Classes de Support](#classes-de-support)
8. [Exemple Complet d'Implémentation](#exemple-complet-dimplémentation)
9. [Diagramme d'Architecture](#diagramme-darchitecture)
10. [Bonnes Pratiques et Recommandations](#bonnes-pratiques-et-recommandations)

---

## Introduction

Cette documentation décrit une implémentation avancée du pattern MVVM pour applications WPF .NET, conçue pour maximiser la réutilisabilité, la maintenabilité et la sécurité thread.

### Avantages de cette Architecture

- **Cache automatique**: Une seule instance de ViewModel par entité garantie
- **Thread-safety**: Accès concurrent sécurisé au DbContext et aux caches
- **Découverte automatique**: Résolution des Factories et Views par convention de nommage
- **Validation intégrée**: FluentValidation au niveau des champs avec sévérités Error/Warning
- **Lazy loading**: Chargement différé des valeurs et validateurs pour performance optimale
- **Séparation des concerns**: Core library sans dépendances WPF

### Prérequis et Dépendances NuGet

```xml
<!-- Core Library (.NET 8) -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.*" />
<PackageReference Include="FluentValidation" Version="11.9.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.*" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.*" />

<!-- WPF Project -->
<PackageReference Include="MaterialDesignThemes" Version="5.0.*" />
```

---

## Système de Cache pour ViewModels

### Concept Clé

Le `GenericRepository<TContext>` maintient un cache thread-safe garantissant qu'une seule instance de ViewModel existe par entité. Cela évite les problèmes de synchronisation et améliore les performances.

### Architecture du Cache

```csharp
public class GenericRepository<TContext> where TContext : DbContext
{
    private readonly TContext _context;

    // Cache: Type d'entité → Dictionary<Entité, ViewModel>
    private readonly ConcurrentDictionary<Type, object> _caches = new();

    // Cache de factories pour création de ViewModels
    private readonly ConcurrentDictionary<Type, object> _factories = new();

    // Sémaphore pour protéger l'accès concurrent au DbContext
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public GenericRepository(TContext context)
    {
        _context = context;
    }

    // Récupère ou crée le cache pour un type d'entité donné
    private ConcurrentDictionary<TEntity, TViewModel> GetCache<TEntity, TViewModel>()
        where TEntity : class, IEntity
        where TViewModel : class, IEntityViewModel<TEntity>
    {
        return (ConcurrentDictionary<TEntity, TViewModel>)_caches.GetOrAdd(
            typeof(TEntity),
            _ => new ConcurrentDictionary<TEntity, TViewModel>(
                new EntityEqualityComparer<TEntity>()
            )
        );
    }
}
```

### Méthodes Principales

#### GetViewModel - Récupération avec Cache

```csharp
public TViewModel GetViewModel<TEntity, TViewModel>(TEntity entity)
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    if (entity == null) return null;

    var cache = GetCache<TEntity, TViewModel>();

    // Retourne le ViewModel en cache ou en crée un nouveau
    return cache.GetOrAdd(entity, e =>
    {
        var factory = GetOrCreateFactory<TEntity, TViewModel>();
        return factory.Create(e, this);
    });
}
```

#### GetAllViewModels - Chargement de toutes les entités

```csharp
public IEnumerable<TViewModel> GetAllViewModels<TEntity, TViewModel>()
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    _semaphore.Wait();
    try
    {
        // Charge toutes les entités depuis la base de données
        var entities = _context.Set<TEntity>().ToList();

        // Mappe chaque entité à son ViewModel (avec cache)
        return entities.Select(e => GetViewModel<TEntity, TViewModel>(e)).ToList();
    }
    finally
    {
        _semaphore.Release();
    }
}
```

#### GetNewViewModel - Création d'une nouvelle entité

```csharp
public TViewModel GetNewViewModel<TEntity, TViewModel>()
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    _semaphore.Wait();
    try
    {
        // Crée une nouvelle instance d'entité
        var entity = Activator.CreateInstance<TEntity>();

        // L'ajoute au DbContext (non persisté tant que SaveAll() n'est pas appelé)
        _context.Set<TEntity>().Add(entity);

        // Retourne le ViewModel mappé (et le met en cache)
        return GetViewModel<TEntity, TViewModel>(entity);
    }
    finally
    {
        _semaphore.Release();
    }
}
```

#### SaveAll - Sauvegarde avec Validation

```csharp
public List<ValidationError> SaveAll()
{
    var allErrors = new List<ValidationError>();
    _semaphore.Wait();
    try
    {
        if (!AllowSaveWithErrors)
        {
            // Collecte les erreurs de tous les ViewModels en cache
            foreach (var cache in _caches.Values)
            {
                var viewModelsProperty = cache.GetType().GetProperty("Values");
                var viewModels = viewModelsProperty?.GetValue(cache) as IEnumerable;

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

            // Si des erreurs existent, retourne-les sans sauvegarder
            if (allErrors.Any())
            {
                return allErrors;
            }
        }

        // Sauvegarde toutes les modifications
        _context.SaveChanges();
        return null; // Aucune erreur
    }
    finally
    {
        _semaphore.Release();
    }
}

private List<ValidationError> GetErrors(IViewModel viewModel)
{
    var errors = new List<ValidationError>();
    var properties = viewModel.GetType().GetProperties();

    foreach (var property in properties)
    {
        if (typeof(IFieldViewModel).IsAssignableFrom(property.PropertyType))
        {
            var field = property.GetValue(viewModel) as IFieldViewModel;
            if (field != null && !string.IsNullOrEmpty(field.Error) && field.HasSetValueFunction)
            {
                errors.Add(new ValidationError
                {
                    ViewModel = viewModel,
                    PropertyName = field.ToString(),
                    ErrorMessage = field.Error
                });
            }
        }
    }
    return errors;
}
```

### EntityEqualityComparer - Gestion des Entités Non Persistées

```csharp
/// <summary>
/// Comparateur permettant de gérer les entités avec Id=0 (non encore en base)
/// en leur assignant des IDs temporaires négatifs pour le cache
/// </summary>
public class EntityEqualityComparer<TEntity> : IEqualityComparer<TEntity>
    where TEntity : IEntity
{
    private static int _tempIdCounter = -1;
    private readonly Dictionary<TEntity, int> _tempIds = new();

    public bool Equals(TEntity x, TEntity y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        int xId = GetEntityId(x);
        int yId = GetEntityId(y);
        return xId == yId;
    }

    public int GetHashCode(TEntity obj)
    {
        return obj is null ? 0 : GetEntityId(obj);
    }

    private int GetEntityId(TEntity entity)
    {
        // Si l'entité a déjà un ID en base, l'utilise
        if (entity.Id != 0) return entity.Id;

        // Sinon, assigne un ID temporaire négatif
        if (!_tempIds.TryGetValue(entity, out int tempId))
        {
            tempId = System.Threading.Interlocked.Decrement(ref _tempIdCounter);
            _tempIds[entity] = tempId;
        }
        return tempId;
    }
}
```

### Méthodes de Gestion des Changements

```csharp
// Vérifie si des modifications non sauvegardées existent
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

// Annule toutes les modifications en cours
public void DiscardChanges()
{
    _semaphore.Wait();
    try
    {
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Unchanged)
            .ToList();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.Reload();
                    break;
            }
        }
    }
    finally
    {
        _semaphore.Release();
    }
}

// Supprime une entité (la retire du cache et du DbContext)
public void DeleteEntity<TEntity>(TEntity entity)
    where TEntity : class, IEntity
{
    _semaphore.Wait();
    try
    {
        _context.Set<TEntity>().Remove(entity);

        // Retire aussi du cache si présent
        var cacheType = typeof(ConcurrentDictionary<,>)
            .MakeGenericType(typeof(TEntity), typeof(IEntityViewModel<>).MakeGenericType(typeof(TEntity)));

        if (_caches.TryGetValue(typeof(TEntity), out var cache))
        {
            var removeMethod = cache.GetType().GetMethod("TryRemove", new[] { typeof(TEntity), cacheType.GetGenericArguments()[1].MakeByRefType() });
            removeMethod?.Invoke(cache, new object[] { entity, null });
        }
    }
    finally
    {
        _semaphore.Release();
    }
}
```

---

## FieldViewModel - Système de Propriétés Typées

### Concept Clé

Les `FieldViewModel<T>` encapsulent les propriétés des entités avec métadonnées UI, validation, et binding bidirectionnel. Chaque propriété d'entité est exposée comme un FieldViewModel dédié.

### Classe de Base Générique

```csharp
public partial class FieldViewModel<T> : ObservableObject, IFieldViewModel
{
    private T? _value;
    private readonly Func<T>? _getValue;
    private readonly Action<T>? _setValue;
    private readonly Func<List<T>>? _listQuery;
    private bool _isInitialized;

    // Métadonnées UI
    private string? _warning;
    private string? _error;
    private string? _label;
    private string? _hint;
    private bool _readOnly;

    // Support pour listes déroulantes
    private bool _valueMustBeInTheList;
    private List<T>? _cachedList;

    public FieldViewModel(
        object? parent = null,
        Func<T>? getValue = null,
        Action<T>? setValue = null,
        Func<List<T>>? listQuery = null)
    {
        Parent = parent;
        _getValue = getValue;
        _setValue = setValue;
        _listQuery = listQuery;
    }

    public object? Parent { get; }

    /// <summary>
    /// Valeur du champ avec lazy loading
    /// </summary>
    public T? Value
    {
        get
        {
            // Charge la valeur au premier accès
            if (!_isInitialized && _getValue != null)
            {
                _value = _getValue();
                _isInitialized = true;
            }
            return _value;
        }
        set
        {
            if (ReadOnly) return;

            // Valide que la valeur est dans la liste si nécessaire
            if (ValueMustBeInTheList && List != null && !List.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _value, value))
            {
                _isInitialized = true;

                // Écrit la valeur dans le modèle sous-jacent
                _setValue?.Invoke(value);

                // Re-valide
                Validate();
            }
        }
    }

    /// <summary>
    /// Liste des valeurs possibles (pour ComboBox, etc.)
    /// </summary>
    public List<T>? List
    {
        get
        {
            if (_cachedList == null && _listQuery != null)
            {
                _cachedList = _listQuery();
            }
            return _cachedList;
        }
    }

    /// <summary>
    /// Recharge la liste depuis la requête
    /// </summary>
    public void RefreshList()
    {
        if (_listQuery != null)
        {
            _cachedList = _listQuery();
            OnPropertyChanged(nameof(List));
        }
    }

    // Métadonnées UI
    public string? Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string? Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }

    public bool ReadOnly
    {
        get => _readOnly;
        set => SetProperty(ref _readOnly, value);
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    public bool ValueMustBeInTheList
    {
        get => _valueMustBeInTheList;
        set => SetProperty(ref _valueMustBeInTheList, value);
    }

    // Validation FluentValidation
    public Action<IRuleBuilder<FieldViewModel<T>, T>> ValidationRules { get; set; }

    public bool HasSetValueFunction => _setValue != null;

    /// <summary>
    /// Exécute la validation sur la valeur actuelle
    /// </summary>
    public void Validate()
    {
        Error = null;
        Warning = null;

        if (ValidationRules == null) return;

        // Construit le validateur FluentValidation
        var validator = new InlineValidator<FieldViewModel<T>>();
        var ruleBuilder = validator.RuleFor(x => x.Value);
        ValidationRules(ruleBuilder);

        // Valide
        var result = validator.Validate(this);

        // Sépare les erreurs des warnings selon la sévérité
        var errors = result.Errors.Where(e => e.Severity == Severity.Error).ToList();
        var warnings = result.Errors.Where(e => e.Severity == Severity.Warning).ToList();

        if (errors.Any())
        {
            Error = errors.First().ErrorMessage;
        }
        else if (warnings.Any())
        {
            Warning = warnings.First().ErrorMessage;
        }
    }

    public override string ToString()
    {
        return Label ?? typeof(T).Name;
    }
}
```

### Types Spécialisés de FieldViewModel

#### StringFieldViewModel

```csharp
public partial class StringFieldViewModel : FieldViewModel<string>
{
    public StringFieldViewModel(
        object? parent = null,
        Func<string> getValue = null,
        Action<string> setValue = null,
        Func<List<string>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
    }
}
```

#### IntegerFieldViewModel

```csharp
public partial class IntegerFieldViewModel : FieldViewModel<int>
{
    public IntegerFieldViewModel(
        object? parent = null,
        Func<int> getValue = null,
        Action<int> setValue = null,
        Func<List<int>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
    }

    // Commandes pour incrémenter/décrémenter
    [RelayCommand]
    private void Increment()
    {
        Value++;
    }

    [RelayCommand]
    private void Decrement()
    {
        Value--;
    }
}
```

#### BoolFieldViewModel

```csharp
public partial class BoolFieldViewModel : FieldViewModel<bool>
{
    public BoolFieldViewModel(
        object? parent = null,
        Func<bool> getValue = null,
        Action<bool> setValue = null
    ) : base(parent, getValue, setValue)
    {
    }
}
```

#### DateTimeFieldViewModel

```csharp
public partial class DateTimeFieldViewModel : FieldViewModel<DateTime>
{
    public DateTimeFieldViewModel(
        object? parent = null,
        Func<DateTime> getValue = null,
        Action<DateTime> setValue = null
    ) : base(parent, getValue, setValue)
    {
    }

    public override string ToString()
    {
        return Value.ToString("dd/MM/yyyy HH:mm");
    }
}
```

### FieldViewModel pour Objets Complexes

#### Exemple: PersonFieldViewModel (Référence à une autre entité)

```csharp
public partial class PersonFieldViewModel : FieldViewModel<PersonViewModel>
{
    public PersonFieldViewModel(
        object? parent = null,
        Func<PersonViewModel> getValue = null,
        Action<PersonViewModel> setValue = null,
        Func<List<PersonViewModel>>? listQuery = null
    ) : base(parent, getValue, setValue, listQuery)
    {
        ValueMustBeInTheList = true;
    }
}
```

### ListViewModel pour Collections

```csharp
public partial class PersonsListFieldViewModel : ListViewModel<PersonViewModel>
{
    public PersonsListFieldViewModel(
        object? parent = null,
        Func<IEnumerable<PersonViewModel>>? itemsLoader = null,
        Func<PersonViewModel, bool>? beforeItemAdded = null,
        Action<PersonViewModel>? onItemAdded = null,
        Func<PersonViewModel, bool>? beforeItemRemoved = null,
        Action<PersonViewModel>? onItemRemoved = null
    ) : base(parent, itemsLoader, beforeItemAdded, onItemAdded, beforeItemRemoved, onItemRemoved)
    {
    }
}
```

---

## Pattern Factory pour ViewModels

### Interface Factory

```csharp
public interface IEntityViewModelFactory<TContext, TEntity, TViewModel>
   where TContext : DbContext
   where TEntity : class, IEntity
   where TViewModel : class, IEntityViewModel<TEntity>
{
    TViewModel Create(TEntity entity, GenericRepository<TContext> repository);
}
```

### Implémentation d'une Factory

```csharp
/// <summary>
/// Factory pour PersonViewModel
/// Convention de nommage: {EntityName}ViewModelFactory dans le namespace du ViewModel
/// </summary>
public class PersonViewModelFactory : IEntityViewModelFactory<AppDbContext, Person, PersonViewModel>
{
    public PersonViewModel Create(Person entity, GenericRepository<AppDbContext> repository)
    {
        return new PersonViewModel(entity, repository);
    }
}
```

### Découverte Automatique des Factories

```csharp
// Dans GenericRepository
private IEntityViewModelFactory<TContext, TEntity, TViewModel> GetOrCreateFactory<TEntity, TViewModel>()
    where TEntity : class, IEntity
    where TViewModel : class, IEntityViewModel<TEntity>
{
    var key = typeof(TViewModel);
    return (IEntityViewModelFactory<TContext, TEntity, TViewModel>)_factories.GetOrAdd(key, _ =>
    {
        var entityType = typeof(TEntity);
        var vmType = typeof(TViewModel);

        // Convention de nommage: {EntityName}ViewModelFactory
        var factoryTypeName = $"{vmType.Namespace}.{entityType.Name}ViewModelFactory";

        // Recherche du type par réflexion
        var factoryType = Type.GetType(factoryTypeName);
        if (factoryType == null)
        {
            throw new InvalidOperationException(
                $"Factory not found: {factoryTypeName}. " +
                $"Create a class implementing IEntityViewModelFactory<TContext, {entityType.Name}, {vmType.Name}>"
            );
        }

        // Instancie la factory
        return Activator.CreateInstance(factoryType);
    });
}
```

### Convention de Nommage

| Élément | Convention | Exemple |
|---------|------------|---------|
| **Entité** | `{EntityName}` | `Person` |
| **ViewModel** | `{EntityName}ViewModel` | `PersonViewModel` |
| **Factory** | `{EntityName}ViewModelFactory` | `PersonViewModelFactory` |
| **Namespace Factory** | Même namespace que ViewModel | `MyApp.Core.ViewModel.Persons` |

---

## Système de Validation

### Validation avec FluentValidation

Les règles de validation sont définies directement dans les FieldViewModel via la propriété `ValidationRules`.

#### Validation Simple

```csharp
public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
    parent: this,
    getValue: () => _person.Name,
    setValue: value => _person.Name = value)
{
    Label = "Nom",
    Hint = "Nom de la personne",
    ValidationRules = rules => rules
        .NotEmpty().WithMessage("Le nom ne peut pas être vide.")
            .WithSeverity(Severity.Error)
        .MaximumLength(100).WithMessage("Le nom ne peut pas dépasser 100 caractères.")
            .WithSeverity(Severity.Error)
};
```

#### Validation avec Méthode Personnalisée

```csharp
public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
    parent: this,
    getValue: () => _person.Name,
    setValue: value => _person.Name = value)
{
    Label = "Nom",
    ValidationRules = rules => rules
        .NotEmpty().WithMessage("Le nom ne peut pas être vide.")
            .WithSeverity(Severity.Error)
        .Must(BeAValidName).WithMessage("Le nom contient des caractères invalides.")
            .WithSeverity(Severity.Error)
};

private bool BeAValidName(string name)
{
    if (string.IsNullOrWhiteSpace(name)) return true;
    return name.All(c => char.IsLetter(c) || c == ' ' || c == '-');
}
```

#### Validation avec Warnings

```csharp
public IntegerFieldViewModel Age => _ageField ??= new IntegerFieldViewModel(
    parent: this,
    getValue: () => _person.Age,
    setValue: value => _person.Age = value)
{
    Label = "Age",
    ValidationRules = rules => rules
        // ERREURS (bloquantes)
        .GreaterThanOrEqualTo(0).WithMessage("L'âge ne peut pas être négatif.")
            .WithSeverity(Severity.Error)
        .LessThan(150).WithMessage("L'âge doit être inférieur à 150 ans.")
            .WithSeverity(Severity.Error)
        // WARNINGS (non bloquants)
        .Must(age => age < 100).WithMessage("Un âge supérieur à 100 ans est inhabituel.")
            .WithSeverity(Severity.Warning)
        .Must(age => age >= 18).WithMessage("Cette personne est mineure.")
            .WithSeverity(Severity.Warning)
};
```

### Validation Globale au Niveau Repository

Le `SaveAll()` collecte automatiquement toutes les erreurs de validation avant de sauvegarder:

```csharp
public class ValidationError
{
    public IViewModel ViewModel { get; set; }
    public string PropertyName { get; set; }
    public string ErrorMessage { get; set; }
}

// Utilisation
var errors = repository.SaveAll();
if (errors != null && errors.Any())
{
    // Affiche les erreurs à l'utilisateur
    foreach (var error in errors)
    {
        MessageBox.Show($"{error.PropertyName}: {error.ErrorMessage}");
    }
}
else
{
    // Sauvegarde réussie
    MessageBox.Show("Données sauvegardées avec succès");
}
```

---

## ViewModelTemplateSelector - Résolution Automatique des Vues

### Concept Clé

Le `ViewModelTemplateSelector` mappe automatiquement les ViewModels à leurs Views par convention de nommage, sans configuration manuelle.

### Implémentation Complète

```csharp
public class ViewModelTemplateSelector : DataTemplateSelector
{
    private static readonly ConcurrentDictionary<string, DataTemplate> TemplateCache = new();
    private readonly ILogger<ViewModelTemplateSelector> _logger;

    public ViewModelTemplateSelector()
    {
        _logger = ServiceLocator.GetService<ILogger<ViewModelTemplateSelector>>();
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item == null) return null;

        // Vérifie que c'est un type de ViewModel valide
        bool isValidType = item is IViewModel
            || item is IFieldViewModel
            || item is ICommandViewModel
            || IsGenericListViewModel(item.GetType());

        if (!isValidType) return null;

        var itemType = item.GetType();
        var itemTypeName = itemType.Name;

        // Convention: PersonViewModel → PersonView
        var viewName = itemTypeName.Replace("ViewModel", "View");

        // Vérifie le cache
        if (TemplateCache.TryGetValue(viewName, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        // Recherche et crée le template
        var template = FindAndLoadTemplate(itemType, viewName);
        if (template != null)
        {
            TemplateCache[viewName] = template;
            return template;
        }

        // Template par défaut si aucune vue trouvée
        var defaultTemplate = Application.Current.TryFindResource("DefaultViewModelTemplate") as DataTemplate;
        TemplateCache[viewName] = defaultTemplate;
        return defaultTemplate;
    }

    private DataTemplate FindAndLoadTemplate(Type viewModelType, string viewName)
    {
        try
        {
            // Transformation du namespace:
            // MyApp.Core.ViewModel.Persons.PersonViewModel → MyApp.View.Person.PersonView
            string viewTypeName = viewModelType.FullName.Replace("ViewModel", "View");
            viewTypeName = viewTypeName.Replace(".ViewModel.", ".View.").Replace("_core.", ".");

            // Récupère les assemblies pertinents
            var viewModelAssembly = viewModelType.Assembly;
            var baseAssemblyName = viewModelAssembly.GetName().Name.Replace("_core", "");

            var relevantAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith(baseAssemblyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Type viewType = null;

            // Tente de trouver le type exact
            foreach (var assembly in relevantAssemblies)
            {
                viewType = assembly.GetType(viewTypeName);
                if (viewType != null) break;
            }

            // Fallback: recherche par nom uniquement
            if (viewType == null)
            {
                foreach (var assembly in relevantAssemblies)
                {
                    viewType = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == viewName && typeof(UserControl).IsAssignableFrom(t));
                    if (viewType != null) break;
                }
            }

            if (viewType != null)
            {
                // Crée le DataTemplate dynamiquement
                var dataTemplate = new DataTemplate(viewModelType);
                var frameworkElementFactory = new FrameworkElementFactory(viewType);
                dataTemplate.VisualTree = frameworkElementFactory;
                return dataTemplate;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating template for {ViewName}", viewName);
        }

        return null;
    }

    private bool IsGenericListViewModel(Type type)
    {
        if (!type.IsGenericType) return false;
        var genericTypeDef = type.GetGenericTypeDefinition();
        return genericTypeDef == typeof(ListViewModel<>);
    }

    public static void ClearCache() => TemplateCache.Clear();
}
```

### Règles de Transformation

| Source | Transformation | Résultat |
|--------|----------------|----------|
| `PersonViewModel` | Replace "ViewModel" → "View" | `PersonView` |
| `Csharp_WPF_MVVM_core.ViewModel.Persons` | `.ViewModel.` → `.View.` | `Csharp_WPF_MVVM.View.Person` |
| `_core` (assembly) | Remove `_core` | Assembly principal |

### Configuration dans App.xaml

```xml
<Application x:Class="CSharp_WPF_MVVM.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:CSharp_WPF_MVVM">
    <Application.Resources>
        <ResourceDictionary>
            <!-- ViewModelTemplateSelector global -->
            <local:ViewModelTemplateSelector x:Key="ViewModelTemplateSelector"/>

            <!-- Template par défaut pour ViewModels sans View -->
            <DataTemplate x:Key="DefaultViewModelTemplate">
                <TextBlock Text="{Binding}" Foreground="Red"/>
            </DataTemplate>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### Utilisation dans les Views

```xml
<!-- ContentControl avec résolution automatique -->
<ContentControl Content="{Binding CurrentViewModel}"
                ContentTemplateSelector="{StaticResource ViewModelTemplateSelector}"/>

<!-- ItemsControl pour listes -->
<ItemsControl ItemsSource="{Binding Items}"
              ItemTemplateSelector="{StaticResource ViewModelTemplateSelector}"/>
```

---

## Classes de Support

### BaseViewModel

```csharp
public partial class BaseViewModel : ObservableObject, IViewModel
{
    private readonly GenericRepository<AppDbContext> _repository;
    private readonly IWindowService _windowService;
    private readonly ILogger<BaseViewModel> _logger;
    private bool _isBusy;

    public BaseViewModel(GenericRepository<AppDbContext> repository)
    {
        _repository = repository;
        _windowService = ServiceLocator.GetService<IWindowService>();
        _logger = ServiceLocator.GetService<ILogger<BaseViewModel>>();
    }

    public GenericRepository<AppDbContext> Repository => _repository;
    public IWindowService WindowService => _windowService;
    public ILogger<BaseViewModel> Log => _logger;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
```

### Interfaces

```csharp
// Interface de base pour tous les ViewModels
public interface IViewModel
{
    GenericRepository<AppDbContext> Repository { get; }
}

// Interface pour les ViewModels d'entités
public interface IEntityViewModel<TEntity> where TEntity : class, IEntity
{
    TEntity Model { get; }
    GenericRepository<AppDbContext> Repository { get; }
}

// Interface pour les champs de ViewModel
public interface IFieldViewModel
{
    object? Parent { get; }
    string? Label { get; set; }
    string? Hint { get; set; }
    bool ReadOnly { get; set; }
    string? Error { get; }
    string? Warning { get; }
    bool HasSetValueFunction { get; }
    void Validate();
}

// Interface pour les entités
public interface IEntity
{
    int Id { get; set; }
}
```

### ListViewModel

```csharp
public partial class ListViewModel<TViewModel> : ObservableObject, IListViewModel<TViewModel>
    where TViewModel : class
{
    private readonly Func<IEnumerable<TViewModel>>? _itemsLoader;
    private readonly Func<TViewModel, bool>? _beforeItemAdded;
    private readonly Action<TViewModel>? _onItemAdded;
    private readonly Func<TViewModel, bool>? _beforeItemRemoved;
    private readonly Action<TViewModel>? _onItemRemoved;

    public ListViewModel(
        object? parent = null,
        Func<IEnumerable<TViewModel>>? itemsLoader = null,
        Func<TViewModel, bool>? beforeItemAdded = null,
        Action<TViewModel>? onItemAdded = null,
        Func<TViewModel, bool>? beforeItemRemoved = null,
        Action<TViewModel>? onItemRemoved = null)
    {
        Parent = parent;
        _itemsLoader = itemsLoader;
        _beforeItemAdded = beforeItemAdded;
        _onItemAdded = onItemAdded;
        _beforeItemRemoved = beforeItemRemoved;
        _onItemRemoved = onItemRemoved;

        Items = new ObservableCollection<TViewModel>();
        SelectedItems = new ObservableCollection<TViewModel>();
    }

    public object? Parent { get; }
    public ObservableCollection<TViewModel> Items { get; }
    public ObservableCollection<TViewModel> SelectedItems { get; set; }

    public bool CanAdd { get; set; }
    public bool CanRemove { get; set; }
    public bool CanReplace { get; set; }
    public bool CanMove { get; set; }
    public bool CanSelect { get; set; }

    public void LoadItems()
    {
        Items.Clear();
        if (_itemsLoader != null)
        {
            foreach (var item in _itemsLoader())
            {
                Items.Add(item);
            }
        }
    }

    public void Refresh()
    {
        LoadItems();
    }

    [RelayCommand]
    private void AddItem(TViewModel item)
    {
        if (!CanAdd) return;

        if (_beforeItemAdded != null && !_beforeItemAdded(item)) return;

        Items.Add(item);
        _onItemAdded?.Invoke(item);
    }

    [RelayCommand]
    private void RemoveItem(TViewModel item)
    {
        if (!CanRemove) return;

        if (_beforeItemRemoved != null && !_beforeItemRemoved(item)) return;

        Items.Remove(item);
        _onItemRemoved?.Invoke(item);
    }
}
```

### ServiceLocator

```csharp
public static class ServiceLocator
{
    private static IServiceProvider _serviceProvider;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public static T GetService<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
        }
        return _serviceProvider.GetService<T>();
    }
}
```

### Configuration Dependency Injection (App.xaml.cs)

```csharp
public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialise le ServiceLocator
        ServiceLocator.Initialize(_serviceProvider);

        // Lance la fenêtre principale
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=YourDatabase.db"));

        // Repository
        services.AddSingleton<GenericRepository<AppDbContext>>();

        // Services
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ViewModelTemplateSelector>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
        });

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}
```

---

## Exemple Complet d'Implémentation

### Étape 1: Créer le Modèle

```csharp
using System.ComponentModel.DataAnnotations;

namespace MyApp.Core.Data.Model
{
    public class Person : IEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public int Age { get; set; }

        public bool IsTeacher { get; set; }

        public DateTime StartDateTime { get; set; }

        public DateTime EndDateTime { get; set; }
    }
}
```

### Étape 2: Ajouter au DbContext

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> Persons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Person>()
            .HasKey(p => p.Id);
    }
}
```

### Étape 3: Créer le ViewModel avec Factory

```csharp
namespace MyApp.Core.ViewModel.Persons
{
    // Factory
    public class PersonViewModelFactory : IEntityViewModelFactory<AppDbContext, Data.Model.Person, PersonViewModel>
    {
        public PersonViewModel Create(Data.Model.Person entity, GenericRepository<AppDbContext> repository)
        {
            return new PersonViewModel(entity, repository);
        }
    }

    // ViewModel
    public partial class PersonViewModel : BaseViewModel, IEntityViewModel<Data.Model.Person>
    {
        private readonly Data.Model.Person _person;

        // Fields (lazy-initialized)
        private IntegerFieldViewModel _idField;
        private StringFieldViewModel _nameField;
        private IntegerFieldViewModel _ageField;
        private BoolFieldViewModel _isTeacher;
        private DateTimeFieldViewModel _startDateTime;
        private DateTimeFieldViewModel _endDateTime;

        public PersonViewModel(Data.Model.Person person, GenericRepository<AppDbContext> repository)
            : base(repository)
        {
            _person = person;
        }

        public Data.Model.Person Model => _person;

        // Propriété Id (ReadOnly)
        public IntegerFieldViewModel Id => _idField ??= new IntegerFieldViewModel(
            parent: this,
            getValue: () => _person.Id,
            setValue: value => _person.Id = value)
        {
            Hint = "Id de la personne en DB",
            Label = "Id",
            ReadOnly = true
        };

        // Propriété Name avec validation
        public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
            parent: this,
            getValue: () => _person.Name,
            setValue: value => _person.Name = value)
        {
            Hint = "Nom de la personne",
            Label = "Nom",
            ValidationRules = rules => rules
                .NotEmpty().WithMessage("Le nom ne peut pas être vide.")
                    .WithSeverity(Severity.Error)
                .MaximumLength(100).WithMessage("Le nom ne peut pas dépasser 100 caractères.")
                    .WithSeverity(Severity.Error)
                .Must(BeAValidName).WithMessage("Le nom contient des caractères invalides.")
                    .WithSeverity(Severity.Error)
                .Must(name => !string.IsNullOrWhiteSpace(name) && name.Length >= 2)
                    .WithMessage("Un nom très court peut être invalide.")
                    .WithSeverity(Severity.Warning)
        };

        // Propriété Age avec validation Error + Warning
        public IntegerFieldViewModel Age => _ageField ??= new IntegerFieldViewModel(
            parent: this,
            getValue: () => _person.Age,
            setValue: value => _person.Age = value)
        {
            Hint = "Age de la personne",
            Label = "Age",
            ValidationRules = rules => rules
                .GreaterThanOrEqualTo(0).WithMessage("L'âge ne peut pas être négatif.")
                    .WithSeverity(Severity.Error)
                .LessThan(150).WithMessage("L'âge doit être inférieur à 150 ans.")
                    .WithSeverity(Severity.Error)
                .Must(age => age < 100).WithMessage("Un âge supérieur à 100 ans est inhabituel.")
                    .WithSeverity(Severity.Warning)
                .Must(age => age >= 18).WithMessage("Cette personne est mineure.")
                    .WithSeverity(Severity.Warning)
        };

        // Propriété booléenne
        public BoolFieldViewModel IsTeacher => _isTeacher ??= new BoolFieldViewModel(
            parent: this,
            getValue: () => _person.IsTeacher,
            setValue: value => _person.IsTeacher = value)
        {
            Hint = "La personne est professeur",
            Label = "Est Prof."
        };

        // Propriétés DateTime
        public DateTimeFieldViewModel StartDateTime => _startDateTime ??= new DateTimeFieldViewModel(
            parent: this,
            getValue: () => _person.StartDateTime,
            setValue: value => _person.StartDateTime = value)
        {
            Hint = "Une date de début",
            Label = "Début"
        };

        public DateTimeFieldViewModel EndDateTime => _endDateTime ??= new DateTimeFieldViewModel(
            parent: this,
            getValue: () => _person.EndDateTime,
            setValue: value => _person.EndDateTime = value)
        {
            Hint = "Une date de fin",
            Label = "Fin"
        };

        // Méthode de validation personnalisée
        private bool BeAValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return name.All(c => char.IsLetter(c) || c == ' ' || c == '-');
        }

        public override string ToString() => $"Person: {Id.Value}, {Name.Value}";
    }
}
```

### Étape 4: Créer la View (XAML)

```xml
<UserControl x:Class="MyApp.View.Person.PersonView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes">
    <StackPanel Margin="10">
        <!-- Id (ReadOnly) -->
        <TextBox Text="{Binding Id.Value}"
                 md:HintAssist.Hint="{Binding Id.Hint}"
                 md:HintAssist.IsFloating="True"
                 IsReadOnly="{Binding Id.ReadOnly}"
                 Style="{StaticResource MaterialDesignOutlinedTextBox}"
                 Margin="0,5"/>

        <!-- Name -->
        <TextBox Text="{Binding Name.Value, UpdateSourceTrigger=PropertyChanged}"
                 md:HintAssist.Hint="{Binding Name.Hint}"
                 md:HintAssist.IsFloating="True"
                 md:TextFieldAssist.HasClearButton="True"
                 Style="{StaticResource MaterialDesignOutlinedTextBox}"
                 Margin="0,5">
            <md:TextFieldAssist.ErrorText>
                <Binding Path="Name.Error"/>
            </md:TextFieldAssist.ErrorText>
        </TextBox>

        <!-- Warning si présent -->
        <TextBlock Text="{Binding Name.Warning}"
                   Foreground="Orange"
                   Margin="0,0,0,5"
                   Visibility="{Binding Name.Warning, Converter={StaticResource NullToVisibilityConverter}}"/>

        <!-- Age avec boutons +/- -->
        <Grid Margin="0,5">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBox Grid.Column="0"
                     Text="{Binding Age.Value, UpdateSourceTrigger=PropertyChanged}"
                     md:HintAssist.Hint="{Binding Age.Hint}"
                     md:HintAssist.IsFloating="True"
                     Style="{StaticResource MaterialDesignOutlinedTextBox}">
                <md:TextFieldAssist.ErrorText>
                    <Binding Path="Age.Error"/>
                </md:TextFieldAssist.ErrorText>
            </TextBox>

            <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="5,0,0,0">
                <Button Command="{Binding Age.DecrementCommand}"
                        Content="-"
                        Width="30"/>
                <Button Command="{Binding Age.IncrementCommand}"
                        Content="+"
                        Width="30"
                        Margin="5,0,0,0"/>
            </StackPanel>
        </Grid>

        <TextBlock Text="{Binding Age.Warning}"
                   Foreground="Orange"
                   Margin="0,0,0,5"
                   Visibility="{Binding Age.Warning, Converter={StaticResource NullToVisibilityConverter}}"/>

        <!-- IsTeacher -->
        <CheckBox IsChecked="{Binding IsTeacher.Value}"
                  Content="{Binding IsTeacher.Label}"
                  Margin="0,10"/>

        <!-- Start DateTime -->
        <DatePicker SelectedDate="{Binding StartDateTime.Value}"
                    md:HintAssist.Hint="{Binding StartDateTime.Hint}"
                    md:HintAssist.IsFloating="True"
                    Style="{StaticResource MaterialDesignOutlinedDatePicker}"
                    Margin="0,5"/>

        <!-- End DateTime -->
        <DatePicker SelectedDate="{Binding EndDateTime.Value}"
                    md:HintAssist.Hint="{Binding EndDateTime.Hint}"
                    md:HintAssist.IsFloating="True"
                    Style="{StaticResource MaterialDesignOutlinedDatePicker}"
                    Margin="0,5"/>
    </StackPanel>
</UserControl>
```

### Étape 5: Utilisation dans un ViewModel Parent

```csharp
public partial class MainViewModel : BaseViewModel
{
    private ObservableCollection<PersonViewModel> _persons;
    private PersonViewModel _selectedPerson;

    public MainViewModel(GenericRepository<AppDbContext> repository) : base(repository)
    {
        LoadPersons();
    }

    public ObservableCollection<PersonViewModel> Persons
    {
        get => _persons;
        set => SetProperty(ref _persons, value);
    }

    public PersonViewModel SelectedPerson
    {
        get => _selectedPerson;
        set => SetProperty(ref _selectedPerson, value);
    }

    private void LoadPersons()
    {
        var persons = Repository.GetAllViewModels<Data.Model.Person, PersonViewModel>();
        Persons = new ObservableCollection<PersonViewModel>(persons);
    }

    [RelayCommand]
    private void AddPerson()
    {
        var newPerson = Repository.GetNewViewModel<Data.Model.Person, PersonViewModel>();
        Persons.Add(newPerson);
        SelectedPerson = newPerson;
    }

    [RelayCommand]
    private void DeletePerson(PersonViewModel person)
    {
        if (person == null) return;

        Repository.DeleteEntity(person.Model);
        Persons.Remove(person);
    }

    [RelayCommand]
    private void Save()
    {
        var errors = Repository.SaveAll();
        if (errors != null && errors.Any())
        {
            var errorMessage = string.Join("\n", errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            WindowService.ShowMessageBox("Erreurs de validation", errorMessage);
        }
        else
        {
            WindowService.ShowMessageBox("Succès", "Données sauvegardées avec succès");
            LoadPersons(); // Recharge pour rafraîchir les IDs
        }
    }
}
```

### Étape 6: View Principale

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"
        Title="Gestion des Personnes" Height="600" Width="800">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <ToolBar Grid.Row="0">
            <Button Command="{Binding AddPersonCommand}" Content="Ajouter"/>
            <Button Command="{Binding DeletePersonCommand}"
                    CommandParameter="{Binding SelectedPerson}"
                    Content="Supprimer"/>
            <Separator/>
            <Button Command="{Binding SaveCommand}" Content="Sauvegarder"/>
        </ToolBar>

        <!-- Liste et détails -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="250"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Liste des personnes -->
            <ListBox Grid.Column="0"
                     ItemsSource="{Binding Persons}"
                     SelectedItem="{Binding SelectedPerson}"
                     DisplayMemberPath="Name.Value"
                     Margin="5"/>

            <!-- Détails de la personne sélectionnée -->
            <ContentControl Grid.Column="1"
                            Content="{Binding SelectedPerson}"
                            ContentTemplateSelector="{StaticResource ViewModelTemplateSelector}"
                            Margin="5"/>
        </Grid>

        <!-- StatusBar -->
        <StatusBar Grid.Row="2">
            <StatusBarItem Content="{Binding Persons.Count, StringFormat='Nombre de personnes: {0}'}"/>
        </StatusBar>
    </Grid>
</Window>
```

---

## Diagramme d'Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         FLUX DE DONNÉES                          │
└─────────────────────────────────────────────────────────────────┘

1. RÉCUPÉRATION D'UN VIEWMODEL

   UI demande une personne
         ↓
   Repository.GetViewModel<Person, PersonViewModel>(personEntity)
         ↓
   ┌─────────────────────────────────────┐
   │ GenericRepository                    │
   │ - Vérifie cache                      │
   │ - Si absent:                         │
   │   1. Trouve PersonViewModelFactory   │
   │   2. Factory.Create(entity, repo)    │
   │   3. Met en cache                    │
   └─────────────────────────────────────┘
         ↓
   PersonViewModel (avec _person entity)
         ↓
   Expose FieldViewModels (lazy-init)
         ↓
   UI Binding


2. MODIFICATION D'UNE PROPRIÉTÉ

   User modifie le nom dans UI
         ↓
   Binding: PersonView.TextBox.Text → PersonViewModel.Name.Value
         ↓
   ┌─────────────────────────────────────┐
   │ StringFieldViewModel.Value setter    │
   │ 1. SetProperty (notify UI)           │
   │ 2. _setValue?.Invoke(value)          │
   │    → _person.Name = value            │
   │ 3. Validate()                        │
   └─────────────────────────────────────┘
         ↓
   Entity modifié (EF ChangeTracker: Modified)
         ↓
   ValidationRules exécutées
         ↓
   Error/Warning affiché dans UI


3. SAUVEGARDE

   User clique "Sauvegarder"
         ↓
   Repository.SaveAll()
         ↓
   ┌─────────────────────────────────────┐
   │ GenericRepository.SaveAll()          │
   │ 1. Parcourt tous les caches          │
   │ 2. Pour chaque ViewModel:            │
   │    - Lit toutes les propriétés       │
   │    - Collecte les FieldViewModel     │
   │    - Vérifie field.Error != null     │
   │ 3. Si erreurs: retourne liste        │
   │ 4. Sinon: _context.SaveChanges()     │
   └─────────────────────────────────────┘
         ↓
   Succès: Entités sauvegardées en DB
   Échec: Liste ValidationError retournée


4. RÉSOLUTION DE VUE

   ContentControl reçoit PersonViewModel
         ↓
   ViewModelTemplateSelector.SelectTemplate()
         ↓
   ┌─────────────────────────────────────┐
   │ ViewModelTemplateSelector            │
   │ 1. Type: PersonViewModel             │
   │ 2. viewName = "PersonView"           │
   │ 3. Vérifie cache                     │
   │ 4. Si absent:                        │
   │    - Transforme namespace            │
   │    - Cherche PersonView par réflexion│
   │    - Crée DataTemplate               │
   │    - Met en cache                    │
   └─────────────────────────────────────┘
         ↓
   DataTemplate avec PersonView instancié
         ↓
   PersonView.xaml affiché avec bindings


┌─────────────────────────────────────────────────────────────────┐
│                    CYCLE DE VIE D'UN VIEWMODEL                   │
└─────────────────────────────────────────────────────────────────┘

1. [CRÉATION]
   Factory.Create(entity, repository)
   → ViewModel instancié
   → Stocké dans cache
   → FieldViewModels NON initialisés (lazy)

2. [PREMIER ACCÈS PROPRIÉTÉ]
   UI: {Binding Name.Value}
   → Name getter appelé (lazy-init avec ??=)
   → StringFieldViewModel créé
   → getValue() appelé → lit _person.Name
   → ValidationRules configurées (pas encore exécutées)

3. [MODIFICATION]
   User input → Name.Value setter
   → _person.Name modifié (EF tracking)
   → Validate() exécutée
   → OnPropertyChanged → UI mise à jour

4. [SAUVEGARDE]
   SaveAll() collecte erreurs
   → Si OK: SaveChanges()
   → _person.Id mis à jour par EF
   → ViewModel reste en cache

5. [SUPPRESSION]
   DeleteEntity(_person)
   → EF: entity removed
   → Cache: TryRemove
   → ViewModel éligible au GC
```

---

## Bonnes Pratiques et Recommandations

### 1. Conventions de Nommage (STRICTES)

| Élément | Convention | Exemple |
|---------|------------|---------|
| Entité | `{Name}` | `Person`, `Room` |
| ViewModel | `{Name}ViewModel` | `PersonViewModel` |
| Factory | `{Name}ViewModelFactory` | `PersonViewModelFactory` |
| View | `{Name}View` | `PersonView` |
| Namespace ViewModel | `{App}.Core.ViewModel.{Entity}` | `MyApp.Core.ViewModel.Persons` |
| Namespace View | `{App}.View.{Entity}` | `MyApp.View.Person` |

### 2. Thread Safety

**DO:**
- Toujours appeler Repository methods pour accès aux entités
- Utiliser `SemaphoreSlim` si vous ajoutez des opérations DbContext custom
- Confier la gestion du cache au GenericRepository

**DON'T:**
- Accéder directement au DbContext depuis les ViewModels
- Créer des ViewModels manuellement (utiliser Factory via Repository)
- Modifier `_caches` ou `_factories` manuellement

### 3. Performance

**Lazy Loading:**
```csharp
// ✅ BON: Lazy initialization avec ??=
public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(...)

// ❌ MAUVAIS: Création à chaque accès
public StringFieldViewModel Name => new StringFieldViewModel(...)
```

**Cache de listes:**
```csharp
// ✅ BON: ListQuery avec cache
public PersonFieldViewModel Teacher => _teacherField ??= new PersonFieldViewModel(
    listQuery: () => Repository.GetViewModels<Person, PersonViewModel>(
        Repository.GetAllPersonsTeachers()).ToList()
)

// Rafraîchit la liste si nécessaire
Teacher.RefreshList();
```

**Template Caching:**
Le ViewModelTemplateSelector cache automatiquement, mais vous pouvez le vider si nécessaire:
```csharp
ViewModelTemplateSelector.ClearCache();
```

### 4. Validation

**Sévérités:**
- **Error**: Bloque la sauvegarde (ex: champ requis, format invalide)
- **Warning**: Informe l'utilisateur mais permet la sauvegarde (ex: valeur inhabituelle)

```csharp
ValidationRules = rules => rules
    .NotEmpty().WithMessage("Requis").WithSeverity(Severity.Error)
    .Must(BeReasonable).WithMessage("Inhabituel").WithSeverity(Severity.Warning)
```

**Validation manuelle:**
```csharp
// Force la validation d'un champ
myField.Validate();

// Vérifie les erreurs avant une action
if (!string.IsNullOrEmpty(Name.Error))
{
    MessageBox.Show("Corrigez les erreurs");
    return;
}
```

### 5. Gestion des Erreurs

**Lors de SaveAll():**
```csharp
var errors = Repository.SaveAll();
if (errors != null && errors.Any())
{
    // Grouper par ViewModel si nécessaire
    var grouped = errors.GroupBy(e => e.ViewModel);
    foreach (var group in grouped)
    {
        var vmErrors = string.Join("\n", group.Select(e => $"- {e.PropertyName}: {e.ErrorMessage}"));
        Log.LogWarning($"Validation errors in {group.Key}:\n{vmErrors}");
    }

    WindowService.ShowMessageBox("Validation", "Veuillez corriger les erreurs");
}
else
{
    WindowService.ShowMessageBox("Succès", "Sauvegardé");
}
```

### 6. Séparation des Concerns

**Structure de projet:**
```
MyApp.Core (Class Library .NET 8)
├── Data
│   ├── Model
│   │   ├── Person.cs
│   │   └── IEntity.cs
│   ├── AppDbContext.cs
│   ├── GenericRepository.cs
│   └── EntityEqualityComparer.cs
├── ViewModel
│   ├── Base
│   │   ├── BaseViewModel.cs
│   │   └── IViewModel.cs
│   ├── Field
│   │   ├── FieldViewModel.cs
│   │   ├── StringFieldViewModel.cs
│   │   └── ...
│   ├── Person
│   │   ├── PersonViewModel.cs
│   │   └── PersonViewModelFactory.cs
│   └── ListViewModel.cs
└── ServiceLocator.cs

MyApp (WPF .NET 8)
├── View
│   ├── Person
│   │   └── PersonView.xaml
│   └── MainWindow.xaml
├── ViewModelTemplateSelector.cs
├── App.xaml
└── App.xaml.cs
```

**Dépendances:**
- ❌ Core NE DOIT PAS référencer le projet WPF
- ✅ WPF référence Core
- ✅ Core peut contenir des interfaces pour services UI (IWindowService)
- ✅ WPF implémente ces interfaces

### 7. Commandes et Actions

**CommandViewModel:**
```csharp
public partial class PersonViewModel : BaseViewModel, IEntityViewModel<Person>
{
    private CommandViewModel _saveCommand;

    public CommandViewModel SaveCommand => _saveCommand ??= new CommandViewModel(
        parent: this,
        text: "Sauvegarder",
        hint: "Sauvegarde la personne",
        execute: async () =>
        {
            IsBusy = true;
            try
            {
                var errors = Repository.SaveAll();
                if (errors == null)
                {
                    WindowService.ShowMessageBox("Succès", "Personne sauvegardée");
                }
            }
            finally
            {
                IsBusy = false;
            }
        },
        canExecute: () => !string.IsNullOrEmpty(Name.Error)
    );
}
```

### 8. Relations Entre Entités

**One-to-One / Many-to-One:**
```csharp
// Dans RoomViewModel
public PersonFieldViewModel Teacher => _teacherField ??= new PersonFieldViewModel(
    parent: this,
    getValue: () => Repository.GetViewModel<Person, PersonViewModel>(_room.Teacher),
    setValue: value => _room.Teacher = value?.Model,
    listQuery: () => Repository.GetAllViewModels<Person, PersonViewModel>()
        .Where(p => p.IsTeacher.Value).ToList()
)
{
    Label = "Professeur",
    ValueMustBeInTheList = true // Force sélection depuis la liste
};
```

**One-to-Many / Many-to-Many:**
```csharp
// Dans RoomViewModel
public PersonsListFieldViewModel Students => _studentsField ??= new PersonsListFieldViewModel(
    parent: this,
    itemsLoader: () => Repository.GetViewModels<Person, PersonViewModel>(_room.Students).ToList(),
    beforeItemAdded: (student) =>
    {
        // Validation métier
        if (_room.Students.Contains(student.Model))
        {
            WindowService.ShowMessageBox("Erreur", "Cet étudiant est déjà dans la salle");
            return false;
        }

        _room.Students.Add(student.Model);
        return true;
    },
    beforeItemRemoved: (student) =>
    {
        _room.Students.Remove(student.Model);
        return true;
    }
)
{
    CanAdd = true,
    CanRemove = true,
    CanSelect = true
};
```

### 9. Debugging et Logging

```csharp
public partial class PersonViewModel : BaseViewModel
{
    public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
        getValue: () =>
        {
            Log.LogDebug($"Reading Name for Person {_person.Id}");
            return _person.Name;
        },
        setValue: value =>
        {
            Log.LogDebug($"Setting Name to '{value}' for Person {_person.Id}");
            _person.Name = value;
        }
    );
}
```

### 10. Migration depuis un MVVM Classique

**Avant (MVVM classique):**
```csharp
public class PersonViewModel : ObservableObject
{
    private Person _person;
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                _person.Name = value;
                ValidateName();
            }
        }
    }

    private void ValidateName()
    {
        // Validation manuelle...
    }
}
```

**Après (ce système):**
```csharp
public partial class PersonViewModel : BaseViewModel, IEntityViewModel<Person>
{
    private Person _person;
    private StringFieldViewModel _nameField;

    public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
        parent: this,
        getValue: () => _person.Name,
        setValue: value => _person.Name = value)
    {
        Label = "Nom",
        ValidationRules = rules => rules
            .NotEmpty().WithMessage("Le nom est requis")
            .WithSeverity(Severity.Error)
    };
}
```

**Avantages:**
- ✅ Validation déclarative avec FluentValidation
- ✅ Métadonnées UI (Label, Hint) colocalisées
- ✅ Lazy loading automatique
- ✅ Binding bidirectionnel simplifié
- ✅ Erreurs/warnings séparés

---

## Résumé des Patterns Clés

| Pattern | But | Fichier Clé |
|---------|-----|-------------|
| **Generic Repository** | Accès données + cache ViewModels | `GenericRepository.cs` |
| **Factory Pattern** | Création ViewModels avec DI | `IEntityViewModelFactory.cs` |
| **Field ViewModel** | Encapsulation propriétés + validation | `FieldViewModel.cs` |
| **Template Selector** | Résolution automatique View/ViewModel | `ViewModelTemplateSelector.cs` |
| **Entity Equality** | Gestion cache entités non persistées | `EntityEqualityComparer.cs` |
| **Lazy Initialization** | Performance (chargement différé) | Opérateur `??=` partout |
| **Service Locator** | Injection dépendances dans ViewModels | `ServiceLocator.cs` |
| **Validation First-Class** | FluentValidation intégrée | `FieldViewModel.Validate()` |

---

## Conclusion

Cette architecture MVVM offre:

1. **Productivité**: Ajout rapide de nouvelles entités (convention > configuration)
2. **Maintenabilité**: Séparation stricte des concerns, code DRY
3. **Performance**: Cache, lazy loading, template caching
4. **Robustesse**: Thread-safety, validation complète avant sauvegarde
5. **Flexibilité**: Facilement extensible (nouveaux FieldViewModel types, validateurs custom)

Pour implémenter dans un nouveau projet:
1. Copiez les classes de base (GenericRepository, FieldViewModel, BaseViewModel, etc.)
2. Configurez le DI dans App.xaml.cs
3. Créez vos entités, ViewModels (+ Factories), et Views
4. Le reste fonctionne par convention

**Points critiques à respecter:**
- ✅ Conventions de nommage strictes
- ✅ Toujours passer par le Repository
- ✅ Une Factory par EntityViewModel
- ✅ Lazy initialization des FieldViewModel
- ✅ ServiceLocator initialisé au démarrage

Bonne chance pour votre implémentation !
