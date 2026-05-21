using System.Text.Json;
using IngestionApi.Validation;
using Xunit;

namespace IngestionApi.UnitTests;

public class MeasurementFluentValidatorTests
{
    private readonly MeasurementFluentValidator _validator = new();

    private static Measurement ValidMeasurement() => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "device-001",
        "patient-001",
        "HeartRate",
        JsonDocument.Parse("72").RootElement,
        "bpm");

    [Fact]
    public void ValidMeasurement_PassesValidation()
    {
        var result = _validator.Validate(ValidMeasurement());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyGuid_FailsValidation()
    {
        var m = ValidMeasurement() with { MeasurementId = Guid.Empty };
        var result = _validator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MeasurementId");
    }

    [Fact]
    public void DefaultTimestamp_FailsValidation()
    {
        var m = ValidMeasurement() with { Timestamp = default };
        var result = _validator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public void EmptyDeviceId_FailsValidation()
    {
        var m = ValidMeasurement() with { DeviceId = "" };
        var result = _validator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DeviceId");
    }

    [Fact]
    public void EmptyType_FailsValidation()
    {
        var m = ValidMeasurement() with { Type = "" };
        var result = _validator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
    }

    [Fact]
    public void MultipleInvalidFields_ReturnsAllErrors()
    {
        var m = ValidMeasurement() with { DeviceId = "", Type = "", MeasurementId = Guid.Empty };
        var result = _validator.Validate(m);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }
}
