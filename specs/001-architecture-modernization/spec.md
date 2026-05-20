# Feature Specification: Architecture Modernization

**Feature Branch**: `001-architecture-modernization`

**Created**: 2026-05-20

**Status**: Complete

**Input**: User description: "Showcase MVVM (bindings, commands), DI, and async HTTP client in DesktopApp; move domain logic to a Domain class library; integration test IngestionApi endpoints; unit test where applicable."

**Implementation Note**: After clarification, domain types (Measurement, MeasurementValidator, IMeasurementStore) remain in IngestionApi rather than a separate Domain library. Client apps communicate exclusively via HTTP (no project references to IngestionApi). A shared Presentation library holds ViewModels and services for both WPF and MAUI apps.

## Clarifications

### Session 2026-05-20

- Q: What type constraint should Measurement.Value have in the Domain library? → A: Use `JsonElement` to preserve arbitrary structured device data.
- Q: Should the MVVM implementation use a community toolkit or hand-roll the infrastructure? → A: Use `CommunityToolkit.Mvvm` (source generators, minimal boilerplate).
- Q: How should the modernized DesktopApp retrieve measurements? → A: Keep auto-poll at 1-second interval (demo purposes) plus manual Refresh command.
- Q: Should the modernized solution include structured logging? → A: Yes, inject `ILogger<T>` in ViewModels and API endpoints.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Domain Library Extraction (Priority: P1)

A developer extracts the Measurement entity, MeasurementValidator, and IMeasurementStore interface from IngestionApi into a standalone Domain class library so that any project in the solution can reference shared business logic without depending on ASP.NET Core.

**Why this priority**: Every other deliverable (MVVM DesktopApp, integration tests, future MAUI app) depends on a clean, reusable domain layer. This is the foundational building block.

**Independent Test**: Build the Domain library in isolation and run its unit tests without starting the API or desktop app.

**Acceptance Scenarios**:

1. **Given** the solution is built, **When** a developer inspects the Domain project, **Then** it targets `net8.0` (no platform suffix) and compiles independently with zero ASP.NET Core references.
2. **Given** the Domain library exists, **When** IngestionApi references it, **Then** the API project no longer contains Measurement, MeasurementValidator, or IMeasurementStore definitions.
3. **Given** the Domain library exists, **When** a new consumer project is added to the solution, **Then** it can reference Domain and use Measurement and MeasurementValidator without pulling in web framework dependencies.

---

### User Story 2 - MVVM Desktop App with DI and Async HTTP (Priority: P2)

A clinician opens the Vitals Monitor desktop app and sees live measurement data fetched asynchronously from the local IngestionApi. The app uses proper MVVM architecture: data bindings display measurements, a command triggers refresh, and an injected HttpClient communicates with the API without blocking the UI thread.

**Why this priority**: Demonstrates the primary UX modernization (responsive UI, testable ViewModel logic) and validates that the extracted Domain library integrates correctly into a consuming application.

**Independent Test**: Launch the DesktopApp with the API running; verify measurements appear, the refresh command works, and no UI freezes occur during network calls.

**Acceptance Scenarios**:

1. **Given** the DesktopApp is running and the IngestionApi is available, **When** the app starts, **Then** it auto-polls every 1 second and asynchronously fetches and displays the latest measurements without freezing the UI.
2. **Given** the DesktopApp is displaying measurements, **When** the user invokes the Refresh command, **Then** the data grid updates with the latest measurements from the API.
3. **Given** the IngestionApi is unavailable, **When** the app attempts to fetch measurements, **Then** it displays a user-friendly error status instead of crashing or hanging.
4. **Given** the DesktopApp source code, **When** a developer inspects the MainWindow code-behind, **Then** it contains only `InitializeComponent()` — all logic resides in the ViewModel.
5. **Given** the DesktopApp project file, **When** a developer inspects dependencies, **Then** `HttpClient` is registered via `IHttpClientFactory` in the DI container and injected into the ViewModel.

---

### User Story 2b - MAUI App with Shared ViewModel Layer (Priority: P2)

A clinician on macOS opens the Vitals Monitor MAUI app and sees the same live measurement data as the WPF app, fetched from the local IngestionApi. The MAUI app reuses the same ViewModels, services, and Domain library as the WPF app — only the XAML views differ.

**Why this priority**: Proves maximum code reuse across platforms. Builds in parallel with the WPF modernization since both consume the same shared ViewModel/service layer.

**Independent Test**: Launch the MAUI app on macOS (Mac Catalyst) or Windows with the API running; verify measurements appear and the refresh command works identically to the WPF app.

**Acceptance Scenarios**:

