# Research: Architecture Modernization

**Date**: 2026-05-20
**Feature**: 001-architecture-modernization

## R1: CommunityToolkit.Mvvm in WPF with DI

**Decision**: Use CommunityToolkit.Mvvm 8.x with source generators for the WPF DesktopApp ViewModel layer.

**Rationale**:
- Microsoft-maintained, framework-agnostic (works in WPF, MAUI, WinUI, Uno)
- Source generators eliminate boilerplate: `[ObservableProperty]` generates `INotifyPropertyChanged`, `[RelayCommand]` generates `ICommand` implementations
- No runtime reflection — AOT-friendly for future .NET native compilation
- Integrates naturally with `Microsoft.Extensions.DependencyInjection`
- ViewModels remain POCO classes testable without UI framework

**Alternatives Considered**:
- Prism: Heavy; includes its own DI container (conflicts with built-in), navigation framework unnecessary for single-window demo
- Hand-rolled: More code, more maintenance, no source-gen benefits, reinvents well-tested patterns
- ReactiveUI: Powerful but steep learning curve, RX dependency overkill for this scope

**Integration Pattern**:
```csharp
// ViewModel uses source generators
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Measurement> _measurements = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync() { ... }
}
```

## R2: DI Setup in WPF (App.xaml.cs)

**Decision**: Configure `ServiceCollection` in `App.xaml.cs` and resolve `MainWindow` + `MainViewModel` from the container.

**Rationale**:
- WPF lacks built-in DI (unlike ASP.NET Core), so manual setup in `App` class is the standard pattern
- `IHttpClientFactory` requires `Microsoft.Extensions.Http` NuGet package
- Set `DataContext` via DI resolution rather than XAML instantiation to enable constructor injection

**Pattern**:
```csharp
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<MeasurementService>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7296");
            client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
        });
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        services.AddLogging(b => b.AddDebug());
        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```

## R3: JsonElement for Measurement.Value

**Decision**: Change `Measurement.Value` from `object` to `System.Text.Json.JsonElement`.

**Rationale**:
- `object` causes runtime boxing, unpredictable serialization (Newtonsoft may serialize differently than System.Text.Json), and no type safety
- `JsonElement` preserves the raw JSON structure from devices without forcing a single numeric type
- Downstream consumers can call `.GetDouble()`, `.GetInt32()`, or enumerate arrays as needed
- System.Text.Json handles `JsonElement` natively — zero custom converters needed
- Validation: check `Value.ValueKind != JsonValueKind.Undefined`

**Alternatives Considered**:
- `double`: Too restrictive — ECG devices send arrays, spirometry sends structured objects
- `string`: Requires consumers to deserialize again; double-serialization overhead
- `object`: Status quo — serialization nightmares, no compile-time safety

**Migration Impact**:
- DeviceSimulator sends anonymous objects → System.Text.Json serializes numeric values as `JsonElement` with `ValueKind.Number` — no simulator changes needed
- InMemoryStore is unaffected (stores the record as-is)
- Integration tests construct `Measurement` with `JsonSerializer.SerializeToElement(value)` helper

## R4: WebApplicationFactory Integration Testing Pattern

**Decision**: Use `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` for in-process API testing.

**Rationale**:
- Standard ASP.NET Core test host — no ports, no network, deterministic
- `Program` class is already public (top-level statements expose it)
- Test client auto-configures base address; add custom headers per-test
- Supports service replacement (e.g., swap store implementation for test isolation)

**Pattern**:
```csharp
public class MeasurementApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MeasurementApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
    }

    [Fact]
    public async Task Post_ValidMeasurement_Returns202()
    {
        var m = new { MeasurementId = Guid.NewGuid(), ... };
        var response = await _client.PostAsJsonAsync("/api/v1/measurements", m);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
```

## R5: InMemoryStore Placement

**Decision**: Keep `InMemoryStore` (the concrete implementation) in the IngestionApi project; only the `IMeasurementStore` interface moves to Domain.

