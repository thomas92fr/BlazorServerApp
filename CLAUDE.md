# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the solution
dotnet build

# Run the web application
dotnet run --project VueBlazor

# Publish for production
dotnet publish -c Release
```

The application runs at https://localhost:7178 (HTTPS) or http://localhost:5116 (HTTP) in development.

## Architecture Overview

This is a **.NET 10.0 Blazor Server** application using **advanced MVVM architecture** with CommunityToolkit.Mvvm, Repository pattern, and Factory pattern.

**Architecture Design:** The solution uses a **consolidated 3-tier architecture** where infrastructure abstractions (Repository, Factory, ViewModel interfaces) are part of the Model layer, creating a cleaner dependency graph and better cohesion.

```
BlazorServerApp.slnx (Solution)
├── VueBlazor/          # Main web application (Blazor Server) - VIEW
│   ├── Pages/          # Routable Razor components
│   ├── Shared/         # Layout and shared components
│   └── Components/     # Reusable components
│       ├── Base/       # ViewModelComponentBase
│       ├── Persons/    # PersonView
│       └── Commons/
│           └── Fields/  # StringFieldView, IntegerFieldView, etc.
│
├── ViewModel/          # ViewModels with CommunityToolkit.Mvvm - VIEWMODEL
│   ├── CounterViewModel.cs
│   ├── WeatherForecastViewModel.cs
│   ├── PersonListViewModel.cs
│   ├── Commons/
│   │   ├── Bases/      # BaseViewModel
│   │   └── Fields/     # FieldViewModel<T>, StringFieldViewModel, etc.
│   └── Persons/        # PersonViewModel, PersonViewModelFactory
│
└── Model/              # Domain models, services, business logic, and infrastructure - MODEL
    ├── Entities/       # IEntity, Person
    ├── Services/       # IWeatherForecastService, WeatherForecastService
    ├── Repositories/   # IRepository, InMemoryRepository, EntityEqualityComparer
    ├── Factories/      # IEntityViewModelFactory
    └── ViewModels/     # IViewModel, IEntityViewModel, IFieldViewModel, ValidationError
```

**Blazor Server Model:** Uses server-side rendering with real-time WebSocket communication (SignalR). Components execute on the server, and UI updates are sent to the browser over a persistent connection.

**Project Dependencies:**
```
VueBlazor
├── Model.csproj
└── ViewModel.csproj
    └── Model.csproj

