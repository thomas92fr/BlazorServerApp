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
├── Query/                         # JQL-like text query engine (Lexer, Parser, ExpressionBuilder)
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
├── Pages/                         # Routable pages (@page) + Login/Logout Razor Pages
├── _Imports.razor                 # Global imports (includes Radzen, Radzen.Blazor)
└── Program.cs

BlazorServerApp.ViewMCP/
├── Discovery/
│   ├── CollectionMetadata.cs      # Metadata records for discovered collections
│   ├── ViewModelDiscoveryService.cs # Scans RootViewModels for CollectionFieldViewModels
│   └── DynamicToolRegistrar.cs    # Creates MCP tools dynamically
└── DependencyInjection.cs         # AddViewMcp(), MapViewMcp()
```

## Key Patterns

### FieldViewModel Pattern

All entity properties are wrapped in typed FieldViewModels:

| Type | Usage |
|------|-------|
| `StringFieldViewModel` | Text properties |
| `PasswordFieldViewModel` | Password properties (masked input, hidden from MCP) |
| `IntegerFieldViewModel` | Numbers (includes +/- commands) |
| `IntegerSliderFieldViewModel` | Sliders (Min, Max, Step) |
| `DecimalFieldViewModel` | Decimal numbers (Format, Step, Min, Max, +/- commands) |
| `BoolFieldViewModel` | Checkboxes |
| `BoolSwitchFieldViewModel` | Toggle switches |
| `DateTimeFieldViewModel` | Date/time pickers |
| `TimeSpanFieldViewModel` | Duration pickers (ShowDays, ShowSeconds, Inline) |
| `ColorFieldViewModel` | Color picker (ShowHSV, ShowRGBA, ShowColors, ShowButton) |
| `HtmlFieldViewModel` | Rich-text HTML content (Height, UploadUrl) |
| `FileFieldViewModel` | File upload as base64 data URL (Accept filter, download) |
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

**PasswordFieldViewModel Example:**
```csharp
public PasswordFieldViewModel Password => _passwordField ??= new PasswordFieldViewModel(
    parent: this,
    getValue: () => _entity.Password,
    setValue: value => _entity.Password = value)
{
    Label = "Password",
    Hint = "Enter your password",
    ValidationRules = rules => rules
        .NotEmpty().WithMessage("Required.").WithSeverity(Severity.Error)
};
```
**MCP:** `GetRawValue()` returns `"***"` instead of the actual password.

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

**HtmlFieldViewModel Example:**
```csharp
public HtmlFieldViewModel Comment => _commentField ??= new HtmlFieldViewModel(
    parent: this,
    getValue: () => _entity.Comment,
    setValue: value => _entity.Comment = value)
{
    Label = "Comment",
    Height = "450px",           // CSS height for the editor (default "450px")
    UploadUrl = "upload/image"  // Server endpoint for image uploads (null = no upload button)
};
```

**FileFieldViewModel Example:**
```csharp
// Entity property is string? — stores combined "filename\nbase64dataurl" format.
// The View splits/combines transparently using FileFieldViewModel.ExtractFileName/ExtractDataUrl/Combine.
public FileFieldViewModel Cv => _cvField ??= new FileFieldViewModel(
    parent: this,
    getValue: () => _entity.Cv ?? string.Empty,
    setValue: value => _entity.Cv = string.IsNullOrEmpty(value) ? null : value)
{
    Label = "CV",
    Accept = ".pdf,.doc,.docx"  // File type filter (null = all types)
};
```
**Storage format:** `filename\ndata:mime;base64,...` in a single `string?` column. Requires `MaximumReceiveMessageSize` in Program.cs for large files (default: 10 MB).
**Download:** Built-in download icon button appears when a file is present.

### CollectionFieldViewModel Pattern

For managing collections of ViewModels with table rendering:

```csharp
public CollectionFieldViewModel<PersonViewModel> Persons => _personsField ??= new CollectionFieldViewModel<PersonViewModel>(
    parent: this,
    query: filterText => UnitOfWork.GetFilteredViewModelsFromTextQuery<Person, PersonViewModel>(filterText))
{
    Label = "Persons",
    AllowAdd = true,
    AllowDelete = true,
    AllowMultiSelect = true,   // Enable Ctrl+Click, Shift+Click selection
    AllowFilter = true,        // Enable JQL-like text filter bar
    CreateItem = () => UnitOfWork.GetNewViewModel<Person, PersonViewModel>(),
    OnItemAdded = vm => { /* Track new entity */ },
    OnItemDeleted = vm => UnitOfWork.DeleteEntity(vm.Model)
};
```

The `query` delegate receives a `string? filterText` parameter: `null` means load all, non-null means apply text filter. `GetFilteredViewModelsFromTextQuery` handles both cases internally.

**Features:**
- ObservableCollection with lazy loading via query
- Auto-generated columns from IFieldViewModel properties (ordered by `ColumnOrder`)
- Built-in commands: `AddCommand`, `DeleteCommand`, `RefreshCommand`, `DeleteSelectedCommand`
- Single selection (`SelectedItem`) and multi-selection (`SelectedItems`)
- Multi-select: Ctrl+Click toggle, Shift+Click range, checkbox column with select-all
- CRUD delegates: `CreateItem`, `OnItemAdded`, `OnItemDeleted`
- Permissions: `AllowAdd`, `AllowUpdate`, `AllowDelete`, `AllowMultiSelect`, `AllowInlineEdit`, `AllowFilter`
- Text filtering: JQL-like query syntax with `AllowFilter = true` (see Query Engine section)

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

// Filtered queries (JQL-like syntax, null = all)
var filtered = UnitOfWork.GetFilteredViewModelsFromTextQuery<Person, PersonViewModel>("age > 30 AND name contains \"John\"");

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
    public bool Deleted { get; set; }  // Required by IEntity
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
// UnitOfWork is only on IRootViewModel, not IViewModel.
// EntityViewModels access it via RootViewModel.UnitOfWork.

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
            query: filterText => UnitOfWork.GetFilteredViewModelsFromTextQuery<Product, ProductViewModel>(filterText))
        {
            AllowFilter = true,
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
- `ViewModelDiscoveryService`: **Singleton** - discovers RootViewModels for MCP tools
- `DynamicToolRegistrar`: **Singleton** - creates MCP tools at startup
- RootViewModels: **Not registered** - created manually via `new RootViewModel(unitOfWorkFactory.Create())`
- EntityViewModels: **Not registered** - created by factory when accessing entities
- Chain: `BlazorServerApp.ViewBlazor.Program` → `ViewModel.AddViewModels()` → `Model.AddModel()` + `ViewMCP.AddViewMcp()`

### Database
- SQLite: `BlazorApp.db` in BlazorServerApp.ViewBlazor root
- Auto-migration on startup via `MigrateDatabaseAsync()`
- Lazy-loading proxies enabled
- **Soft Delete**: All entities implement `IEntity.Deleted`. `DeleteEntity()` sets `Deleted = true` instead of removing the row. A global query filter in `ApplicationDbContext` automatically excludes soft-deleted entities from all queries.

### Authentication
- **Cookie + OpenID Connect** (Google) dual authentication
- Login/Logout are **Razor Pages** (`Pages/Login.cshtml`, `Pages/Logout.cshtml`), not Blazor components, because they need direct `HttpContext` access for `SignInAsync`/`SignOutAsync`
- `_Host.cshtml` is protected with `[Authorize]` — unauthenticated users redirect to `/Login`
- OIDC auto-provisions a local `User` entity from the email claim on first login
- OIDC secrets (`ClientId`, `ClientSecret`) go in `dotnet user-secrets`, not in `appsettings.json`
- MainLayout header displays current username and logout button via `<AuthorizeView>`
- Middleware order in `Program.cs`: `UseAuthentication()` → `UseAuthorization()` before `MapBlazorHub()`

## UI Framework

The application uses **Radzen Blazor** as the UI component library (no Bootstrap).

### Setup in Program.cs
```csharp
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB (for file uploads)
    });
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
| `PasswordFieldView` | `PasswordFieldViewModel` | RadzenPassword |
| `IntegerFieldView` | `IntegerFieldViewModel` | RadzenNumeric |
| `IntegerSliderFieldView` | `IntegerSliderFieldViewModel` | RadzenSlider |
| `DecimalFieldView` | `DecimalFieldViewModel` | RadzenNumeric, RadzenDropDown |
| `BoolFieldView` | `BoolFieldViewModel` | RadzenCheckBox |
| `BoolSwitchFieldView` | `BoolSwitchFieldViewModel` | RadzenSwitch |
| `ColorFieldView` | `ColorFieldViewModel` | RadzenColorPicker |
| `DateTimeFieldView` | `DateTimeFieldViewModel` | RadzenDatePicker |
| `TimeSpanFieldView` | `TimeSpanFieldViewModel` | RadzenTimeSpanPicker |
| `ReferenceFieldView` | `ReferenceFieldViewModel<T>` | RadzenDropDown |
| `HtmlFieldView` | `HtmlFieldViewModel` | RadzenHtmlEditor |
| `FileFieldView` | `FileFieldViewModel` | RadzenFileInput (TValue=string) |
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

