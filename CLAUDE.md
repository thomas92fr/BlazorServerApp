# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build BlazorServerApp.slnx

# Run the Blazor Server app
dotnet run --project BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj

# Run with hot reload for development
dotnet watch --project BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj

# EF Core migrations
dotnet ef migrations add <MigrationName> --project BlazorServerApp.Model/BlazorServerApp.Model.csproj --startup-project BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj
dotnet ef database update --project BlazorServerApp.Model/BlazorServerApp.Model.csproj --startup-project BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj
```

## Architecture

Three-layer .NET 10.0 Blazor Server application using MVVM pattern:

```
ViewBlazor (UI) → ViewModel → Model (Data)
```

- **Model**: Entity Framework Core 10.0 with SQLite, Unit of Work pattern, repositories
- **ViewModel**: MVVM with CommunityToolkit.Mvvm, FluentValidation for validation
- **ViewBlazor**: Blazor Server components with Radzen Blazor UI framework

### Project Structure

```
BlazorServerApp.Model/
├── Data/
│   ├── ApplicationDbContext.cs
│   └── EntityConfigurations/      # IEntityTypeConfiguration<T>
├── Entities/                      # Domain entities (implement IEntity)
├── Repositories/                  # IGenericRepository<T>, GenericRepository<T>
├── UnitOfWork/                    # IUnitOfWork, IUnitOfWorkFactory, UnitOfWork, UnitOfWorkFactory
├── ViewModels/                    # Interfaces: IFieldViewModel, IEntityViewModel, IViewModel, IRootViewModel
├── Factories/                     # IEntityViewModelFactory, DefaultEntityViewModelFactory
└── DependencyInjection.cs

BlazorServerApp.ViewModel/
├── Commons/
│   ├── Bases/                     # BaseViewModel, RootViewModel
│   └── Fields/                    # FieldViewModels, CommandViewModel
├── {EntityName}/                  # EntityViewModel (e.g., Persons/)
├── {ListName}ViewModel.cs         # Page-level RootViewModels (tabs)
└── DependencyInjection.cs

BlazorServerApp.ViewBlazor/
├── Components/
│   ├── Base/                      # ViewModelPageBase<T>, ViewModelComponentBase<T>
│   ├── Commons/Fields/            # StringFieldView, IntegerFieldView, etc.
│   ├── Tabs/                      # TabBar.razor (RadzenTabs)
│   └── {EntityName}/              # EntityView.razor
├── Shared/
│   ├── MainLayout.razor           # RadzenLayout with collapsible sidebar
│   └── NavMenu.razor              # RadzenPanelMenu navigation
├── Pages/                         # Routable pages (@page)
├── _Imports.razor                 # Global imports (includes Radzen, Radzen.Blazor)
└── Program.cs
```

## Key Patterns

### FieldViewModel Pattern

All entity properties are wrapped in typed FieldViewModels:

| Type | Usage |
|------|-------|
| `StringFieldViewModel` | Text properties |
| `IntegerFieldViewModel` | Numbers (includes +/- commands) |
| `IntegerSliderFieldViewModel` | Sliders (Min, Max, Step) |
| `DecimalFieldViewModel` | Decimal numbers (Format, Step, Min, Max, +/- commands) |
| `BoolFieldViewModel` | Checkboxes |
| `DateTimeFieldViewModel` | Date/time pickers |
| `TimeSpanFieldViewModel` | Duration pickers (ShowDays, ShowSeconds, Inline) |
| `ReferenceFieldViewModel<T>` | Entity references (dropdowns) |
| `CollectionFieldViewModel<T>` | Collections (table view with CRUD) |

**Features:**
- Lazy-loaded values via getter/setter delegates
- FluentValidation with Error/Warning severity separation
- UI metadata (Label, Hint, ReadOnly, ColumnWidth, ColumnOrder)
- Form grouping (FormGroupHeader, FormGroupOrder)
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

**DecimalFieldViewModel Example:**
```csharp
public DecimalFieldViewModel Score => _scoreField ??= new DecimalFieldViewModel(
    parent: this,
    getValue: () => _entity.Score,
    setValue: value => _entity.Score = value)
{
    Label = "Score",
    Format = "#.00",    // RadzenNumeric format
    Step = 0.1m,
    Min = 0m,
    Max = 100m
};
```

**IntegerSliderFieldViewModel Example:**
```csharp
public IntegerSliderFieldViewModel Satisfaction => _satisfactionField ??= new IntegerSliderFieldViewModel(
    parent: this,
    getValue: () => _entity.Satisfaction,
    setValue: value => _entity.Satisfaction = value)
{
    Label = "Satisfaction",
    Min = 0,
    Max = 100,
    Step = 5
};
```

**TimeSpanFieldViewModel Example:**
```csharp
public TimeSpanFieldViewModel WorkDuration => _workDurationField ??= new TimeSpanFieldViewModel(
    parent: this,
    getValue: () => _entity.WorkDuration,
    setValue: value => _entity.WorkDuration = value)
{
    Label = "Work Duration",
    ShowDays = true,
    ShowSeconds = false,
    Placeholder = "Select duration"
};
```

### CollectionFieldViewModel Pattern

For managing collections of ViewModels with table rendering:

```csharp
public CollectionFieldViewModel<PersonViewModel> Persons => _personsField ??= new CollectionFieldViewModel<PersonViewModel>(
    parent: this,
    query: () => UnitOfWork.GetAllViewModels<Person, PersonViewModel>())
{
    Label = "Persons",
    AllowAdd = true,
    AllowDelete = true,
    AllowMultiSelect = true,   // Enable Ctrl+Click, Shift+Click selection
    CreateItem = () => UnitOfWork.GetNewViewModel<Person, PersonViewModel>(),
    OnItemAdded = vm => { /* Track new entity */ },
    OnItemDeleted = vm => UnitOfWork.DeleteEntity(vm.Model)
};
```

**Features:**
- ObservableCollection with lazy loading via query
- Auto-generated columns from IFieldViewModel properties (ordered by `ColumnOrder`)
- Built-in commands: `AddCommand`, `DeleteCommand`, `RefreshCommand`, `DeleteSelectedCommand`
- Single selection (`SelectedItem`) and multi-selection (`SelectedItems`)
- Multi-select: Ctrl+Click toggle, Shift+Click range, checkbox column with select-all
- CRUD delegates: `CreateItem`, `OnItemAdded`, `OnItemDeleted`
- Permissions: `AllowAdd`, `AllowUpdate`, `AllowDelete`, `AllowMultiSelect`

### AutoFormView Component

Automatically generates a form from any ViewModel by discovering IFieldViewModel properties:

```razor
<AutoFormView ViewModel="@SelectedPerson" />

