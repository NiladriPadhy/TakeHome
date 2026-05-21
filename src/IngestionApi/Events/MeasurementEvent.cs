namespace IngestionApi.Events;

public record MeasurementEvent(
    Measurement Measurement,
    DateTimeOffset PublishedAt);
