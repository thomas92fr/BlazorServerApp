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

This is a **.NET 10.0 Blazor Server** application using **advanced MVVM architecture** with CommunityToolkit.Mvvm, Repository pattern, and Factory pattern:

```
BlazorServerApp.slnx (Solution)
├── VueBlazor/          # Main web application (Blazor Server) - VIEW
│   ├── Pages/          # Routable Razor components
│   ├── Shared/         # Layout and shared components
│   └── Components/     # Reusable components
│       ├── Base/       # ViewModelComponentBase
│       ├── Person/     # PersonView
│       └── ViewModels/FieldViews/  # StringFieldView, IntegerFieldView, etc.
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
├── Model/              # Domain models, services and business logic - MODEL
│   ├── Entities/       # IEntity, Person
│   └── Services/       # IWeatherForecastService, WeatherForecastService
│
└── Infrastructure/     # Repository pattern, Factory pattern, Core interfaces
    ├── Repository/     # IRepository, InMemoryRepository, EntityEqualityComparer
    ├── Factory/        # IEntityViewModelFactory
    └── ViewModel/      # IViewModel, IEntityViewModel, IFieldViewModel, ValidationError
```

**Blazor Server Model:** Uses server-side rendering with real-time WebSocket communication (SignalR). Components execute on the server, and UI updates are sent to the browser over a persistent connection.

## MVVM Pattern Implementation

- **Model:** Contains domain entities, service interfaces AND implementations, business logic
  - Domain entities implement `IEntity` interface (provides `int Id` property)
  - Organized in subdirectories: `Entities/`, `Services/`
  - Services are registered via their interfaces in `Program.cs`
  - No external NuGet dependencies (pure domain layer)

- **Infrastructure:** Abstraction layer providing Repository and Factory patterns
  - **Repository Pattern:** `IRepository` interface with `InMemoryRepository` implementation
    - Registered as **Scoped** (one instance per user circuit)
    - Caches ViewModels and manages Entity ↔ ViewModel mapping
    - Change tracking via `MarkAsModified()`, `SaveAll()`, `DiscardChanges()`
  - **Factory Pattern:** `IEntityViewModelFactory<TEntity, TViewModel>` for decoupled ViewModel creation
    - Convention-based discovery: `{EntityName}ViewModelFactory` in `ViewModel.{EntityName}Plural` namespace
  - **Core Interfaces:** `IViewModel`, `IEntityViewModel<T>`, `IFieldViewModel`, `ValidationError`

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
  - Depends on Model, ViewModel, and Infrastructure projects

## Key Patterns

### Repository Pattern (Blazor-specific)
- `IRepository` registered as **Scoped** (one instance per user circuit, not Singleton)
  - Located in `Infrastructure/Repository/IRepository.cs:10`
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
- Corresponding Blazor views in `VueBlazor/Components/ViewModels/FieldViews/`

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
- Repository is **Scoped** for data isolation between users
- ViewModels are **Scoped** to share state within a circuit
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

**ViewModel Project Dependencies:**
- CommunityToolkit.Mvvm v8.4.0 (provides `[ObservableProperty]`, `[RelayCommand]`)
- FluentValidation v12.1.1 (field-level validation with Error/Warning severity)

**VueBlazor Project:**
- Blazor Server template
- Docker support configured (ports 80/443)
- User secrets enabled for local development
- Runs at https://localhost:7178 (HTTPS) or http://localhost:5116 (HTTP) in development