@* With options *@
<AutoFormView ViewModel="@SelectedPerson"
              ExcludeFields="@(new[] { "Id" })"
              RespectHiddenInColumn="false" />
```

**Parameters:**
- `ViewModel` - The ViewModel instance to render
- `IncludeFields` - Only show these field names (optional)
- `ExcludeFields` - Hide these field names (optional)
- `RespectHiddenInColumn` - If true, honors `HiddenInColumn` property (default: false)

**Form Grouping:**
Fields are grouped using `FormGroupHeader` and ordered by `FormGroupOrder`. Groups are rendered in `RadzenCard`:
```csharp
public StringFieldViewModel Name => _nameField ??= new StringFieldViewModel(...)
{
    Label = "Name",
    FormGroupHeader = "Identification",  // Group name → wrapped in RadzenCard
    FormGroupOrder = 1,                   // Group display order
    ColumnOrder = 2                       // Field order within group
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
    style: CommandStyle.Primary      // Default, Primary, Success, Danger, Warning, Info, Light, Dark
);
```

Parameterized version: `CommandViewModel<T>` with `execute: (T param) => ...`

### RootViewModel Pattern (Multi-Tab Architecture)

`RootViewModel` is the base class for tab-level ViewModels. Each tab has its own isolated `IUnitOfWork` and `DbContext`:

```csharp
public class ProductListViewModel : RootViewModel
{
    public ProductListViewModel(IUnitOfWork unitOfWork, ILogger? logger = null)
        : base(unitOfWork, logger)
    {
        Title = "Products";
    }

    // SaveCommand and DiscardCommand are inherited from RootViewModel
    // ValidationErrors and StatusMessage are inherited

    public CollectionFieldViewModel<ProductViewModel> Products => _productsField ??= ...;
}
```

**Features:**
- `SaveCommand` - Validates and saves all changes
- `DiscardCommand` - Reverts all unsaved changes (enabled only when HasChanges)
- `ValidationErrors` - List of errors from last save attempt
- `StatusMessage` - Success/info message
- `HasChanges` - Tracks unsaved changes
- `Dispose()` - Disposes the owned UnitOfWork

**Creating a RootViewModel in a Blazor page:**
```razor
@page "/products"
@inject IUnitOfWorkFactory UnitOfWorkFactory
@implements IDisposable

