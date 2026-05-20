# Feature Specification: API Architectural Improvements

**Feature Branch**: `002-api-architectural-improvements`

**Created**: 2026-05-21

**Status**: Draft

**Input**: User description: "Implement 10 architectural improvements to the IngestionApi to improve maintainability, testability, and API quality"

## Clarifications

### Session 2026-05-21

- Q: How should pagination metadata be returned while maintaining backward compatibility? → A: Response headers (X-Total-Count, X-Has-More); body remains a plain JSON array.
- Q: What mechanism should be used for in-process event notifications? → A: System.Threading.Channels.Channel<T> (bounded async queue with ChannelReader for consumers).
- Q: Should the health check endpoint preserve the existing /healthz path or adopt the ASP.NET default? → A: Map at /healthz (same path, framework-backed via MapHealthChecks).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Centralized API Key Authentication (Priority: P1)

As an API consumer (DeviceSimulator, MAUI/WPF clients), I continue to authenticate using the existing `x-api-key` header, but the API now validates credentials through a centralized, swappable authentication mechanism rather than inline code in each endpoint.

**Why this priority**: Authentication is a cross-cutting concern that touches all endpoints. Centralizing it unblocks all other improvements by establishing the filter/middleware pattern and makes the API immediately more testable.

**Independent Test**: Can be tested by sending requests with valid/invalid API keys and verifying the same 401/200 behavior as today, while also verifying that a test double can replace the validator in integration tests.

**Acceptance Scenarios**:

1. **Given** a request with a valid `x-api-key` header, **When** the request reaches any protected endpoint, **Then** the request proceeds to the endpoint handler and returns the expected response.
2. **Given** a request without an `x-api-key` header, **When** the request reaches any protected endpoint, **Then** the system returns 401 Unauthorized.
3. **Given** a request with an invalid `x-api-key` header, **When** the request reaches any protected endpoint, **Then** the system returns 401 Unauthorized.
4. **Given** a test environment, **When** a mock API key validator is registered, **Then** the system uses the mock instead of the real implementation.

---

### User Story 2 - Structured Validation Error Responses (Priority: P1)

As an API consumer, when I submit an invalid measurement, I receive a structured error response (RFC 7807 Problem Details) with per-field error messages so I can programmatically identify and fix the exact validation issue.

**Why this priority**: Structured error responses are essential for client developers to build reliable integrations. Combined with FluentValidation, this provides actionable feedback instead of a generic "invalid measurement" string.

**Independent Test**: Can be tested by submitting measurements with various invalid fields and verifying the response body contains RFC 7807 structure with field-level error details.

**Acceptance Scenarios**:

1. **Given** a measurement with an empty DeviceId, **When** submitted to the POST endpoint, **Then** the system returns 400 with a Problem Details response containing a validation error for the DeviceId field.
2. **Given** a measurement with multiple invalid fields, **When** submitted to the POST endpoint, **Then** the system returns 400 with a Problem Details response listing all field-level errors.
3. **Given** a measurement with all valid fields, **When** submitted to the POST endpoint, **Then** the system returns 202 Accepted as before.
4. **Given** any error response, **When** the client parses the body, **Then** the response contains `type`, `title`, `status`, and `errors` properties per RFC 7807.

---

### User Story 3 - Service Layer for Business Logic (Priority: P1)

As a developer maintaining the API, I interact with measurement ingestion and querying through a service layer that encapsulates business logic, making endpoint handlers thin and the system easier to test in isolation.

**Why this priority**: A service layer is foundational for testability and separation of concerns. It enables mocking at the service boundary for endpoint-level tests and provides a natural extension point for future cross-cutting logic (logging, events).

**Independent Test**: Can be tested by verifying that endpoint handlers delegate to the service interface, and that the service can be tested independently with a mocked store.

**Acceptance Scenarios**:

1. **Given** a valid measurement POST request, **When** processed by the endpoint, **Then** the endpoint delegates to the service layer which handles storage.
2. **Given** a GET request for measurements, **When** processed by the endpoint, **Then** the endpoint delegates to the service layer which handles querying.
3. **Given** a unit test targeting the service, **When** the store is mocked, **Then** the service logic (validation, storage, event publishing) can be tested without HTTP infrastructure.

---

### User Story 4 - Paginated Measurement Queries (Priority: P2)

