# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build BlazorServerApp.slnx

# Run the Blazor Server app (from ViewBlazor directory)
dotnet run --project ViewBlazor/ViewBlazor.csproj

# Run with hot reload for development
dotnet watch --project ViewBlazor/ViewBlazor.csproj

# EF Core migrations (run from ViewBlazor directory for startup project context)
dotnet ef migrations add <MigrationName> --project ../Model/Model.csproj
dotnet ef database update --project ../Model/Model.csproj
```

## Architecture

Three-layer .NET 10.0 Blazor Server application using MVVM pattern:

```
ViewBlazor (UI) → ViewModel → Model (Data)
```

- **Model**: Entity Framework Core 10.0 with SQLite, Unit of Work pattern, repositories
- **ViewModel**: MVVM with CommunityToolkit.Mvvm, FluentValidation for validation
- **ViewBlazor**: Blazor Server components and pages

### Project Structure

```
Model/
├── Data/
│   ├── ApplicationDbContext.cs
│   └── EntityConfigurations/      # IEntityTypeConfiguration<T>
├── Entities/                      # Domain entities (implement IEntity)
├── Repositories/                  # IGenericRepository<T>, GenericRepository<T>
├── UnitOfWork/                    # IUnitOfWork, UnitOfWork
├── ViewModels/                    # Interfaces: IFieldViewModel, IEntityViewModel, IViewModel
├── Factories/                     # IEntityViewModelFactory<TEntity, TViewModel>
└── DependencyInjection.cs

ViewModel/
├── Commons/
│   ├── Bases/                     # BaseViewModel
│   └── Fields/                    # FieldViewModels, CommandViewModel
├── {EntityName}/                  # EntityViewModel + Factory (e.g., Persons/)
├── {ListName}ViewModel.cs         # Page-level ViewModels
└── DependencyInjection.cs

ViewBlazor/
├── Components/
│   ├── Base/                      # ViewModelPageBase<T>, ViewModelComponentBase<T>
│   ├── Commons/Fields/            # StringFieldView, IntegerFieldView, etc.
│   └── {EntityName}/              # EntityView.razor
├── Pages/                         # Routable pages (@page)
└── Program.cs
```

## Key Patterns

### FieldViewModel Pattern

All entity properties are wrapped in typed FieldViewModels:

| Type | Usage |
|------|-------|
| `StringFieldViewModel` | Text properties |
| `IntegerFieldViewModel` | Numbers (includes +/- commands) |
| `BoolFieldViewModel` | Checkboxes |
| `DateTimeFieldViewModel` | Date/time pickers |
| `ReferenceFieldViewModel<T>` | Entity references (dropdowns) |

**Features:**
- Lazy-loaded values via getter/setter delegates
- FluentValidation with Error/Warning severity separation
- UI metadata (Label, Hint, ReadOnly)
- Computed field support with auto-recalculation
- Automatic entity change tracking via `MarkAsModified()`
- List support via `listQuery` for dropdowns

**Example:**
```csharp
public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
    parent: this,
    getValue: () => _entity.Name,
    setValue: value => _entity.Name = value)
{
    Label = "Name",
    Hint = "Full name",
    ValidationRules = rules => rules
        .NotEmpty().WithMessage("Required.").WithSeverity(Severity.Error)
        .MaximumLength(100).WithMessage("Too long.").WithSeverity(Severity.Error)
        .Must(n => n?.Length >= 2).WithMessage("Short name.").WithSeverity(Severity.Warning)
};
```

### CommandViewModel Pattern

Commands for user actions with sync/async support:

```csharp
public CommandViewModel SaveCommand => _saveCommand ??= new CommandViewModel(
    parent: this,
    text: "Save",
    hint: "Save all changes",
    execute: SaveInternal,           // or executeAsync: SaveAsync
    canExecute: () => HasChanges,    // optional conditional enable
    style: CommandStyle.Primary      // Default, Primary, Success, Danger, Warning, Info
);
```

Parameterized version: `CommandViewModel<T>` with `execute: (T param) => ...`

### Unit of Work

`IUnitOfWork` manages DbContext, repositories, ViewModel caching, and validation:

```csharp
// Get ViewModels
var vm = UnitOfWork.GetViewModel<Person, PersonViewModel>(entity);
var all = UnitOfWork.GetAllViewModels<Person, PersonViewModel>();
var newVm = UnitOfWork.GetNewViewModel<Person, PersonViewModel>();

// CRUD
UnitOfWork.DeleteEntity(entity);
UnitOfWork.MarkAsModified(entity);

