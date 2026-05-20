# Technical Documentation

## 1. SpecKit: AI-Driven Spec-First Development

### What is SpecKit?

SpecKit is a structured workflow framework for AI-assisted coding that enforces a **specification → plan → tasks → implement** pipeline. Instead of jumping directly into code, SpecKit guides the AI agent through progressive refinement:

1. **`/speckit.specify`** — Capture user stories, acceptance criteria, and requirements in a technology-agnostic spec
2. **`/speckit.clarify`** — Ask targeted questions to resolve ambiguities
3. **`/speckit.plan`** — Produce research, data models, API contracts, and an implementation plan
4. **`/speckit.tasks`** — Generate dependency-ordered, file-specific tasks
5. **`/speckit.implement`** — Execute tasks phase-by-phase with validation checkpoints

### Advantages for AI Coding

| Advantage | Description |
|-----------|-------------|
| **Traceability** | Every code change traces back to a requirement in spec.md |
| **Reduced Hallucination** | Contracts and data models are defined before code, preventing the AI from inventing undocumented behavior |
| **Incremental Validation** | Each phase has checkpoints (build, test) before proceeding |
| **Backward Compatibility** | Constraints documented upfront prevent breaking changes |
| **Reproducibility** | Another developer (or AI agent) can re-run the same tasks and get consistent results |
| **Constitution Governance** | A project constitution enforces architectural principles across all features |

---

## 2. Architectural Changes

### 2.1 IngestionApi (Major Refactoring)

The API underwent 10 internal architectural improvements while maintaining **zero external contract changes**:

```
src/IngestionApi/
├── Program.cs                    — Route groups, DI registration, thin endpoint handlers
├── Measurement.cs                — Unchanged record type
├── MeasurementStore.cs           — Unchanged (IMeasurementStore + InMemoryStore)
├── Filters/
│   └── ApiKeyAuthFilter.cs       — IEndpointFilter: centralized auth via IApiKeyValidator
├── Validation/
│   └── MeasurementFluentValidator.cs — FluentValidation rules with per-field errors
├── Services/
│   ├── IApiKeyValidator.cs       — Interface for testable auth
│   ├── ApiKeyValidator.cs        — Config-based key validation
│   ├── IMeasurementService.cs    — Service layer interface
│   └── MeasurementService.cs     — Orchestrates: validate → store → publish event → log
├── Events/
│   ├── MeasurementEvent.cs       — Event record (Measurement + PublishedAt)
│   └── MeasurementEventChannel.cs — Bounded Channel<T> (capacity 1000, DropOldest)
└── Models/
    └── PaginatedQuery.cs         — Query parameters (Type, Since, Skip, Take)
```

**Key Improvements:**

| # | Improvement | Before | After |
|---|-------------|--------|-------|
| 1 | Auth | Inline `ValidateApiKey()` in each handler | `IEndpointFilter` on route group |
| 2 | Validation | Static `MeasurementValidator.IsValid()` → bool | FluentValidation → per-field RFC 7807 errors |
| 3 | Error Format | `"invalid measurement"` string | RFC 7807 Problem Details with `errors` dict |
| 4 | Service Layer | Business logic in endpoint handlers | `IMeasurementService` (testable in isolation) |
| 5 | Pagination | No pagination (return all up to 500) | `skip`/`take` params + `X-Total-Count`/`X-Has-More` headers |
| 6 | Route Org | Flat `app.MapPost/Get` | `MapGroup("/api/v1")` with shared filter |
| 7 | Logging | None | Structured `ILogger` with DeviceId, Type, outcome |
| 8 | Events | None | `Channel<MeasurementEvent>` for async subscribers |
| 9 | Health | Manual `MapGet("/healthz")` | Framework `MapHealthChecks("/healthz")` |
| 10 | DI | Direct store calls | Full DI: validator, service, channel, auth |

### 2.2 Client Apps (MAUI & WPF Desktop) — From Earlier 001 Modernization

Both client apps were modernized in the previous feature branch (`001-architecture-modernization`):

**Shared Presentation Layer (`src/Presentation/`):**
- MVVM pattern using `CommunityToolkit.Mvvm`
- `MainViewModel` with `ObservableProperty`, `RelayCommand`, auto-poll timer (1s)
- `MeasurementService` (HTTP client wrapper) shared between both apps
- `MeasurementDto` record for deserialization