As an API consumer querying measurements, I can paginate results using skip/take parameters so I can efficiently retrieve large datasets without overloading the client or server.

**Why this priority**: Pagination prevents unbounded result sets and improves client-side performance, but the existing 500-item cap provides a temporary safety net so this is not as urgent as foundational changes.

**Independent Test**: Can be tested by inserting multiple measurements and verifying that skip/take parameters control result windows and pagination metadata is present in the response.

**Acceptance Scenarios**:

1. **Given** 50 measurements in the store, **When** I query with `skip=0&take=10`, **Then** I receive the first 10 measurements and pagination metadata (totalCount, hasMore).
2. **Given** 50 measurements in the store, **When** I query with `skip=10&take=10`, **Then** I receive measurements 11-20.
3. **Given** no skip/take parameters, **When** I query measurements, **Then** the system uses sensible defaults (skip=0, take=50) and returns results with pagination metadata.
4. **Given** skip/take values that exceed the dataset, **When** I query, **Then** the system returns available results without error and indicates no more pages.
5. **Given** existing clients that do not send skip/take, **When** they query, **Then** they receive results (backward-compatible behavior with defaults applied).

---

### User Story 5 - Event-Driven Notifications (Priority: P2)

As a future consumer (e.g., a real-time dashboard), I can subscribe to measurement ingestion events so I receive notifications when new measurements are stored, enabling real-time updates without polling.

**Why this priority**: Event-driven architecture enables real-time features and decouples the ingestion path from downstream consumers, but existing clients do not depend on this yet.

**Independent Test**: Can be tested by subscribing to the event channel, posting a measurement, and verifying the event is received with correct measurement data.

**Acceptance Scenarios**:

1. **Given** a subscriber listening to measurement events, **When** a new measurement is successfully stored, **Then** the subscriber receives a notification containing the measurement data.
2. **Given** no subscribers, **When** a measurement is stored, **Then** the system does not block or fail (fire-and-forget semantics).
3. **Given** a measurement that fails validation, **When** it is rejected, **Then** no event is published.

---

### User Story 6 - Structured Logging for Observability (Priority: P2)

As an operations team member, I can observe measurement ingestion activity through structured log entries so I can monitor system health, debug issues, and track device activity.

**Why this priority**: Logging is essential for production operations and debugging, but the system functions correctly without it.

**Independent Test**: Can be tested by submitting measurements and verifying that structured log entries are produced with expected properties (deviceId, type, timestamp, outcome).

**Acceptance Scenarios**:

1. **Given** a valid measurement submission, **When** it is successfully stored, **Then** a structured log entry is emitted with device ID, measurement type, and timestamp.
2. **Given** an invalid measurement submission, **When** it fails validation, **Then** a structured log entry is emitted indicating the validation failure and which fields failed.
3. **Given** an unauthorized request, **When** the API key is invalid or missing, **Then** a structured log entry is emitted indicating the authentication failure.

---

### User Story 7 - Health Check Enrichment (Priority: P3)

As a deployment platform (Kubernetes, Azure App Service), I can query standardized health check endpoints for readiness and liveness probes so I can automatically manage the application lifecycle.

**Why this priority**: Replaces the manual `/healthz` with a framework-supported health check system, but the existing endpoint already satisfies basic needs.

**Independent Test**: Can be tested by querying health endpoints and verifying proper response format, status codes, and that dependency checks are included.

**Acceptance Scenarios**:

1. **Given** the API is running and healthy, **When** I query the health endpoint, **Then** I receive a 200 response indicating healthy status.
2. **Given** the health check system, **When** queried, **Then** the response follows the standard ASP.NET Health Checks response format.
3. **Given** existing clients that query `/healthz`, **When** they make the request, **Then** they still receive a successful response (backward compatibility).

---

### User Story 8 - Route Organization (Priority: P3)

As a developer, the API routes are organized using route groups so that shared configuration (filters, prefixes) is applied consistently and new endpoints can be added with minimal boilerplate.

**Why this priority**: This is a code organization improvement that reduces duplication and makes the codebase more maintainable but has no external behavioral change.

**Independent Test**: Can be tested by verifying all existing endpoints continue to respond at the same URLs with the same behavior after refactoring to route groups.

**Acceptance Scenarios**:

1. **Given** the refactored route registration, **When** any existing endpoint URL is called, **Then** the response is identical to the pre-refactoring behavior.
2. **Given** a shared filter on the route group, **When** any endpoint in the group is called, **Then** the filter logic (e.g., API key validation) is applied automatically.

