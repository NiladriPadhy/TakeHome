namespace IngestionApi;

public static class MeasurementValidator
{
    public static bool IsValid(Measurement m) => m.MeasurementId != Guid.Empty && m.Timestamp != default && !string.IsNullOrWhiteSpace(m.DeviceId) && !string.IsNullOrWhiteSpace(m.Type);
}

