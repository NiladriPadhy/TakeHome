using System.Text.Json;

namespace IngestionApi;

public record Measurement(
    Guid MeasurementId,
    DateTimeOffset Timestamp,
    string DeviceId,
    string PatientId,
    string Type,
    JsonElement Value,
    string Unit);
