# Implementation Plan: Architecture Modernization

**Branch**: `001-architecture-modernization` | **Date**: 2026-05-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-architecture-modernization/spec.md`

## Summary

Modernize the MedDevice desktop platform by improving domain types in IngestionApi (Measurement with `JsonElement` Value, enhanced validator), creating a shared `Presentation` library for ViewModels and services (CommunityToolkit.Mvvm, async HttpClient), refactoring the WPF DesktopApp to consume the shared layer, and building a parallel .NET MAUI app targeting Windows and Mac Catalyst that reuses the same ViewModels. Client apps interact with the API exclusively via HTTP — no project references between API and client projects. Comprehensive integration and unit test suites validate correctness.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0 (LTS) for API, Presentation, WPF, tests; .NET 9.0 for MAUI app

**SDK**: global.json specifies `8.0.401` with `rollForward: latestMajor` (resolves to SDK 10.0.x)

**Primary Dependencies**: CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.Http, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, System.Text.Json, Microsoft.Maui.Controls 9.0.120

**Storage**: In-memory (thread-safe `InMemoryStore`; no persistent DB for this iteration)

**Testing**: xUnit 2.x + FluentAssertions + NSubstitute; Microsoft.AspNetCore.Mvc.Testing for integration tests

**Target Platform**: Windows (WPF `net8.0-windows` + MAUI `net9.0-windows10.0.19041.0`); macOS (MAUI `net9.0-maccatalyst`); API runs on any platform (`net8.0`, static port `http://localhost:5100`)

**Project Type**: Multi-project solution: web service (IngestionApi, owns domain types) + shared presentation library (Presentation, client-side DTOs + ViewModels) + desktop app (DesktopApp/WPF) + cross-platform app (MauiApp) + console (DeviceSimulator). Client apps communicate with API via HTTP only — no project references to IngestionApi.

**Performance Goals**: DesktopApp displays data within 2s of launch; integration tests complete in <30s; unit tests in <5s

**Constraints**: UI must never block on network I/O; client apps have zero direct project references to IngestionApi

**Scale/Scope**: Demo/showcase scope — single user, local API, ~500 measurements max in-memory

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance | Notes |
|-----------|-----------|-------|
| I. Contract-First API Design | ✅ PASS | Domain library defines shared models; API contracts documented in `/contracts/` |
| II. Domain Isolation | ✅ PASS | Domain types (Measurement, Validator, Store) live in IngestionApi; clients use own DTOs via HTTP |
| III. MVVM & Presentation Separation | ✅ PASS | CommunityToolkit.Mvvm, DI, async HttpClient; code-behind has only InitializeComponent() |
| IV. Test-Driven Quality | ✅ PASS | Integration tests via WebApplicationFactory; unit tests for Domain + ViewModel |
| V. Cross-Platform Portability | ✅ PASS | MAUI app targets `net8.0-maccatalyst` + `net8.0-windows`; shared Presentation lib is platform-agnostic |
| VI. Dependency Injection & Simplicity | ✅ PASS | Built-in DI container; IHttpClientFactory; ILogger<T>; YAGNI respected |

**Gate result: PASS** — no violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-architecture-modernization/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── ingestion-api.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── IngestionApi/
│   ├── IngestionApi.csproj        # net8.0 web API (owns domain types)
│   ├── Program.cs                 # Minimal API endpoints + DI
│   ├── Measurement.cs             # Record type with JsonElement Value
│   ├── MeasurementValidator.cs    # Stateless validation
│   ├── IMeasurementStore.cs       # Store abstraction
│   └── InMemoryStore.cs           # Concrete store implementation
├── Presentation/
│   ├── Presentation.csproj        # net8.0 class library (shared ViewModels + services)
│   ├── Models/
│   │   └── MeasurementDto.cs     # Client-side DTO for API responses
│   ├── ViewModels/
│   │   └── MainViewModel.cs      # ObservableProperty, RelayCommand, ILogger<T>
│   └── Services/
│       └── MeasurementService.cs  # Typed HttpClient wrapper (calls API over HTTP)
├── DesktopApp/
│   ├── DesktopApp.csproj          # net8.0-windows; References Presentation only
│   ├── App.xaml / App.xaml.cs     # DI container setup, IHttpClientFactory registration
│   ├── MainWindow.xaml            # WPF bindings to shared MainViewModel
│   └── MainWindow.xaml.cs         # InitializeComponent() only
├── MauiApp/
│   ├── MauiApp.csproj             # net9.0-maccatalyst (conditional net9.0-windows); References Presentation only
│   ├── MauiProgram.cs             # DI setup via CreateMauiApp() builder
│   ├── App.xaml / App.xaml.cs     # MAUI app shell
│   ├── MainPage.xaml              # MAUI bindings to shared MainViewModel
│   ├── MainPage.xaml.cs           # InitializeComponent() only
│   └── Platforms/
│       ├── MacCatalyst/
│       │   ├── Program.cs         # UIApplication.Main entry point
│       │   ├── AppDelegate.cs     # MauiUIApplicationDelegate
│       │   └── Info.plist         # ATS config (NSAllowsLocalNetworking)
│       └── Windows/
│           └── App.xaml.cs        # MauiWinUIApplication
└── DeviceSimulator/
    ├── DeviceSimulator.csproj
    └── Program.cs                 # Posts HeartRate every 2s to http://localhost:5100 with console logging

tests/
├── Presentation.Tests/
│   ├── Presentation.Tests.csproj
│   └── MainViewModelTests.cs
└── IngestionApi.IntegrationTests/
    ├── IngestionApi.IntegrationTests.csproj
    └── MeasurementApiIntegrationTests.cs
```

**Structure Decision**: Multi-project .NET solution with a two-layer + API architecture:
1. **IngestionApi** (`net8.0`) — web API owning domain types (Measurement, Validator, Store)
2. **Presentation** (`net8.0`) — shared ViewModels, services, and client DTOs consumed by BOTH WPF and MAUI apps
3. **App hosts** — WPF (`net8.0-windows`) and MAUI (`net9.0-maccatalyst;net9.0-windows`) contain only platform-specific XAML views and DI bootstrap

Client apps interact with IngestionApi exclusively via HTTP calls through MeasurementService. No project references exist between API and client projects.

## Complexity Tracking

No constitution violations — table intentionally left empty.
