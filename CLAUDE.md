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

### Key Patterns

**FieldViewModel Pattern**: All entity properties are wrapped in typed FieldViewModels (`StringFieldViewModel`, `IntegerFieldViewModel`, `BoolFieldViewModel`, `DateTimeFieldViewModel`, `ReferenceFieldViewModel<T>`) that provide:
- Lazy-loaded values via getter/setter delegates
- FluentValidation with Error/Warning severity separation
- UI metadata (Label, Hint, ReadOnly)
- Computed field support with auto-recalculation
- Automatic entity change tracking via `MarkAsModified()`

**Unit of Work**: `IUnitOfWork` manages DbContext, repositories, and validation. Call `SaveAll()` to validate and persist; it returns `List<ValidationError>?` on failure.

**ViewModelPageBase<T>**: Base class for Blazor pages that auto-injects ViewModel and subscribes to `PropertyChanged` for automatic `StateHasChanged()` calls.

**Factory Pattern**: Each entity has a corresponding `IEntityViewModelFactory<TEntity, TViewModel>` for creating ViewModels from entities.

### Dependency Injection

Each project has a `DependencyInjection.cs` file that registers its services. ViewModels are registered as Scoped (per Blazor circuit). Database auto-migrates on startup via `MigrateDatabaseAsync()`.

### Database

SQLite database (`BlazorApp.db`) in the ViewBlazor project root. Entity configurations are in `Model/Data/EntityConfigurations/`.

## Conventions

- Null-coalescing lazy field initialization: `public IntegerFieldViewModel CurrentCount => _currentCountField ??= new(...)`
- FluentValidation severity: `.WithSeverity(Severity.Error)` blocks save, `.WithSeverity(Severity.Warning)` allows save
- Computed fields: Set `IsComputed = true`, use `NotifyOnChange` to trigger dependent field updates
