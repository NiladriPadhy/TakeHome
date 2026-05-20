# API Contract: IngestionApi v1

**Base URL**: `http://localhost:5100`
**Authentication**: Header `x-api-key: local-dev`

## Endpoints

### GET /healthz

**Description**: Health check endpoint (unauthenticated).

**Request**: No body, no query parameters.

**Response**:
- `200 OK`
```json
{
  "status": "healthy"
}
```

---

### POST /api/v1/measurements

**Description**: Submit a single measurement from a device.

**Authentication**: Required (`x-api-key` header)

**Request Body** (`application/json`):
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

**Validation Rules**:
- `measurementId` must be a non-empty GUID
- `timestamp` must be a non-default DateTimeOffset
- `deviceId` must be non-blank
- `type` must be non-blank
- `value` must be a defined JSON value (not undefined/missing)

**Responses**:

| Status | Condition | Body |
|--------|-----------|------|
| `202 Accepted` | Valid payload + valid API key | Echo of the measurement; `Location` header: `/api/v1/measurements/{id}` |
| `400 Bad Request` | Validation failure | `"invalid measurement"` |
| `401 Unauthorized` | Missing or invalid `x-api-key` | Empty |

---

### GET /api/v1/measurements

**Description**: Query stored measurements with optional filters.

**Authentication**: Required (`x-api-key` header)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `type` | string | No | null (all types) | Filter by measurement type (case-insensitive) |
| `since` | DateTimeOffset | No | UtcNow - 5 minutes | Only return measurements after this timestamp |

**Response**:
- `200 OK`
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

**Constraints**:
- Maximum 500 results returned (most recent, via `TakeLast(500)`)
- Results filtered by `since` timestamp first, then by `type` if provided

| Status | Condition | Body |
|--------|-----------|------|
| `200 OK` | Valid request | Array of measurements (may be empty `[]`) |
| `401 Unauthorized` | Missing or invalid `x-api-key` | Empty |

---

## Error Format

Non-validation errors use ASP.NET Core Problem Details (`AddProblemDetails()` is configured).

Validation errors currently return plain string `"invalid measurement"` as the body (not Problem Details format).

## Versioning

API version is embedded in the path: `/api/v1/`. Breaking changes will introduce `/api/v2/` while maintaining v1 for backward compatibility per the constitution.
