using FluentValidation;
using IngestionApi.Events;
using IngestionApi.Models;

namespace IngestionApi.Services;

public class MeasurementService : IMeasurementService
{
    private readonly IMeasurementStore _store;
    private readonly IValidator<Measurement> _validator;
    private readonly IMeasurementEventChannel _eventChannel;
    private readonly ILogger<MeasurementService> _logger;

    public MeasurementService(
        IMeasurementStore store,
        IValidator<Measurement> validator,
        IMeasurementEventChannel eventChannel,
        ILogger<MeasurementService> logger)
    {
        _store = store;
        _validator = validator;
        _eventChannel = eventChannel;
        _logger = logger;
    }

    public async Task<Measurement> AddAsync(Measurement measurement)
    {
        var validationResult = await _validator.ValidateAsync(measurement);

        if (!validationResult.IsValid)
        {
            var failedFields = string.Join(", ", validationResult.Errors.Select(e => e.PropertyName));
            _logger.LogWarning("Measurement validation failed for device {DeviceId}: fields [{FailedFields}]",
                measurement.DeviceId, failedFields);
            throw new ValidationException(validationResult.Errors);
        }

        await _store.AddAsync(measurement);

        _logger.LogInformation("Measurement ingested: DeviceId={DeviceId}, Type={Type}, Timestamp={Timestamp}",
            measurement.DeviceId, measurement.Type, measurement.Timestamp);

        var evt = new MeasurementEvent(measurement, DateTimeOffset.UtcNow);
        _ = _eventChannel.PublishAsync(evt);

        return measurement;
    }

    public async Task<(IReadOnlyList<Measurement> Items, int TotalCount)> QueryAsync(PaginatedQuery query)
    {
        var since = query.Since ?? DateTimeOffset.UtcNow.AddMinutes(-5);
        var all = await _store.QueryAsync(query.Type, since);
        var list = all.OrderByDescending(m => m.Timestamp).ToList();
        var totalCount = list.Count;
        var items = list.Skip(query.Skip).Take(query.Take).ToList();

        return (items, totalCount);
    }
}
