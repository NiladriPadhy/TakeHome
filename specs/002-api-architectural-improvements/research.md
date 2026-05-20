# Research: API Architectural Improvements

## Decision 1: FluentValidation Integration Pattern

**Decision**: Use FluentValidation 11.x with manual invocation in the service layer (not auto-validation middleware).

**Rationale**: Manual validation gives explicit control over when validation runs, avoids magic middleware, and keeps the pipeline transparent. The service layer calls `validator.ValidateAsync()` and converts `ValidationResult` into Problem Details.

**Alternatives considered**:
- Auto-validation via `SharpGrip.FluentValidation.AutoValidation` — adds implicit middleware, less transparent
- DataAnnotations — limited expressiveness, no cross-field rules
- Custom static validator (current) — no per-field error messages

## Decision 2: Endpoint Filter vs Middleware for Auth

**Decision**: Use `IEndpointFilter` (minimal API filter) for API key validation.

**Rationale**: Endpoint filters are scoped to specific route groups (not global), compose well with MapGroup, and have access to endpoint metadata. This allows `/healthz` to remain unauthenticated while all `/api/v1/*` routes get the filter automatically.

**Alternatives considered**:
- Global middleware — would require path-based exclusion logic for health endpoint
- Authorization policies — overkill for a simple API key; would need a custom `AuthenticationHandler`
- Per-endpoint inline check (current) — duplicated, not testable in isolation

## Decision 3: Channel<T> for Event Notifications

**Decision**: Use `System.Threading.Channels.Channel<MeasurementEvent>` with bounded capacity (1000).

**Rationale**: Channels are in-box (no external dependency), purpose-built for async producer/consumer in .NET, support bounded capacity with backpressure/drop semantics, and integrate with `IAsyncEnumerable` via `ReadAllAsync()`. BoundedChannelFullMode.DropOldest prevents blocking the ingestion path.

**Alternatives considered**:
- IObservable/Subject<T> (Rx) — requires System.Reactive package, push-based model less natural for async consumers
- In-memory event bus (MediatR notifications) — adds heavy dependency for a simple pub/sub
- Direct SignalR hub call — couples ingestion to a specific consumer technology

## Decision 4: Pagination Response Headers

**Decision**: Return pagination metadata via `X-Total-Count` and `X-Has-More` response headers. Body remains a JSON array.

**Rationale**: Existing clients deserialize the GET response as `List<MeasurementDto>` — a top-level array. Changing to an envelope object would break them. Headers are opt-in metadata that existing clients safely ignore.

**Alternatives considered**:
- Envelope object `{ items, totalCount, hasMore }` — breaks existing clients
- New `/api/v2/measurements` endpoint — maintains v1 but doubles maintenance surface
- Link headers (RFC 5988) — more complex to parse, overkill for simple pagination

## Decision 5: Health Check Framework

**Decision**: Use `Microsoft.Extensions.Diagnostics.HealthChecks` with `MapHealthChecks("/healthz")`.

**Rationale**: Framework-native, supports custom health check classes, outputs standard response format, path stays at `/healthz` for backward compatibility. The custom health check verifies the in-memory store is responsive.

**Alternatives considered**:
- Keep manual `/healthz` endpoint — misses framework features (readiness/liveness distinction, dependency checks)
- Third-party health check library — unnecessary when framework provides everything needed

## Decision 6: Service Layer Granularity

**Decision**: Single `IMeasurementService` interface with `AddMeasurementAsync` and `QueryMeasurementsAsync` methods.

**Rationale**: Two operations (ingest + query) don't warrant separate service classes. A single interface keeps things simple (YAGNI/Principle VI) while still providing a testable boundary. The service handles validation, storage, event publishing, and logging.

**Alternatives considered**:
- MediatR commands/queries — adds 3rd-party dependency and boilerplate for 2 operations
- Separate `IIngestionService` + `IQueryService` — premature split, violates YAGNI
- No service layer (current) — endpoint handlers contain business logic, harder to unit test

## Decision 7: Route Group Organization

**Decision**: Single `MapGroup("/api/v1")` with auth filter applied, plus `/healthz` mapped outside the group (no auth).

**Rationale**: All API endpoints share the `/api/v1` prefix and require authentication. A route group centralizes the filter and prefix. Health check stays outside since it's unauthenticated.

**Alternatives considered**:
- Carter library — adds dependency for minimal benefit with only 2 endpoints
- Controller-based — contradicts Minimal API constraint in constitution
- No grouping (current) — duplicates filter logic per endpoint