@code {
    private ProductListViewModel? ViewModel;

    protected override void OnInitialized()
    {
        var unitOfWork = UnitOfWorkFactory.Create();  // Isolated UnitOfWork for this tab
        ViewModel = new ProductListViewModel(unitOfWork);
        ViewModel.PropertyChanged += (s, e) => InvokeAsync(StateHasChanged);
    }

    public void Dispose() => ViewModel?.Dispose();
}
```

### Unit of Work and Factory

`IUnitOfWorkFactory` creates isolated `IUnitOfWork` instances for each tab:

```csharp
// Inject factory
@inject IUnitOfWorkFactory UnitOfWorkFactory

// Create isolated UnitOfWork for a tab
var unitOfWork = UnitOfWorkFactory.Create();
var viewModel = new ProductListViewModel(unitOfWork);
```

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

**Tab Isolation:** Each `IUnitOfWork` has its own `DbContext` and ViewModel cache. Changes in one tab don't affect other tabs until saved to the database.

### Blazor Page Patterns

**For RootViewModels (tabs with isolated data):** Create ViewModel manually via factory:

```razor
@page "/persons"
@inject IUnitOfWorkFactory UnitOfWorkFactory
@using System.ComponentModel
@implements IDisposable

@code {
    private PersonListViewModel? ViewModel;

    protected override void OnInitialized()
    {
        var unitOfWork = UnitOfWorkFactory.Create();
        ViewModel = new PersonListViewModel(unitOfWork);
        ViewModel.PropertyChanged += OnPropertyChanged;
        ViewModel.Initialize();
    }

    private void OnPropertyChanged(object? s, PropertyChangedEventArgs e)
        => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.Dispose();
        }
    }
}
```

**For child components:** Use `ViewModelComponentBase<T>` with `[Parameter] TViewModel ViewModel`.

## Creating a New Entity

### Step 1: Entity (BlazorServerApp.Model/Entities/)

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

### Step 2: Configuration (BlazorServerApp.Model/Data/EntityConfigurations/)

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

### Step 3: DbContext (BlazorServerApp.Model/Data/ApplicationDbContext.cs)

```csharp
public DbSet<Product> Products { get; set; } = null!;
```

### Step 4: Migration

```bash
dotnet ef migrations add AddProduct --project ../Model/BlazorServerApp.Model.csproj
dotnet ef database update --project ../Model/BlazorServerApp.Model.csproj
```

### Step 5: EntityViewModel (BlazorServerApp.ViewModel/Products/)

```csharp
// NOTE: Factory is auto-generated by DefaultEntityViewModelFactory.
// EntityViewModels receive IRootViewModel for access to tab context.

public class ProductViewModel : BaseViewModel, IEntityViewModel<Product>
{
    private readonly Product _product;
    private IntegerFieldViewModel? _idField;
    private StringFieldViewModel? _nameField;

