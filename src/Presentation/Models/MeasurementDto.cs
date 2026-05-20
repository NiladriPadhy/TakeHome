using System.Text.Json;

namespace Presentation.Models;

public record MeasurementDto(
    Guid MeasurementId,
    DateTimeOffset Timestamp,
    string DeviceId,
    string PatientId,
    string Type,
    JsonElement Value,
    string Unit);
