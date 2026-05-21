# API Contract: IngestionApi v1 (Updated)

**Base URL**: `http://localhost:5100`
**Authentication**: Header `x-api-key: local-dev` (via centralized endpoint filter)

## Endpoints

### GET /healthz

**Description**: Health check endpoint (unauthenticated — outside route group).

**Request**: No body, no query parameters.

**Response**:
- `200 OK` (healthy)
- `503 Service Unavailable` (unhealthy)

Response format follows ASP.NET Health Checks standard output.

**Backward compatibility**: Same path, same 200 for healthy. Clients checking `response.IsSuccessStatusCode` continue to work.

---

### POST /api/v1/measurements

**Description**: Submit a single measurement from a device.

**Authentication**: Required (endpoint filter validates `x-api-key` header)

**Request Body** (`application/json`): Unchanged
```json
{
  "measurementId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2026-05-20T10:30:00+00:00",
  "deviceId": "sim-01",
  "patientId": "p-123",
  "type": "HeartRate",
  "value": 72,
  "unit": "bpm"
}
```

**Validation Rules** (now per-field via FluentValidation):
- `measurementId` must be a non-empty GUID
- `timestamp` must be a non-default DateTimeOffset
- `deviceId` must be non-blank
- `type` must be non-blank
- `value` must be a defined JSON value (not undefined/missing)

**Responses**:

| Status | Condition | Body |
|--------|-----------|------|
| `202 Accepted` | Valid payload + valid API key | Echo of the measurement; `Location` header: `/api/v1/measurements/{id}` |
| `400 Bad Request` | Validation failure | RFC 7807 Problem Details with per-field `errors` |
| `401 Unauthorized` | Missing or invalid `x-api-key` | Empty (returned by filter before handler) |

**400 Response Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "DeviceId": ["'Device Id' must not be empty."],
    "MeasurementId": ["'Measurement Id' must not be equal to '00000000-0000-0000-0000-000000000000'."]
  }
}
```

**Side effects**: On success, publishes `MeasurementEvent` to the event channel.

---

### GET /api/v1/measurements

**Description**: Query stored measurements with optional filters and pagination.

**Authentication**: Required (endpoint filter validates `x-api-key` header)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `type` | string | No | null (all types) | Filter by measurement type |
| `since` | DateTimeOffset | No | UtcNow - 5 minutes | Only return measurements after this timestamp |
| `skip` | int | No | 0 | Number of results to skip |
| `take` | int | No | 50 | Maximum results to return (max 500) |

**Response**:
- `200 OK`

**Response Body**: JSON array (unchanged format)
```json
[
  {
    "measurementId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "timestamp": "2026-05-20T10:30:00+00:00",
    "deviceId": "sim-01",
    "patientId": "p-123",
    "type": "HeartRate",
    "value": 72,
    "unit": "bpm"
  }
]
```

**Response Headers** (new, opt-in metadata):

| Header | Type | Description |
|--------|------|-------------|
| `X-Total-Count` | int | Total number of matching items (before skip/take) |
| `X-Has-More` | bool | Whether more results exist beyond this page |

| Status | Condition | Body |
|--------|-----------|------|
| `200 OK` | Valid request | Array of measurements (may be empty `[]`) |
| `400 Bad Request` | Invalid skip/take values (negative) | RFC 7807 Problem Details |
| `401 Unauthorized` | Missing or invalid `x-api-key` | Empty |

---

## Error Format

All error responses use RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Error Title",
  "status": 400,
  "detail": "Optional human-readable detail",
  "errors": { }
}
```

## Authentication Flow

```
Request → ApiKeyAuthFilter (IEndpointFilter)
  ├── No/Invalid x-api-key → 401 Unauthorized (short-circuit)
  └── Valid x-api-key → next(context) → endpoint handler
```

The filter is applied to the `/api/v1` route group. The `/healthz` endpoint is outside this group and requires no authentication.