    public ProductViewModel(Product product, IRootViewModel rootViewModel, ILogger? logger = null)
        : base(rootViewModel, logger)
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

### Step 6: RootViewModel (BlazorServerApp.ViewModel/ProductListViewModel.cs)

```csharp
// RootViewModel for the Products tab - NOT registered in DI
public class ProductListViewModel : RootViewModel
{
    public ProductListViewModel(IUnitOfWork unitOfWork, ILogger? logger = null)
        : base(unitOfWork, logger)
    {
        Title = "Products";
    }

    public CollectionFieldViewModel<ProductViewModel> Products => _productsField ??=
        new CollectionFieldViewModel<ProductViewModel>(
            parent: this,
            query: () => UnitOfWork.GetAllViewModels<Product, ProductViewModel>())
        {
            CreateItem = () => UnitOfWork.GetNewViewModel<Product, ProductViewModel>(),
            OnItemDeleted = vm => UnitOfWork.DeleteEntity(vm.Model)
        };
}
```

### Step 7: UI Components (BlazorServerApp.ViewBlazor/Components/Products/)

Create `ProductView.razor` using existing field components:
```razor
<StringFieldView Field="@ViewModel.Name" />
```

## Conventions

### Naming
- Entity: `Product`
- EntityViewModel: `ProductViewModel` (receives `IRootViewModel` in constructor)
- RootViewModel: `ProductListViewModel` (inherits `RootViewModel`, owns `IUnitOfWork`)
- Factory: `ProductViewModelFactory` (optional, same namespace as ViewModel - auto-generated if not provided)
- Configuration: `ProductConfiguration`
- UI Component: `ProductView.razor`

### Code Patterns
- Lazy initialization: `Property => _field ??= new FieldViewModel(...)`
- Validation severity: `.WithSeverity(Severity.Error)` blocks save, `.WithSeverity(Severity.Warning)` allows save
- Computed fields: `IsComputed = true` (auto ReadOnly), `NotifyOnChange = new[] { "DependentProp" }`
- Reference fields: getValue returns ViewModel, setValue receives ViewModel, listQuery returns List<ViewModel>
- Table columns: `ColumnOrder` (sort order), `ColumnWidth` (CSS width), `HiddenInColumn` (exclude from table)
- Form groups: `FormGroupHeader` (group name), `FormGroupOrder` (group order)

### Dependency Injection
- `IUnitOfWorkFactory`: **Singleton** - creates isolated UnitOfWork per tab
- `IDbContextFactory<ApplicationDbContext>`: **Singleton** - used by UnitOfWorkFactory
- RootViewModels: **Not registered** - created manually via `new RootViewModel(unitOfWorkFactory.Create())`
- EntityViewModels: **Not registered** - created by factory when accessing entities
- Chain: `BlazorServerApp.ViewBlazor.Program` → `ViewModel.AddViewModels()` → `Model.AddModel()`

### Database
- SQLite: `BlazorApp.db` in BlazorServerApp.ViewBlazor root
- Auto-migration on startup via `MigrateDatabaseAsync()`
- Lazy-loading proxies enabled

## UI Framework

The application uses **Radzen Blazor** as the UI component library (no Bootstrap).

### Setup in Program.cs
```csharp
builder.Services.AddRadzenComponents();
```

### MainLayout Structure
```razor
<RadzenLayout>
    <RadzenHeader>
        <RadzenSidebarToggle />
    </RadzenHeader>
    <RadzenSidebar @bind-Expanded="@sidebarExpanded">
        <NavMenu />  <!-- Uses RadzenPanelMenu -->
    </RadzenSidebar>
    <RadzenBody>
        <TabBar />  <!-- Uses RadzenTabs -->
    </RadzenBody>
</RadzenLayout>
<RadzenComponents />  <!-- Required for tooltips, dialogs -->
```

### Theme
```razor
<RadzenTheme Theme="material" />
```

### Common Radzen Services
- `TooltipService` - For field hints via `TooltipService.Open()`
- `DialogService` - For confirmation dialogs via `DialogService.Confirm()`

## UI Components Reference

| Component | FieldViewModel | Radzen Component |
|-----------|----------------|------------------|
| `StringFieldView` | `StringFieldViewModel` | RadzenTextBox, RadzenDropDown, RadzenAutoComplete |
| `IntegerFieldView` | `IntegerFieldViewModel` | RadzenNumeric |
| `IntegerSliderFieldView` | `IntegerSliderFieldViewModel` | RadzenSlider |
| `DecimalFieldView` | `DecimalFieldViewModel` | RadzenNumeric, RadzenDropDown |
| `BoolFieldView` | `BoolFieldViewModel` | RadzenCheckBox |
| `DateTimeFieldView` | `DateTimeFieldViewModel` | RadzenDatePicker |
| `TimeSpanFieldView` | `TimeSpanFieldViewModel` | RadzenTimeSpanPicker |
| `ReferenceFieldView` | `ReferenceFieldViewModel<T>` | RadzenDropDown |
| `CollectionFieldView` | `CollectionFieldViewModel<T>` | RadzenDataGrid |
| `AutoFormView` | Any ViewModel | RadzenCard (groups), RadzenStack |
| `CommandView` | `CommandViewModel` | RadzenButton |
| `CommandViewGeneric` | `CommandViewModel<T>` | RadzenButton |

### Field View Pattern (Radzen)
All field views use `RadzenFormField` with `Variant.Outlined`:
```razor
<RadzenFormField Text="@Field?.Label" Variant="Variant.Outlined" class="w-100">
    <End>
        <RadzenIcon Icon="info" MouseEnter="@ShowHintTooltip" />
    </End>
    <ChildContent>
        <RadzenTextBox ... />
    </ChildContent>
</RadzenFormField>

@if (!string.IsNullOrEmpty(Field?.Error))
{
    <RadzenAlert AlertStyle="AlertStyle.Danger" ... />
}
```