1. **Given** the MAUI app targets `net8.0-maccatalyst` and `net8.0-windows`, **When** it is built, **Then** it compiles successfully on both platforms.
2. **Given** the shared ViewModels project exists, **When** both DesktopApp (WPF) and MauiApp reference it, **Then** both apps use the identical MainViewModel with no code duplication.
3. **Given** the MAUI app is running on macOS, **When** the IngestionApi is available, **Then** it auto-polls every 1 second and displays measurements identically to the WPF app.
4. **Given** the MAUI app, **When** the user invokes the Refresh command, **Then** the data refreshes asynchronously without UI freeze.
5. **Given** the MAUI app source code, **When** a developer inspects the MainPage code-behind, **Then** it contains only `InitializeComponent()` — all logic resides in the shared MainViewModel.

---

### User Story 3 - Integration Tests for IngestionApi (Priority: P3)

A developer runs the integration test suite and confirms that all IngestionApi endpoints behave correctly for valid inputs, invalid inputs, and authentication failures — without deploying the service externally.

**Why this priority**: Validates the API contract end-to-end in-process, catching regressions before code reaches production. Depends on Domain extraction (P1) being complete so tests reference shared models.

**Independent Test**: Run `dotnet test` targeting the integration test project; all tests pass in CI without external services.

**Acceptance Scenarios**:

1. **Given** the integration test project, **When** tests execute, **Then** the API is hosted in-memory via `WebApplicationFactory<Program>` with no external network calls.
2. **Given** a valid measurement payload with a valid API key, **When** POST `/api/v1/measurements` is called, **Then** the response is 202 Accepted with the measurement echoed in the body.
3. **Given** an invalid measurement payload (empty DeviceId), **When** POST `/api/v1/measurements` is called, **Then** the response is 400 Bad Request.
4. **Given** a request without the `x-api-key` header, **When** any protected endpoint is called, **Then** the response is 401 Unauthorized.
5. **Given** measurements have been posted, **When** GET `/api/v1/measurements?type=HeartRate` is called, **Then** only HeartRate measurements are returned.
6. **Given** the health endpoint, **When** GET `/healthz` is called, **Then** the response is 200 OK with a healthy status object.

---

### User Story 4 - Unit Tests for Domain and ViewModel Logic (Priority: P4)

A developer runs unit tests that verify domain validation rules and ViewModel behaviors in isolation, using mocks for external dependencies such as HttpClient and IMeasurementStore.

**Why this priority**: Provides fast feedback on business rules and presentation logic without requiring a running API or UI framework. Complements integration tests with finer-grained coverage.

**Independent Test**: Run `dotnet test` for Domain.Tests and Presentation.Tests; all pass in under 5 seconds with no network or UI dependencies.

**Acceptance Scenarios**:

1. **Given** a Measurement with an empty Guid, **When** MeasurementValidator.IsValid is called, **Then** it returns false.
2. **Given** a Measurement with all required fields populated, **When** MeasurementValidator.IsValid is called, **Then** it returns true.
3. **Given** a Measurement with a blank DeviceId, **When** MeasurementValidator.IsValid is called, **Then** it returns false.
4. **Given** the MainViewModel with a mocked HTTP client returning measurement data, **When** the RefreshCommand executes, **Then** the Measurements collection property is populated.
5. **Given** the MainViewModel with a mocked HTTP client that throws, **When** the RefreshCommand executes, **Then** an error status message is set and no exception propagates.

---

### Edge Cases