Model.csproj (no dependencies)
```
This clean dependency graph ensures:
- **Model** is the foundation layer (domain + infrastructure abstractions)
- **ViewModel** depends only on Model for interfaces and entities
- **VueBlazor** depends on both Model and ViewModel for complete MVVM implementation

## Project Structure Details

### Model Project
The Model project serves as the **foundation layer** containing:
- **Domain entities** with the IEntity interface
- **Business services** for application logic
- **Infrastructure abstractions** (Repository, Factory, ViewModel interfaces)
- **No external dependencies** except Microsoft.Extensions.Logging.Abstractions

This consolidation provides:
- Clear separation between interface contracts (Model) and implementations (ViewModel/Services)
- Single source of truth for all infrastructure abstractions
- Simplified dependency management

### ViewModel Project
Contains **presentation logic implementations**:
- Concrete ViewModels using CommunityToolkit.Mvvm
- FieldViewModel implementations with FluentValidation
- Factory implementations for creating ViewModels
- Depends on Model for interfaces and entities

### VueBlazor Project
The **user interface layer** with Blazor Server:
- Razor components for UI rendering
- ViewModelComponentBase for bridging PropertyChanged to StateHasChanged
- FieldView components for reusable field rendering
- Depends on Model and ViewModel projects

## MVVM Pattern Implementation

- **Model:** Contains domain entities, services, business logic, and infrastructure abstractions
  - **Entities:** Domain entities implement `IEntity` interface (provides `int Id` property)
  - **Services:** Service interfaces and implementations for business logic
  - **Repository:** `IRepository` interface with `InMemoryRepository` implementation
    - Registered as **Scoped** (one instance per user circuit)
    - Caches ViewModels and manages Entity ↔ ViewModel mapping
    - Change tracking via `MarkAsModified()`, `SaveAll()`, `DiscardChanges()`
  - **Factory:** `IEntityViewModelFactory<TEntity, TViewModel>` for decoupled ViewModel creation
    - Convention-based discovery: `{EntityName}ViewModelFactory` in `ViewModel.{EntityName}Plural` namespace
  - **ViewModel Interfaces:** `IViewModel`, `IEntityViewModel<T>`, `IFieldViewModel`, `ValidationError`
  - **Dependencies:** Microsoft.Extensions.Logging.Abstractions v10.0.2

- **ViewModel:** Uses CommunityToolkit.Mvvm with advanced patterns
  - **BaseViewModel:** All ViewModels inherit from this (combines `ObservableObject` + `IViewModel`)
    - Injects `IRepository` and `ILogger` (optional)
    - Provides `IsBusy` property and `ExecuteAsync()` helper for async operations
  - **FieldViewModel Pattern:** Generic wrapper for entity properties with validation, lazy loading, and metadata
    - Typed variants: `StringFieldViewModel`, `IntegerFieldViewModel`, `BoolFieldViewModel`, `DateTimeFieldViewModel`
    - FluentValidation integration with Error/Warning severity separation
  - **Entity-specific ViewModels:** Organized in subdirectories (e.g., `Persons/`)
  - **Factory Convention:** Each entity type has a corresponding factory (e.g., `PersonViewModelFactory`)
  - **Dependencies:** CommunityToolkit.Mvvm v8.4.0, FluentValidation v12.1.1
  - Registered as **Scoped** (shared within circuit, not per component)

- **View (VueBlazor):** Razor components with ViewModelComponentBase pattern
  - **ViewModelComponentBase:** Critical pattern that bridges `PropertyChanged` → `StateHasChanged`
    - Blazor requires explicit `StateHasChanged()` calls (no automatic WPF-style databinding)
    - Usage: `@inherits ViewModelComponentBase<TViewModel>`
  - **FieldView Components:** Reusable views for FieldViewModels (e.g., `StringFieldView`, `IntegerFieldView`)
  - ViewModels injected as **Scoped** and initialized in `OnInitializedAsync()`
  - Depends on Model and ViewModel projects

## Key Patterns

### Repository Pattern (Blazor-specific)
- `IRepository` registered as **Scoped** (one instance per user circuit, not Singleton)
  - Located in `Model/Repositories/IRepository.cs:10`
- Caches ViewModels and manages Entity ↔ ViewModel mapping
- Change tracking: `MarkAsModified()`, `SaveAll()`, `HasChanges()`, `DiscardChanges()`
- Factory discovery: Automatically finds `IEntityViewModelFactory<,>` implementations via reflection
- Methods: `GetViewModel()`, `GetAllViewModels()`, `GetNewViewModel()`, `DeleteEntity()`

### Factory Pattern
- Convention: `{EntityName}ViewModelFactory` implements `IEntityViewModelFactory<TEntity, TViewModel>`
- Located in `ViewModel.{EntityName}Plural` namespace (e.g., `ViewModel.Persons.PersonViewModelFactory`)
- Discovered automatically by `InMemoryRepository` via reflection
- Decouples ViewModel creation from Repository, enabling testability and flexibility
- Example: `PersonViewModelFactory` in `ViewModel/Persons/PersonViewModelFactory.cs`

### FieldViewModel Pattern
- Generic wrapper for entity properties: `FieldViewModel<T>`
  - Lazy loading via `getValue()`/`setValue()` callbacks
  - FluentValidation integration with Error/Warning severity separation
  - UI metadata: `Label`, `Hint`, `ReadOnly`
  - List support for dropdowns via `listQuery()` and `List` property
  - Bidirectional binding: Automatically syncs with underlying entity
  - Calls `MarkAsModified()` on value change
- Typed variants: `StringFieldViewModel`, `IntegerFieldViewModel`, `BoolFieldViewModel`, `DateTimeFieldViewModel`
- Located in `ViewModel/Commons/Fields/`
- Corresponding Blazor views in `VueBlazor/Components/Commons/Fields/`

### ViewModelComponentBase Pattern (CRITICAL for Blazor)
- Base class for Blazor components using ViewModels
  - Located in `VueBlazor/Components/Base/ViewModelComponentBase.cs:21`
- Subscribes to ViewModel's `PropertyChanged` event
- Automatically calls `StateHasChanged()` to refresh UI (essential pattern for Blazor)
- Blazor difference from WPF: No automatic databinding, requires explicit `StateHasChanged()`
- Usage: `@inherits ViewModelComponentBase<TViewModel>`

### BaseViewModel
- Base class for all application ViewModels
  - Located in `ViewModel/Commons/Bases/BaseViewModel.cs:17`
- Inherits from `ObservableObject` (INotifyPropertyChanged) and implements `IViewModel`
- Injects `IRepository` and `ILogger` (optional)
- Provides `IsBusy` property for loading indicators
- Provides `ExecuteAsync()` helper for async operations with automatic IsBusy management

### Dependency Injection

**Required using statements:**
```csharp
using Model.Repositories;      // IRepository, InMemoryRepository
using Model.Services;          // IWeatherForecastService, WeatherForecastService
using ViewModel;               // ViewModels
```

**Service registration in Program.cs:**
```csharp
// Repository - Scoped per user circuit (not Singleton!)
builder.Services.AddScoped<IRepository, InMemoryRepository>();