// Persist
List<ValidationError>? errors = UnitOfWork.SaveAll();  // null = success
UnitOfWork.DiscardChanges();
bool hasChanges = UnitOfWork.HasChanges();
```

### ViewModelPageBase<T>

Base class for Blazor pages that auto-injects ViewModel and subscribes to `PropertyChanged`:

```razor
@page "/persons"
@inherits ViewModelPageBase<PersonListViewModel>

@code {
    protected override void OnInitialized()
    {
        base.OnInitialized();  // REQUIRED: subscribes to PropertyChanged
        ViewModel.Initialize();
    }
}
```

Use `ViewModelComponentBase<T>` for child components with `[Parameter] TViewModel ViewModel`.

## Creating a New Entity

### Step 1: Entity (Model/Entities/)

```csharp
public class Product : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? CategoryId { get; set; }
    public virtual Category? Category { get; set; }
}
```

### Step 2: Configuration (Model/Data/EntityConfigurations/)

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId);
    }
}
```

### Step 3: DbContext (Model/Data/ApplicationDbContext.cs)

```csharp
public DbSet<Product> Products { get; set; } = null!;
```

### Step 4: Migration

```bash
dotnet ef migrations add AddProduct --project ../Model/Model.csproj
dotnet ef database update --project ../Model/Model.csproj
```

### Step 5: ViewModel + Factory (ViewModel/Products/)

```csharp
// Factory - MUST be named {EntityName}ViewModelFactory
public class ProductViewModelFactory : IEntityViewModelFactory<Product, ProductViewModel>
{
    public ProductViewModel Create(Product entity, IUnitOfWork unitOfWork)
        => new ProductViewModel(entity, unitOfWork);
}

public class ProductViewModel : BaseViewModel, IEntityViewModel<Product>
{
    private readonly Product _product;
    private IntegerFieldViewModel? _idField;
    private StringFieldViewModel? _nameField;

    public ProductViewModel(Product product, IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _product = product;
    }

    public Product Model => _product;

    public IntegerFieldViewModel Id => _idField ??= new IntegerFieldViewModel(
        parent: this, getValue: () => _product.Id, setValue: v => _product.Id = v)
    { Label = "Id", ReadOnly = true };

    public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(
        parent: this, getValue: () => _product.Name, setValue: v => _product.Name = v)
    {
        Label = "Name",
        ValidationRules = rules => rules.NotEmpty().WithSeverity(Severity.Error)
    };
}
```

### Step 6: Register ViewModel (ViewModel/DependencyInjection.cs)

```csharp
services.AddScoped<ProductListViewModel>();
```

### Step 7: UI Components (ViewBlazor/Components/Products/)

Create `ProductView.razor` using existing field components:
```razor
<StringFieldView Field="@ViewModel.Name" />
```

## Conventions

### Naming
- Entity: `Product`
- ViewModel: `ProductViewModel`
- Factory: `ProductViewModelFactory` (same namespace as ViewModel)
- Configuration: `ProductConfiguration`
- UI Component: `ProductView.razor`
- List ViewModel: `ProductListViewModel`

### Code Patterns
- Lazy initialization: `Property => _field ??= new FieldViewModel(...)`
- Validation severity: `.WithSeverity(Severity.Error)` blocks save, `.WithSeverity(Severity.Warning)` allows save
- Computed fields: `IsComputed = true` (auto ReadOnly), `NotifyOnChange = new[] { "DependentProp" }`
- Reference fields: getValue returns ViewModel, setValue receives ViewModel, listQuery returns List<ViewModel>

### Dependency Injection
- ViewModels: **Scoped** (per Blazor circuit)
- Services: **Singleton** or **Scoped** as needed
- Chain: `ViewBlazor.Program` → `ViewModel.AddViewModels()` → `Model.AddModel()`

### Database
- SQLite: `BlazorApp.db` in ViewBlazor root
- Auto-migration on startup via `MigrateDatabaseAsync()`
- Lazy-loading proxies enabled

## UI Components Reference

| Component | FieldViewModel | Usage |
|-----------|----------------|-------|
| `StringFieldView` | `StringFieldViewModel` | Text input or dropdown |
| `IntegerFieldView` | `IntegerFieldViewModel` | Number input with +/- buttons |
| `BoolFieldView` | `BoolFieldViewModel` | Checkbox |
| `DateTimeFieldView` | `DateTimeFieldViewModel` | Date/time picker |
| `ReferenceFieldView` | `ReferenceFieldViewModel<T>` | Entity dropdown |
| `CommandView` | `CommandViewModel` | Button |
| `CommandViewGeneric` | `CommandViewModel<T>` | Button with parameter |