**MAUI App (`src/MauiApp/`):**
- .NET 9 MAUI targeting macOS Catalyst
- DI via `MauiApp.CreateBuilder()` with `AddHttpClient<MeasurementService>`
- `CollectionView` bound to `MainViewModel.Measurements`
- `NSAllowsLocalNetworking` in Info.plist for HTTP on macOS

**WPF Desktop App (`src/DesktopApp/`):**
- .NET 8 WPF with DI via `ServiceCollection`
- `DataGrid` bound to `MainViewModel.Measurements`
- Same shared ViewModel as MAUI

### 2.3 Device Simulator

Simple console app (unchanged across both features):
- Posts randomized HeartRate measurements every 2 seconds
- Uses `x-api-key: local-dev` header
- Targets `http://localhost:5100/api/v1/measurements`
- Validates connectivity via HTTP status code (expects `202 Accepted`)

---

## 3. Test Strategy

### 3.1 Unit Tests (`tests/IngestionApi.UnitTests/`)

**Framework:** xUnit + NSubstitute (mocking)

| Test Class | Tests | What It Validates |
|------------|-------|-------------------|
| `ApiKeyAuthFilterTests` | 4 | Filter passes valid keys, returns 401 for invalid/missing/empty keys |
| `MeasurementFluentValidatorTests` | 6 | Each validation rule independently + combined multi-field errors |
| `MeasurementServiceTests` | 3 | Successful add stores + publishes event; validation failures throw; query applies pagination |

**Total: 13 unit tests**

Example test — validating the auth filter:
```csharp
[Fact]
public async Task InvalidApiKey_Returns401()
{
    _validator.IsValid("bad-key").Returns(false);
    var (context, next) = CreateContext("bad-key");

    var result = await _filter.InvokeAsync(context, next);

    Assert.IsType<UnauthorizedHttpResult>(result);
    await next.DidNotReceive().Invoke(Arg.Any<EndpointFilterInvocationContext>());
}
```

### 3.2 Integration Tests (`tests/IngestionApi.IntegrationTests/`)

**Framework:** xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)

| Test | What It Validates |
|------|-------------------|
| `HealthEndpoint_ReturnsOk` | GET /healthz → 200 with `{"status":"healthy"}` |
| `PostValidMeasurement_Returns202Accepted` | POST valid measurement → 202 + Location header |
| `PostInvalidMeasurement_Returns400BadRequest` | POST with empty DeviceId → 400 |
| `RequestWithoutApiKey_Returns401Unauthorized` | POST without x-api-key → 401 |
| `QueryByType_ReturnsFilteredResults` | GET ?type=HeartRate filters correctly |
| `QueryWithSinceParameter_ReturnsOnlyRecentMeasurements` | GET ?since=... time-filters correctly |

**Total: 6 integration tests**

These tests run against the **real API pipeline** (in-memory, no external dependencies) and were **not modified** during the 002 refactoring — proving backward compatibility.

### 3.3 Presentation Tests (`tests/Presentation.Tests/`)

| Test | What It Validates |
|------|-------------------|
| `RefreshCommand_PopulatesMeasurements_WhenServiceReturnsData` | ViewModel loads data via service |
| `RefreshCommand_SetsErrorStatus_WhenServiceThrows` | ViewModel handles HTTP errors gracefully |
| `GetHealthAsync_ReturnsTrue_WhenApiHealthy` | Health check service integration |

**Total: 3 presentation tests**

### 3.4 Running All Tests

```bash
# All tests (22 total)
dotnet test TakeHome.sln

# Unit tests only
dotnet test tests/IngestionApi.UnitTests/

# Integration tests only
dotnet test tests/IngestionApi.IntegrationTests/

# Presentation tests only
dotnet test tests/Presentation.Tests/
```

### 3.5 Test Coverage Summary

| Area | Coverage |
|------|----------|
| Auth filter (centralized) | ✅ Unit + Integration |
| Validation (per-field) | ✅ Unit + Integration |
| Service layer (orchestration) | ✅ Unit |
| Pagination | ✅ Unit (via service test) |
| Health check | ✅ Integration |
| Route groups + backward compat | ✅ Integration (all 6 pass unchanged) |
| Event channel | ✅ Unit (verified publish on add) |
| MVVM ViewModel | ✅ Presentation tests |