---

### Edge Cases

- What happens when skip or take parameters are negative? System returns 400 with Problem Details explaining the constraint.
- What happens when skip exceeds total items? System returns empty results with totalCount and hasMore=false.
- What happens when the event channel is full? System does not block the ingestion path; events may be dropped with a warning log.
- What happens when an API key header is present but empty? System treats it as invalid and returns 401.
- What happens when the health check store dependency is unavailable? Health endpoint returns degraded/unhealthy status.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST validate API keys through a centralized endpoint filter rather than inline code in each handler.
- **FR-002**: System MUST expose API key validation behind a dependency-injected interface so implementations can be swapped for testing or alternate auth strategies.
- **FR-003**: System MUST validate measurements using structured, per-field validation rules that produce individual error messages for each invalid field.
- **FR-004**: System MUST return RFC 7807 Problem Details responses for all error conditions (validation failures, authentication failures, server errors).
- **FR-005**: System MUST delegate business logic from endpoint handlers to a service layer interface with methods for ingestion and querying.
- **FR-006**: System MUST support pagination on the GET measurements endpoint via `skip` and `take` query parameters with defaults of skip=0, take=50.
- **FR-007**: System MUST return pagination metadata (totalCount, hasMore) alongside measurement query results.
- **FR-008**: System MUST organize API routes using route groups to apply shared configuration (filters, route prefix) consistently.
- **FR-009**: System MUST emit structured log entries for measurement ingestion (success and failure), authentication events, and query operations.
- **FR-010**: System MUST publish domain events when measurements are successfully stored, allowing subscribers to receive real-time notifications.
- **FR-011**: System MUST use the framework-standard health check system for readiness/liveness probes.
- **FR-012**: System MUST maintain backward compatibility with existing clients — same URL paths, same port (5100), same HTTP contract for existing request/response shapes.
- **FR-013**: System MUST ensure all existing integration tests continue to pass without modification.
- **FR-014**: System MUST NOT modify the Presentation, MauiApp, or DesktopApp projects.

### Key Entities

- **Measurement**: Core data entity representing a device reading (MeasurementId, Timestamp, DeviceId, PatientId, Type, Value, Unit).
- **MeasurementEvent**: A notification published after a measurement is successfully stored, carrying the measurement data for downstream consumers.
- **PaginatedResult**: A wrapper around query results that includes the items collection plus pagination metadata (totalCount, hasMore, skip, take).
- **ValidationError**: A structured representation of per-field validation failures included in Problem Details responses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 10 architectural improvements are implemented and verifiable through the codebase structure.
- **SC-002**: All existing integration tests pass without modification, confirming backward compatibility.
- **SC-003**: Measurement validation errors provide per-field detail — clients can identify exactly which field failed and why.
- **SC-004**: API error responses conform to RFC 7807 structure and are machine-parseable by any HTTP client.
- **SC-005**: The service layer is independently testable — unit tests can exercise business logic without HTTP infrastructure or real storage.
- **SC-006**: Authentication logic is testable in isolation — a mock validator can replace the real one in tests.
- **SC-007**: Pagination allows clients to retrieve measurement subsets efficiently with metadata to navigate result pages.
- **SC-008**: Measurement ingestion events are observable by subscribers within the same process without polling.
- **SC-009**: Structured log entries contain device ID, measurement type, and operation outcome for each ingestion request.
- **SC-010**: Health check endpoint reports system readiness using the standard framework health check format.

## Assumptions

- The existing HTTP contract (URLs, ports, request/response shapes) is the system's public API and must not change for existing operations.
- The `x-api-key: local-dev` value remains the valid key for development; the interface-based approach allows future implementations (config-driven, secret store) without changing the contract.
- Pagination is additive — existing clients that omit skip/take parameters receive results with sensible defaults applied transparently.
- Pagination metadata (totalCount, hasMore) is returned via response headers (`X-Total-Count`, `X-Has-More`). The response body remains a plain JSON array — no envelope object.
- The event-driven notification system operates in-process (no external message broker required for this iteration).
- Domain types (Measurement, events, validation) remain in the IngestionApi project — no separate Domain project is created.
- FluentValidation is an acceptable third-party dependency for structured validation.
- The InMemoryStore remains the default store implementation; these improvements do not introduce a persistent database.