// Services - Singleton (shared across all users)
builder.Services.AddSingleton<IWeatherForecastService, WeatherForecastService>();

// ViewModels - Scoped (shared within circuit, not per component)
builder.Services.AddScoped<CounterViewModel>();
builder.Services.AddScoped<WeatherForecastViewModel>();
builder.Services.AddScoped<PersonListViewModel>();
```

**Lifetime explanation:**
- **Repository (Scoped):** One instance per user circuit, ensures data isolation between concurrent users
- **Services (Singleton):** Shared across all users for stateless operations
- **ViewModels (Scoped):** Shared within a circuit, not per component instance
- ViewModels initialized in component's `OnInitializedAsync()`

### Other Patterns
- **Component Lifecycle:** ViewModels initialized in `OnInitializedAsync()` (not constructor)
- **Commands:** Use `@onclick="ViewModel.CommandName.Execute"` or `@onclick="() => ViewModel.CommandName.Execute(parameter)"`
- **Routing:** Components in `Pages/` with `@page` directive are routable

## Blazor Server Specifics

This application adapts MVVM patterns for Blazor Server. Key differences from WPF:

**Connection Model:**
- SignalR WebSocket (persistent connection per user) vs WPF desktop app
- Each connection gets its own **Scoped** Repository instance (isolated data per user)

**UI Updates:**
- Blazor requires explicit `StateHasChanged()` calls (no automatic WPF DataBinding)
- Solution: `ViewModelComponentBase` pattern bridges `PropertyChanged` → `StateHasChanged`

**Component Lifecycle:**
- ViewModels initialized in `OnInitializedAsync()` (not constructor like WPF)
- ViewModels are **Scoped** (shared within circuit, not per component instance)

**Repository Lifetime:**
- **Scoped** (one per user circuit) vs WPF Singleton (one per application)
- Ensures data isolation between concurrent users

**Command Execution:**
```razor
@* Simple command *@
<button @onclick="ViewModel.SaveCommand.Execute">Save</button>

@* Command with parameter *@
<button @onclick="() => ViewModel.DeleteCommand.Execute(item)">Delete</button>