- What happens when the API returns an empty measurement list? The UI should display an empty state, not crash.
- What happens when a measurement has a null or unexpected `Value` type? Validation should reject it or the UI should handle it gracefully.
- What happens when multiple rapid refresh commands are issued? The ViewModel should prevent concurrent requests or gracefully handle overlap.
- What happens when the API key is missing in a test? The test should assert 401 without unhandled exceptions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: ~~The solution MUST contain a `Domain` class library~~ → **Revised**: Domain types (Measurement, MeasurementValidator, IMeasurementStore) remain in IngestionApi. No separate Domain project — client apps use HTTP to interact with the API.
- **FR-002**: ~~IngestionApi MUST reference the Domain library~~ → **Revised**: IngestionApi owns all domain types directly. Client apps use client-side DTOs (MeasurementDto) for deserialization.
- **FR-003**: The DesktopApp MUST implement the MVVM pattern using `CommunityToolkit.Mvvm` source generators, with a MainViewModel exposing an `ObservableProperty` Measurements collection and a `RelayCommand` for refresh.
- **FR-004**: The DesktopApp MUST use dependency injection via `Microsoft.Extensions.DependencyInjection` to register and resolve services.
- **FR-005**: The DesktopApp MUST use `HttpClient` (via `IHttpClientFactory`) with async/await for all API communication; `WebClient` usage MUST be removed.
- **FR-006**: The DesktopApp MainWindow MUST use XAML data bindings to the ViewModel for all dynamic content (data grid, status text, button commands).
- **FR-007**: The DesktopApp MUST display a user-friendly error message when the API is unreachable, without crashing.
- **FR-008**: Integration tests MUST cover all IngestionApi endpoints (health, post measurement, query measurements) including success, validation failure, and auth failure scenarios.
- **FR-009**: Integration tests MUST use `WebApplicationFactory<Program>` for in-process hosting.
- **FR-010**: Unit tests MUST cover MeasurementValidator logic for all boundary conditions.
- **FR-011**: Unit tests MUST cover ViewModel command execution with mocked HTTP dependencies.
- **FR-012**: All new public types and methods MUST have XML documentation comments.
- **FR-013**: The solution MUST build with zero warnings and all tests MUST pass via `dotnet test`.
- **FR-014**: ViewModels and API endpoints MUST accept `ILogger<T>` via constructor injection and log key operations (fetch start/complete, errors) using structured logging.
- **FR-015**: ViewModels and services (MeasurementService) MUST reside in a shared class library (`Shared` or `App.ViewModels`) targeting `net8.0` so both WPF and MAUI apps reference it without duplication.
- **FR-016**: The solution MUST contain a .NET MAUI app (`MauiApp`) targeting `net9.0-maccatalyst` (and conditionally `net9.0-windows10.0.19041.0` on Windows) that uses the shared ViewModels and services.
- **FR-017**: The MAUI app MUST use dependency injection via `MauiProgram.CreateMauiApp()` builder, registering the same services (IHttpClientFactory, MainViewModel, ILogger) as the WPF app.
- **FR-018**: The MAUI app MUST use MAUI XAML data bindings to the shared MainViewModel for all dynamic content.

### Non-Functional Requirements

- **NFR-001**: The DesktopApp UI MUST remain responsive (no UI thread blocking) during API calls.
- **NFR-002**: Integration tests MUST execute in under 30 seconds total.
- **NFR-003**: Unit tests MUST execute in under 5 seconds total.
- **NFR-004**: The Domain library MUST have no dependencies on ASP.NET Core, WPF, or MAUI packages.

### Key Entities

- **Measurement**: Represents a single reading from a medical device — identified by MeasurementId, associated with a DeviceId and PatientId, typed (e.g., HeartRate, SpO2), with a Value (`JsonElement` to support numeric, array, or structured device payloads) and Unit, timestamped.
- **MeasurementValidator**: Stateless validation logic ensuring a Measurement has required fields (non-empty Guid, non-default Timestamp, non-blank DeviceId, non-blank Type, and a defined Value — not `Undefined` ValueKind).
- **IMeasurementStore**: Abstraction for persisting and querying Measurements — supports adding a measurement and querying by type and time window.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The DesktopApp displays live measurement data within 2 seconds of launch when the API is available.
- **SC-002**: All integration tests (minimum 6 test cases covering happy path, validation errors, and auth failures) pass consistently across runs.
- **SC-003**: All unit tests (minimum 5 test cases for validator + ViewModel) pass in under 5 seconds.
- **SC-004**: ~~Domain library compiles independently~~ → **Revised**: Domain types in IngestionApi compile with `dotnet build src/IngestionApi/IngestionApi.csproj` succeeding with zero errors.
- **SC-005**: The DesktopApp and MauiApp code-behind files contain no business logic — only framework initialization.
- **SC-006**: The full solution builds and all tests pass with a single `dotnet test` invocation from the repository root.
- **SC-007**: The MAUI app compiles and launches on Mac Catalyst, displaying the same measurement data as the WPF app.

## Assumptions

- The IngestionApi continues to use in-memory storage for this iteration; persistent storage is out of scope.
- The API key validation mechanism (`x-api-key: local-dev`) remains unchanged for now; production-grade auth is a future concern.
- The DeviceSimulator project was updated to: use HTTP (port 5100) instead of HTTPS to avoid certificate issues, add SSL bypass handler for dev environments, and add console logging for posted measurements.
- The existing WPF target (`net8.0-windows`) is retained and modernized; a parallel MAUI app targets `net9.0-maccatalyst` (and conditionally `net9.0-windows10.0.19041.0` on Windows).
- `Newtonsoft.Json` in the DesktopApp will be replaced with `System.Text.Json` as part of the modernization.
- xUnit is the test framework for both unit and integration tests.
