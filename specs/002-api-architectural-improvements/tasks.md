# Tasks: API Architectural Improvements

**Input**: Design documents from `/specs/002-api-architectural-improvements/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ingestion-api.md ✓, quickstart.md ✓

**Tests**: Included — plan.md specifies unit tests for service layer, auth filter, and validator.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US8)
- Exact file paths included in descriptions

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Add dependencies, create folder structure, create unit test project

- [x] T001 Add FluentValidation.AspNetCore and Microsoft.Extensions.Diagnostics.HealthChecks packages to src/IngestionApi/IngestionApi.csproj
- [x] T002 Create folder structure: src/IngestionApi/Filters/, src/IngestionApi/Validation/, src/IngestionApi/Services/, src/IngestionApi/Events/, src/IngestionApi/Models/
- [x] T003 Create tests/IngestionApi.UnitTests/ project with xUnit, NSubstitute, and reference to IngestionApi; add to TakeHome.sln

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interfaces, models, and infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create PaginatedQuery record in src/IngestionApi/Models/PaginatedQuery.cs with Type, Since, Skip (default 0), Take (default 50) properties
- [x] T005 [P] Create MeasurementEvent record in src/IngestionApi/Events/MeasurementEvent.cs with Measurement and PublishedAt properties
- [x] T006 [P] Create IApiKeyValidator interface with IsValid(string? apiKey) method in src/IngestionApi/Services/IApiKeyValidator.cs
- [x] T007 [P] Create IMeasurementEventChannel interface and MeasurementEventChannel implementation (bounded Channel\<MeasurementEvent\> capacity 1000, DropOldest) in src/IngestionApi/Events/MeasurementEventChannel.cs
- [x] T008 [P] Create IMeasurementService interface with AddAsync and QueryAsync methods in src/IngestionApi/Services/IMeasurementService.cs
- [x] T009 Refactor src/IngestionApi/Program.cs to use MapGroup("/api/v1") for route organization; move POST and GET measurement endpoints into the group; keep /healthz outside the group (unauthenticated)

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Centralized API Key Authentication (Priority: P1) 🎯 MVP

**Goal**: Replace inline API key validation with a centralized IEndpointFilter + injectable IApiKeyValidator

**Independent Test**: Send requests with valid/invalid/missing API keys; verify 401/200 behavior unchanged; verify mock validator can replace real one in tests

### Implementation for User Story 1

- [x] T010 [US1] Implement ApiKeyValidator (reads expected key from IConfiguration "ApiKey" section) in src/IngestionApi/Services/ApiKeyValidator.cs
- [x] T011 [US1] Implement ApiKeyAuthFilter as IEndpointFilter that resolves IApiKeyValidator from DI and short-circuits with 401 for invalid keys in src/IngestionApi/Filters/ApiKeyAuthFilter.cs
- [x] T012 [US1] Register IApiKeyValidator in DI and attach ApiKeyAuthFilter to the /api/v1 route group in src/IngestionApi/Program.cs
- [x] T013 [US1] Write unit tests for ApiKeyAuthFilter (valid key passes, missing key returns 401, invalid key returns 401, empty key returns 401) in tests/IngestionApi.UnitTests/ApiKeyAuthFilterTests.cs

**Checkpoint**: Auth is centralized. All /api/v1 endpoints require valid API key via filter. /healthz remains unauthenticated.

---

## Phase 4: User Story 2 — Structured Validation Error Responses (Priority: P1)

**Goal**: Replace static IsValid() with FluentValidation providing per-field errors in RFC 7807 Problem Details format

**Independent Test**: Submit measurements with various invalid fields; verify 400 response contains Problem Details with field-level errors

### Implementation for User Story 2

- [x] T014 [US2] Implement MeasurementFluentValidator (AbstractValidator\<Measurement\>) with rules: MeasurementId != Guid.Empty, Timestamp != default, DeviceId not empty, Type not empty, Value must be defined in src/IngestionApi/Validation/MeasurementFluentValidator.cs
- [x] T015 [US2] Write unit tests for MeasurementFluentValidator (each rule individually, multiple failures combined) in tests/IngestionApi.UnitTests/MeasurementFluentValidatorTests.cs

**Checkpoint**: Validator produces per-field structured errors. Problem Details mapping happens in US3 service integration.

---

## Phase 5: User Story 3 — Service Layer for Business Logic (Priority: P1)

**Goal**: Thin endpoint handlers delegate to IMeasurementService which orchestrates validation → store → event publish → logging

**Independent Test**: Unit test service with mocked store, validator, channel, and logger; verify orchestration logic

### Implementation for User Story 3

- [x] T016 [US3] Implement MeasurementService (inject IMeasurementStore, IValidator\<Measurement\>, IMeasurementEventChannel, ILogger) in src/IngestionApi/Services/MeasurementService.cs
- [x] T017 [US3] Register IMeasurementService, IValidator\<Measurement\>, and IMeasurementEventChannel in DI; update POST endpoint to delegate to service.AddAsync in src/IngestionApi/Program.cs
- [x] T018 [US3] Map validation failures from service to RFC 7807 Problem Details (TypedResults.Problem with errors dictionary) in POST endpoint in src/IngestionApi/Program.cs
- [x] T019 [US3] Write unit tests for MeasurementService (successful add stores + publishes event, validation failure throws/returns errors, query delegates to store) in tests/IngestionApi.UnitTests/MeasurementServiceTests.cs

**Checkpoint**: Endpoint handlers are thin. Business logic is in the service layer. Validation errors return RFC 7807.

---

## Phase 6: User Story 4 — Paginated Measurement Queries (Priority: P2)

**Goal**: GET endpoint supports skip/take with defaults; returns X-Total-Count and X-Has-More response headers

**Independent Test**: Insert multiple measurements, verify skip/take controls result window, verify headers present

### Implementation for User Story 4

- [x] T020 [US4] Implement pagination in MeasurementService.QueryAsync: filter by type/since from store, compute totalCount, apply skip/take, return (items, totalCount) in src/IngestionApi/Services/MeasurementService.cs
- [x] T021 [US4] Update GET endpoint to bind skip/take query params (with defaults 0/50, max take 500), call service.QueryAsync, add X-Total-Count and X-Has-More response headers; return 400 Problem Details for negative skip/take in src/IngestionApi/Program.cs

**Checkpoint**: Pagination works. Existing clients without skip/take get defaults applied. Headers are opt-in metadata.

---

## Phase 7: User Story 5 — Event-Driven Notifications (Priority: P2)

**Goal**: MeasurementEvent published to channel on successful ingestion; no blocking; subscribers can read asynchronously

**Independent Test**: Subscribe to channel, post measurement, verify event received with correct data

### Implementation for User Story 5

- [x] T022 [US5] Register MeasurementEventChannel as singleton IMeasurementEventChannel in DI and verify service publishes MeasurementEvent on successful AddAsync (fire-and-forget, no blocking on full channel) in src/IngestionApi/Program.cs

**Checkpoint**: Events flow through the channel. No subscribers needed for system to function.

---

## Phase 8: User Story 6 — Structured Logging for Observability (Priority: P2)

**Goal**: ILogger emits structured entries with deviceId, type, timestamp, and outcome for each ingestion request

**Independent Test**: Submit measurements and verify log entries contain expected structured properties

### Implementation for User Story 6

- [x] T023 [P] [US6] Add structured logging to MeasurementService: log successful ingestion (deviceId, type, timestamp) and validation failures (deviceId, failed fields) in src/IngestionApi/Services/MeasurementService.cs
- [x] T024 [P] [US6] Add structured logging to ApiKeyAuthFilter for authentication failures (missing/invalid key) in src/IngestionApi/Filters/ApiKeyAuthFilter.cs

**Checkpoint**: All ingestion, validation, and auth events produce structured log entries.

---

## Phase 9: User Story 7 — Health Check Enrichment (Priority: P3)

**Goal**: Replace manual /healthz with framework MapHealthChecks; same path, standard format

**Independent Test**: Query /healthz and verify 200 response with ASP.NET Health Checks format

### Implementation for User Story 7

- [x] T025 [US7] Register health check services (AddHealthChecks) and replace manual /healthz endpoint with app.MapHealthChecks("/healthz") using a custom ResponseWriter that outputs `{"status":"healthy"}` JSON (preserving backward compatibility with existing integration test assertion) in src/IngestionApi/Program.cs
- [x] T026 [US7] Remove the inline /healthz handler that was previously defined in src/IngestionApi/Program.cs (now handled by MapHealthChecks with compatible response format)

**Checkpoint**: /healthz uses framework health checks. Existing clients get 200 OK as before.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, backward compatibility verification, final validation

- [x] T027 [P] Remove deprecated src/IngestionApi/MeasurementValidator.cs (replaced by FluentValidation in T014)
- [x] T028 Run all existing integration tests (tests/IngestionApi.IntegrationTests/) to verify backward compatibility — all 6 tests must pass without modification
- [x] T029 Run quickstart.md validation scenarios to confirm end-to-end functionality

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories
- **US1 Auth (Phase 3)**: Depends on Phase 2 (needs IApiKeyValidator interface, route group)
- **US2 Validation (Phase 4)**: Depends on Phase 2 (needs folder structure)
- **US3 Service (Phase 5)**: Depends on Phase 3 (auth filter on group) + Phase 4 (validator implementation)
- **US4 Pagination (Phase 6)**: Depends on Phase 5 (needs service layer)
- **US5 Events (Phase 7)**: Depends on Phase 5 (service publishes events)
- **US6 Logging (Phase 8)**: Depends on Phase 5 (logging in service + filter)
- **US7 Health (Phase 9)**: Depends on Phase 2 (route structure) — can run in parallel with US4–US6
- **Polish (Phase 10)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Phase 2 → can start first
- **US2 (P1)**: Phase 2 → can start in parallel with US1
- **US3 (P1)**: Phase 3 + Phase 4 → depends on US1 and US2
- **US4 (P2)**: Phase 5 → depends on US3
- **US5 (P2)**: Phase 5 → depends on US3
- **US6 (P2)**: Phase 5 → depends on US3
- **US7 (P3)**: Phase 2 → independent of other stories (can run anytime after foundational)
- **US8 (P3)**: Implemented in Phase 2 (T009) as foundational infrastructure

### Parallel Opportunities

- T004, T005, T006, T007, T008 can all run in parallel (Phase 2, different files)
- T010 and T014 can run in parallel (US1 impl + US2 validator, different files)
- T013 and T015 can run in parallel (unit test files, different targets)
- T023 and T024 can run in parallel (logging in different files)
- T025 and T027 can run in parallel (health checks + cleanup)
- US7 (Phase 9) can run in parallel with US4/US5/US6 (Phases 6–8)

---

## Parallel Example: Phase 2 Foundation

```
All run in parallel (different files, no dependencies):
  T004: PaginatedQuery in Models/PaginatedQuery.cs
  T005: MeasurementEvent in Events/MeasurementEvent.cs
  T006: IApiKeyValidator in Services/IApiKeyValidator.cs
  T007: IMeasurementEventChannel + impl in Events/MeasurementEventChannel.cs
  T008: IMeasurementService in Services/IMeasurementService.cs

