# Implementation Plan: API Architectural Improvements

**Branch**: `002-api-architectural-improvements` | **Date**: 2026-05-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-api-architectural-improvements/spec.md`

## Summary

Improve the IngestionApi's internal architecture with 10 enhancements: centralized endpoint filter for auth, interface-based API key validation, FluentValidation for structured per-field errors, RFC 7807 Problem Details responses, a service layer abstracting business logic, pagination via headers, route groups for organization, structured logging, Channel<T>-based event notifications, and framework-standard health checks. All changes are internal to IngestionApi — no modifications to client apps, no changes to request/response body format, same port 5100.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0 (LTS)

**Primary Dependencies**: FluentValidation 11.x, Microsoft.Extensions.Diagnostics.HealthChecks, System.Threading.Channels (in-box)

**Storage**: In-memory (InMemoryStore unchanged)

**Testing**: xUnit + NSubstitute + Microsoft.AspNetCore.Mvc.Testing; existing 6 integration tests must pass unmodified, new service-layer unit tests added

**Target Platform**: Any OS (`net8.0`); API listens on `http://localhost:5100`

**Project Type**: Web service (ASP.NET Core Minimal API)

**Performance Goals**: No regression from current performance; endpoint filters add <1ms overhead

**Constraints**: Zero changes to request/response body shapes; same URL paths; same port; no modifications to Presentation/MauiApp/DesktopApp/DeviceSimulator projects

**Scale/Scope**: Single IngestionApi project; ~500 measurements max in-memory

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance | Notes |
|-----------|-----------|-------|
| I. Contract-First API Design | ✅ PASS | Same HTTP contract preserved; pagination metadata added as opt-in headers |
| II. Domain Isolation | ⚠️ DEVIATION (justified) | Constitution says domain in separate library; we keep types in IngestionApi per prior decision. Service layer provides logical isolation within the project. |
| III. MVVM & Presentation Separation | ✅ N/A | No UI changes in this feature |
| IV. Test-Driven Quality | ✅ PASS | Existing integration tests preserved; new unit tests for service layer and validators |
| V. Cross-Platform Portability | ✅ N/A | No UI changes |
| VI. Dependency Injection & Simplicity | ✅ PASS | All new types registered via DI; interfaces introduced only where testability requires (IApiKeyValidator, IMeasurementService) |

**Gate result: PASS** — one documented deviation (Principle II) justified by prior architectural decision and backward compatibility constraint.

## Project Structure

### Documentation (this feature)

```text
specs/002-api-architectural-improvements/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/           # Phase 1 output (updated)
│   └── ingestion-api.md
└── tasks.md             # Phase 2 output
```

### Source Code (changes scoped to IngestionApi)

```text
src/IngestionApi/
├── IngestionApi.csproj           # Add FluentValidation, HealthChecks packages
├── Program.cs                    # Refactored: route groups, service registration, health checks
├── Measurement.cs                # Unchanged
├── MeasurementValidator.cs       # Replaced by FluentValidation validator
├── MeasurementStore.cs           # Unchanged (IMeasurementStore + InMemoryStore)
├── Filters/
│   └── ApiKeyAuthFilter.cs       # IEndpointFilter implementation
├── Validation/
│   └── MeasurementFluentValidator.cs  # FluentValidation rules
├── Services/
│   ├── IMeasurementService.cs    # Service interface
│   ├── MeasurementService.cs     # Service implementation (store + events + logging)
│   ├── IApiKeyValidator.cs       # Auth interface
│   └── ApiKeyValidator.cs        # Concrete implementation (config-based)
├── Events/
│   ├── MeasurementEvent.cs       # Event record
│   └── MeasurementEventChannel.cs # Channel<T> wrapper registered as singleton
└── Models/
    └── PaginatedQuery.cs         # skip/take parameter model

tests/
└── IngestionApi.UnitTests/       # NEW: service layer unit tests
    ├── IngestionApi.UnitTests.csproj
    ├── MeasurementServiceTests.cs
    ├── ApiKeyAuthFilterTests.cs
    └── MeasurementFluentValidatorTests.cs
```

## Complexity Tracking

| Principle | Deviation | Rationale |
|-----------|-----------|-----------|
| II. Domain Isolation | Domain types remain in IngestionApi, not a separate library | Prior architectural decision (001-architecture-modernization); client apps use HTTP only; service layer provides logical boundary within the project |
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
