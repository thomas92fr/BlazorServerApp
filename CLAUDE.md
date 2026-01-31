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

This is a **.NET 10.0 Blazor Server** application using **MVVM architecture** with CommunityToolkit.Mvvm:

```
BlazorServerApp.slnx (Solution)
├── VueBlazor/          # Main web application (Blazor Server) - VIEW
│   ├── Pages/          # Routable Razor components (Views)
│   └── Shared/         # Layout and shared components
├── Model/              # Domain models, services and business logic - MODEL
│   ├── WeatherForecast.cs
│   └── Services/
│       ├── IWeatherForecastService.cs (interface)
│       └── WeatherForecastService.cs (implementation)
└── ViewModel/          # ViewModels with CommunityToolkit.Mvvm - VIEWMODEL
    ├── CounterViewModel.cs
    └── WeatherForecastViewModel.cs
```

**Blazor Server Model:** Uses server-side rendering with real-time WebSocket communication (SignalR). Components execute on the server, and UI updates are sent to the browser over a persistent connection.

## MVVM Pattern Implementation

- **Model:** Contains domain entities, service interfaces AND implementations, business logic
  - No dependencies on other projects
  - Services are registered via their interfaces in `Program.cs`
- **ViewModel:** Uses `ObservableObject` for property change notifications and `RelayCommand` for commands
  - `[ObservableProperty]` generates properties with INotifyPropertyChanged
  - `[RelayCommand]` generates ICommand implementations
  - ViewModels depend only on Model project (no reference to VueBlazor)
  - Injected as Transient in components
- **View (VueBlazor):** Razor components inject ViewModels and bind to their properties
  - Contains only UI components, no business logic
  - Depends on Model and ViewModel projects

## Key Patterns

- **Dependency Injection:** Services and ViewModels registered in `Program.cs` and injected via `@inject`
- **Component Lifecycle:** ViewModels are initialized in `OnInitializedAsync()`
- **Commands:** Use `ViewModel.CommandName.Execute()` or `ViewModel.CommandName.Execute(parameter)` in Razor markup
- **Routing:** Components in `Pages/` with `@page` directive are routable

## Project Configuration

- Nullable reference types enabled
- Implicit usings enabled
- Docker support configured (ports 80/443)
- User secrets enabled for local development