## Query Engine (JQL-like Text Filtering)

The application includes a text query engine in `BlazorServerApp.Model/Query/` that parses JQL-like queries into `Expression<Func<TEntity, bool>>` for EF Core server-side filtering.

### Architecture

```
QueryEngine (facade)
  └── QueryLexer → Token[]
       └── QueryParser → AST (QueryNode)
            └── ExpressionBuilder → Expression<Func<TEntity, bool>>
```

### Supported Syntax

| Feature | Example |
|---------|---------|
| Comparison | `age = 30`, `age > 18`, `age <= 65` |
| String operators | `name contains "John"`, `name startsWith "A"`, `name endsWith "son"` |
| Null checks | `mentor is null`, `mentor is not null` |
| IN operator | `age in (25, 30, 35)` |
| Boolean logic | `age > 18 AND isTeacher = true` |
| OR logic | `name = "Alice" OR name = "Bob"` |
| NOT | `NOT isTeacher = true` |
| Parentheses | `(age > 18 OR age < 10) AND name contains "A"` |
| Navigation properties | `Mentor.Age = 42`, `Mentor.Name contains "John"` |
| Multi-level navigation | `Mentor.Mentor.Name = "Alice"` |

### Usage

Integrated via `IUnitOfWork.GetFilteredViewModelsFromTextQuery<TEntity, TViewModel>(string? queryText)`:
- If `queryText` is `null` or whitespace, returns all entities (equivalent to `GetAllViewModels`)
- Otherwise parses the query and applies it server-side via EF Core