@* Async commands work natively with Blazor's rendering cycle *@
<button @onclick="ViewModel.LoadDataCommand.Execute" disabled="@ViewModel.IsBusy">Load</button>
```

**Field Binding:**
```razor
@* Use dedicated FieldView components *@
<StringFieldView Field="@ViewModel.NameField" />
<IntegerFieldView Field="@ViewModel.AgeField" />
<BoolFieldView Field="@ViewModel.IsTeacherField" />
<DateTimeFieldView Field="@ViewModel.StartDateTimeField" />
```

**No Template Selectors Needed:**
- WPF uses DataTemplateSelectors for polymorphic UI
- Blazor components directly reference ViewModels (no XAML template selection)

## Project Configuration

**All Projects:**
- Target Framework: .NET 10.0
- Nullable reference types: enabled
- Implicit usings: enabled

**Model Project Dependencies:**
- Microsoft.Extensions.Logging.Abstractions v10.0.2 (logging support for InMemoryRepository)

**ViewModel Project Dependencies:**
- CommunityToolkit.Mvvm v8.4.0 (provides `[ObservableProperty]`, `[RelayCommand]`)
- FluentValidation v12.1.1 (field-level validation with Error/Warning severity)

**VueBlazor Project:**
- Blazor Server template
- Docker support configured (ports 80/443)
- User secrets enabled for local development
- Runs at https://localhost:7178 (HTTPS) or http://localhost:5116 (HTTP) in development

## Namespaces Reference

**Model Project:**
- `Model.Entities` - Domain entities (IEntity, Person, WeatherForecast)
- `Model.Services` - Business services (IWeatherForecastService, WeatherForecastService)
- `Model.Repositories` - Data access abstractions (IRepository, InMemoryRepository, EntityEqualityComparer)
- `Model.Factories` - Factory pattern (IEntityViewModelFactory<,>)
- `Model.ViewModels` - ViewModel interfaces (IViewModel, IEntityViewModel<>, IFieldViewModel, ValidationError)

**ViewModel Project:**
- `ViewModel` - Root ViewModels (CounterViewModel, WeatherForecastViewModel, PersonListViewModel)
- `ViewModel.Commons.Bases` - Base classes (BaseViewModel)
- `ViewModel.Commons.Fields` - Field ViewModels (FieldViewModel<T>, StringFieldViewModel, etc.)
- `ViewModel.Persons` - Entity-specific ViewModels (PersonViewModel, PersonViewModelFactory)

**VueBlazor Project:**
- `VueBlazor.Components.Base` - Base components (ViewModelComponentBase<T>)
- `VueBlazor.Components.Commons.Fields` - Field view components (StringFieldView, IntegerFieldView, etc.)
- `VueBlazor.Components.Persons` - Entity-specific views (PersonView)
- `VueBlazor.Pages` - Routable pages (Counter, Weather, PersonList)
- `VueBlazor.Shared` - Shared layout components (MainLayout, NavMenu)

## Common Import Patterns

**In ViewModels (.cs files):**
```csharp
using Model.Entities;           // For entity types
using Model.Repositories;       // For IRepository
using Model.ViewModels;         // For IViewModel, IEntityViewModel<>, ValidationError
using Model.Factories;          // For IEntityViewModelFactory<,> (in factories)
using ViewModel.Commons.Bases;  // For BaseViewModel
using ViewModel.Commons.Fields; // For FieldViewModel<T>
using CommunityToolkit.Mvvm.ComponentModel;  // For [ObservableProperty]
using CommunityToolkit.Mvvm.Input;           // For [RelayCommand]
using FluentValidation;         // For validation rules
```

**In Razor components (.razor files):**
```razor
@using Model.Repositories
@using Model.ViewModels
@using ViewModel
@using ViewModel.Persons
@using VueBlazor.Components.Base
@using VueBlazor.Components.Commons.Fields
```

**Note:** Most common using statements are in `_Imports.razor` for global availability.