Then sequential:
  T009: Refactor Program.cs route groups (depends on all interfaces existing)
```

## Parallel Example: User Stories 1 & 2

```
In parallel (different files):
  T010: ApiKeyValidator in Services/ApiKeyValidator.cs
  T014: MeasurementFluentValidator in Validation/MeasurementFluentValidator.cs

Then in parallel (test files):
  T013: ApiKeyAuthFilterTests.cs
  T015: MeasurementFluentValidatorTests.cs
```

---

## Implementation Strategy

### MVP First (User Stories 1–3)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 — Centralized Auth
4. Complete Phase 4: US2 — Structured Validation
5. Complete Phase 5: US3 — Service Layer
6. **STOP and VALIDATE**: Run integration tests. MVP delivers auth, validation, service layer.

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Auth centralized → Run integration tests ✓
3. US2 + US3 → Validation + Service → Run integration tests ✓ (MVP!)
4. US4 → Pagination → Run integration tests ✓
5. US5 + US6 → Events + Logging → Run integration tests ✓
6. US7 → Health checks → Run integration tests ✓
7. Polish → Final validation ✓

### Critical Constraint

**Zero changes to**: Presentation/, MauiApp/, DesktopApp/, DeviceSimulator/ projects. Same HTTP contract (paths, port, request/response bodies). All 6 existing integration tests pass unmodified.