Used by `CollectionFieldViewModel` when `AllowFilter = true`: the filter bar passes user input as `filterText` to the `query` delegate.

## MCP Server (Model Context Protocol)

The application exposes an MCP server at `/mcp` for AI assistant integration. Tools are **auto-discovered** from RootViewModels.

### Auto-Discovery System

The MCP server automatically generates tools by scanning:
1. All classes inheriting from `RootViewModel`
2. Their `CollectionFieldViewModel<T>` properties
3. The `IFieldViewModel` properties of the item type `T`

**Example:** `PersonListViewModel` with `CollectionFieldViewModel<PersonViewModel> Persons` automatically generates a `GetAllPersons` tool.

### How It Works

```
RootViewModel (PersonListViewModel)
  └── CollectionFieldViewModel<PersonViewModel> Persons
        └── PersonViewModel
              └── IFieldViewModel properties → serialized via GetRawValue()
```

**Key Components:**
- `ViewModelDiscoveryService` - Scans assemblies for RootViewModels and their collections
- `DynamicToolRegistrar` - Creates `McpServerTool` instances for each discovered entity
- `IFieldViewModel.GetRawValue()` - Returns raw value for JSON serialization

### Generated Tools

For each `CollectionFieldViewModel<T>` discovered, a `GetAll{CollectionName}` tool is generated:

| RootViewModel | Collection Property | Generated Tool |
|---------------|---------------------|----------------|
| `PersonListViewModel` | `Persons` | `GetAllPersons` |
| `ProductListViewModel` | `Products` | `GetAllProducts` |

### Tool Output Format

```json
{
  "items": [
    {
      "id": 1,
      "name": "John Doe",
      "age": 30,
      "isTeacher": false,
      "score": 85.5,
      "mentor": 2
    }
  ],
  "count": 1
}
```

**Serialization Rules:**
- Primitive types → direct value
- `DateTime` → ISO 8601 format
- `TimeSpan` → "hh:mm:ss" format
- `ReferenceFieldViewModel<T>` → referenced entity's ID (not full object)
- `CollectionFieldViewModel<T>` → count only
- `FileFieldViewModel` → file name only (not the base64 content)
- `PasswordFieldViewModel` → `"***"` (never exposes actual password)

### Adding MCP Support to New Entities

No additional configuration needed! When you create a new `RootViewModel` with a `CollectionFieldViewModel<T>`:

1. The `ViewModelDiscoveryService` automatically detects it at startup
2. A `GetAll{CollectionName}` tool is registered
3. The tool serializes all `IFieldViewModel` properties

### Configuration

**Program.cs:**
```csharp
// Add MCP server services
builder.Services.AddViewMcp();

// Map MCP endpoints
app.MapViewMcp();
```

**Endpoint:** `https://localhost:port/mcp`
