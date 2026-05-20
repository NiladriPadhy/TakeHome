using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using IngestionApi.Events;
using IngestionApi.Models;
using IngestionApi.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IngestionApi.UnitTests;

public class MeasurementServiceTests
{
    private readonly IMeasurementStore _store = Substitute.For<IMeasurementStore>();
    private readonly IValidator<Measurement> _validator = Substitute.For<IValidator<Measurement>>();
    private readonly IMeasurementEventChannel _channel = Substitute.For<IMeasurementEventChannel>();
    private readonly ILogger<MeasurementService> _logger = Substitute.For<ILogger<MeasurementService>>();
    private readonly MeasurementService _service;

    public MeasurementServiceTests()
    {
        _service = new MeasurementService(_store, _validator, _channel, _logger);
    }

    private static Measurement ValidMeasurement() => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "device-001",
        "patient-001",
        "HeartRate",
        JsonDocument.Parse("72").RootElement,
        "bpm");

    [Fact]
    public async Task AddAsync_ValidMeasurement_StoresAndPublishesEvent()
    {
        var m = ValidMeasurement();
        _validator.ValidateAsync(m, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var result = await _service.AddAsync(m);

        Assert.Equal(m, result);
        await _store.Received(1).AddAsync(m);
        await _channel.Received(1).PublishAsync(Arg.Is<MeasurementEvent>(e => e.Measurement == m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_InvalidMeasurement_ThrowsValidationException()
    {
        var m = ValidMeasurement() with { DeviceId = "" };
        _validator.ValidateAsync(m, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("DeviceId", "must not be empty") }));

        await Assert.ThrowsAsync<ValidationException>(() => _service.AddAsync(m));
        await _store.DidNotReceive().AddAsync(Arg.Any<Measurement>());
        await _channel.DidNotReceive().PublishAsync(Arg.Any<MeasurementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_DelegatesToStoreWithPagination()
    {
        var items = Enumerable.Range(0, 20).Select(i =>
            ValidMeasurement() with { DeviceId = $"d-{i}" }).ToList();

        _store.QueryAsync("HeartRate", Arg.Any<DateTimeOffset>())
            .Returns(items.AsEnumerable());

        var query = new PaginatedQuery("HeartRate", null, Skip: 5, Take: 3);
        var (result, totalCount) = await _service.QueryAsync(query);

        Assert.Equal(20, totalCount);
        Assert.Equal(3, result.Count);
        Assert.Equal("d-5", result[0].DeviceId);
    }
}