**Rationale**:
- The interface defines the contract (Domain concern); the implementation is infrastructure
- Other consumers (DesktopApp) don't need their own store — they call the API
- If a future project needs a different store (e.g., SQLite for MAUI offline), it implements `IMeasurementStore` locally
- Keeps Domain dependency-free (no `System.Collections.Concurrent` assumptions)

**Alternatives Considered**:
- Move InMemoryStore to Domain: Pollutes the library with a specific implementation; violates "no infrastructure in domain"
- Create separate Infrastructure project: Over-engineering for a single in-memory list; violates YAGNI

## R6: Replacing Newtonsoft.Json with System.Text.Json

**Decision**: Remove `Newtonsoft.Json` from DesktopApp; use `System.Text.Json` for HTTP deserialization.

**Rationale**:
- Constitution mandates System.Text.Json
- `HttpClient.GetFromJsonAsync<T>()` extension (from `System.Net.Http.Json`) uses System.Text.Json natively
- `JsonElement` for Measurement.Value aligns perfectly
- Fewer dependencies, better performance, AOT-compatible

**Migration Steps**:
1. Remove `Newtonsoft.Json` NuGet from DesktopApp.csproj
2. Replace `JsonConvert.DeserializeObject<>` with `HttpClient.GetFromJsonAsync<>()`
3. Remove `using Newtonsoft.Json;`

## R7: Shared Presentation Library for WPF + MAUI Code Reuse

**Decision**: Create a `Presentation` class library targeting `net8.0` that contains all ViewModels and services, consumed by both WPF DesktopApp and MAUI app.

**Rationale**:
- CommunityToolkit.Mvvm is framework-agnostic — ViewModels work identically in WPF and MAUI
- `IHttpClientFactory`, `ILogger<T>`, and `ObservableObject` have no platform dependencies
- Only XAML views differ (WPF Window vs MAUI ContentPage); the binding targets are identical
- Maximizes code reuse: ViewModel logic, service wrappers, and commands are written once
- Test project (`Presentation.Tests`) covers ViewModel logic for both platforms simultaneously

**Architecture**:
```
Domain (net8.0)           ← entities, validation, interfaces
    ↑
Presentation (net8.0)     ← ViewModels, services, commands (CommunityToolkit.Mvvm)
    ↑         ↑
DesktopApp    MauiApp     ← platform XAML + DI bootstrap only
(WPF)         (MAUI)
```

**Key Packages for Presentation.csproj**:
- `CommunityToolkit.Mvvm` (source generators)
- `Microsoft.Extensions.Http` (IHttpClientFactory)
- `Microsoft.Extensions.Logging.Abstractions` (ILogger<T>)
- Project reference to `Domain`

**Why not a .NET MAUI Class Library?**: A standard `net8.0` class library is consumed by both WPF and MAUI without requiring MAUI workload to build. MAUI class libraries target multi-TFM which would break WPF consumption.

**Alternatives Considered**:
- ViewModels in each app (duplicated): Violates DRY; changes must be made twice; test coverage doubled
- Shared project (not library): No binary output, harder to manage, no independent testing
- MAUI class library: Requires MAUI workload on CI for all builds; WPF can't reference it

## R8: .NET MAUI App Setup for Mac Catalyst + Windows

**Decision**: Create a .NET MAUI app targeting `net8.0-maccatalyst` and `net8.0-windows10.0.19041.0`.

**Rationale**:
- User develops on macOS — Mac Catalyst is the native target
- Windows support via WinUI 3 (MAUI's Windows backend)
- DI setup in `MauiProgram.CreateMauiApp()` mirrors WPF `App.xaml.cs` pattern
- Both apps register identical services: `MainViewModel`, `MeasurementService`, `IHttpClientFactory`

**DI Pattern (MauiProgram.cs)**:
```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddHttpClient<MeasurementService>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7296");
            client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
        });
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddLogging(l => l.AddDebug());

        return builder.Build();
    }
}
```

**TFMs in .csproj**:
```xml
<TargetFrameworks>net8.0-maccatalyst;net8.0-windows10.0.19041.0</TargetFrameworks>
```
