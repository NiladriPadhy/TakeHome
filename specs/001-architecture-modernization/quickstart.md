# Quickstart: Architecture Modernization

## Prerequisites

- .NET 8 SDK (8.0.401+) — global.json with `rollForward: latestMajor` also supports .NET 9/10 SDKs
- .NET 9 workload for MAUI (`dotnet workload install maui`)
- Windows 10+ (for WPF DesktopApp) or macOS 12+ (for MAUI Mac Catalyst)
- IDE: Visual Studio 2022+ or VS Code with C# Dev Kit

## Build

```bash
# From repository root
dotnet build TakeHome.sln
```

## Run the API

```bash
cd src/IngestionApi
dotnet run
# Listening on http://localhost:5100
```

Verify health:
```bash
curl http://localhost:5100/healthz
# {"status":"healthy"}
```

## Run the Device Simulator

```bash
# In a separate terminal
cd src/DeviceSimulator
dotnet run
# Sends HeartRate measurements every 2s to http://localhost:5100
# Console output: [HH:mm:ss] Posted HeartRate=XX bpm → 202
```

## Run the Desktop App (WPF — Windows only)

```bash
# In a separate terminal (Windows only)
cd src/DesktopApp
dotnet run
# Opens Vitals Monitor window, auto-polls measurements every 1 second
```

## Run the MAUI App (macOS or Windows)

```bash
# macOS (Mac Catalyst)
cd src/MauiApp
dotnet build -f net9.0-maccatalyst
open bin/Debug/net9.0-maccatalyst/maccatalyst-arm64/MauiApp.app

# Windows
cd src/MauiApp
dotnet run -f net9.0-windows10.0.19041.0
```

## Run Tests

```bash
dotnet test TakeHome.sln
# Runs 9 tests (6 integration + 3 unit) — all should pass
```

## Static Port Configuration

All projects use a single static HTTP port: `http://localhost:5100`

- **IngestionApi**: Configured via `builder.WebHost.UseUrls("http://localhost:5100")` in Program.cs
- **DeviceSimulator**: Posts to `http://localhost:5100/api/v1/measurements`
- **MauiApp**: HttpClient base address set to `http://localhost:5100`
- **DesktopApp**: HttpClient base address set to `http://localhost:5100`

## Notes

- MAUI on macOS requires `NSAllowsLocalNetworking` in Info.plist (already configured) to allow plain HTTP to localhost
- The API uses a simple `x-api-key: local-dev` header for authentication
- The DeviceSimulator includes `DangerousAcceptAnyServerCertificateValidator` for dev flexibility

## Run All Tests

```bash
# From repository root
dotnet test TakeHome.sln
```

### Run by category:

```bash
# Unit tests only (Domain + ViewModel)
dotnet test tests/Domain.Tests
dotnet test tests/DesktopApp.Tests

# Integration tests only
dotnet test tests/IngestionApi.IntegrationTests
```

## Project Dependencies

```
Domain (net8.0)                    ← no external framework dependencies
  ↑
Presentation (net8.0)              ← CommunityToolkit.Mvvm, HttpClient, ILogger
  ↑              ↑
DesktopApp       MauiApp           ← platform XAML + DI bootstrap
(net8.0-win)     (net8.0-maccatalyst;net8.0-windows)
  
IngestionApi (net8.0)              ← ASP.NET Core, references Domain
DeviceSimulator (net8.0)           ← standalone console, calls API over HTTP
```

## Key Configuration

| Setting | Location | Value |
|---------|----------|-------|
| API base URL | `src/DesktopApp/App.xaml.cs` and `src/MauiApp/MauiProgram.cs` | `https://localhost:7296` |
| API key | Header `x-api-key` | `local-dev` |
| Poll interval | `MainViewModel` (in Presentation) | 1 second |
| Max query results | `InMemoryStore` | 500 |
| MAUI TFMs | `src/MauiApp/MauiApp.csproj` | `net8.0-maccatalyst;net8.0-windows10.0.19041.0` |
