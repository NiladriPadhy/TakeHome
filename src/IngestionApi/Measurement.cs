namespace IngestionApi;

public record Measurement(Guid MeasurementId, DateTimeOffset Timestamp, string DeviceId, string PatientId, string Type, object Value, string Unit);
