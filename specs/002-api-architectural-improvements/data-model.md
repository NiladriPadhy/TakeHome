# Data Model: API Architectural Improvements

## Entities

### Measurement (unchanged)

```csharp
public record Measurement(
    Guid MeasurementId,
    DateTimeOffset Timestamp,
    string DeviceId,
    string PatientId,
    string Type,
    JsonElement Value,
    string Unit);
```

**Unchanged** — existing entity remains as-is.

### MeasurementEvent (new)

```csharp
public record MeasurementEvent(
    Measurement Measurement,
    DateTimeOffset PublishedAt);
```

**Purpose**: Published to the event channel after a measurement is successfully stored.

**Relationships**: Contains the stored Measurement + timestamp of publication.

### PaginatedQuery (new)

```csharp
public record PaginatedQuery(
    string? Type,
    DateTimeOffset? Since,
    int Skip = 0,
    int Take = 50);
```

**Purpose**: Encapsulates query parameters for the GET measurements endpoint.

**Validation rules**:
- Skip >= 0
- Take >= 1, Take <= 500

### ValidationError (implicit via FluentValidation)

FluentValidation's `ValidationResult` provides per-field errors automatically. These are mapped to RFC 7807 `errors` dictionary in the Problem Details response:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "DeviceId": ["DeviceId must not be empty."],
    "MeasurementId": ["MeasurementId must not be equal to empty GUID."]
  }
}
```

## Interfaces

### IApiKeyValidator

```csharp
public interface IApiKeyValidator
{
    bool IsValid(string? apiKey);
}
```

**Implementation**: `ApiKeyValidator` checks against configured key value.

### IMeasurementService

```csharp
public interface IMeasurementService
{
    Task<Measurement> AddAsync(Measurement measurement);
    Task<(IReadOnlyList<Measurement> Items, int TotalCount)> QueryAsync(PaginatedQuery query);
}
```

**Implementation**: `MeasurementService` orchestrates validation → storage → event publishing → logging.

### IMeasurementEventChannel

```csharp
public interface IMeasurementEventChannel
{
    ValueTask PublishAsync(MeasurementEvent evt, CancellationToken ct = default);
    IAsyncEnumerable<MeasurementEvent> ReadAllAsync(CancellationToken ct = default);
}
```

**Implementation**: `MeasurementEventChannel` wraps a bounded `Channel<MeasurementEvent>` (capacity: 1000, DropOldest on full).

## State Transitions

```
Measurement submitted
    → Validation (FluentValidation)
        ├── FAIL → 400 Problem Details (no state change)
        └── PASS → Store.AddAsync()
                     → Event published to Channel
                     → 202 Accepted returned
```

## Store Interface (unchanged)

```csharp
public interface IMeasurementStore
{
    Task AddAsync(Measurement measurement);
    Task<IReadOnlyList<Measurement>> QueryAsync(string? type, DateTimeOffset since);
}
```

Extended internally to support count queries for pagination:
- `QueryAsync` filters first, then the service layer applies Skip/Take and calculates TotalCount.
