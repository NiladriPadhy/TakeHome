using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IngestionApi.IntegrationTests;

public class MeasurementApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MeasurementApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostValidMeasurement_Returns202Accepted()
    {
        var measurement = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = "device-001",
            PatientId = "patient-001",
            Type = "HeartRate",
            Value = 72,
            Unit = "bpm"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/measurements", measurement);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains("/api/v1/measurements/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PostInvalidMeasurement_Returns400BadRequest()
    {
        var measurement = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = "",  // invalid: empty
            PatientId = "patient-001",
            Type = "HeartRate",
            Value = 72,
            Unit = "bpm"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/measurements", measurement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithoutApiKey_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        // No x-api-key header

        var measurement = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = "device-001",
            PatientId = "patient-001",
            Type = "HeartRate",
            Value = 72,
            Unit = "bpm"
        };

        var response = await client.PostAsJsonAsync("/api/v1/measurements", measurement);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QueryByType_ReturnsFilteredResults()
    {
        // Post multiple types
        var heartRate = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = "device-001",
            PatientId = "patient-001",
            Type = "HeartRate",
            Value = 72,
            Unit = "bpm"
        };
        var temperature = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            DeviceId = "device-001",
            PatientId = "patient-001",
            Type = "Temperature",
            Value = 36.6,
            Unit = "C"
        };

        await _client.PostAsJsonAsync("/api/v1/measurements", heartRate);
        await _client.PostAsJsonAsync("/api/v1/measurements", temperature);

        var response = await _client.GetAsync("/api/v1/measurements?type=HeartRate");
        var json = await response.Content.ReadAsStringAsync();
        var results = JsonDocument.Parse(json).RootElement;

        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        foreach (var r in results.EnumerateArray())
        {
            Assert.Equal("HeartRate", r.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task QueryWithSinceParameter_ReturnsOnlyRecentMeasurements()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recentTimestamp = DateTimeOffset.UtcNow;

        var oldMeasurement = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = oldTimestamp,
            DeviceId = "device-time-test",
            PatientId = "patient-001",
            Type = "SpO2",
            Value = 98,
            Unit = "%"
        };
        var recentMeasurement = new
        {
            MeasurementId = Guid.NewGuid(),
            Timestamp = recentTimestamp,
            DeviceId = "device-time-test",
            PatientId = "patient-001",
            Type = "SpO2",
            Value = 97,
            Unit = "%"
        };

        await _client.PostAsJsonAsync("/api/v1/measurements", oldMeasurement);
        await _client.PostAsJsonAsync("/api/v1/measurements", recentMeasurement);

        var since = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o");
        var response = await _client.GetAsync($"/api/v1/measurements?since={Uri.EscapeDataString(since)}");
        var json = await response.Content.ReadAsStringAsync();
        var results = JsonDocument.Parse(json).RootElement;

        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        foreach (var r in results.EnumerateArray())
        {
            var ts = r.GetProperty("timestamp").GetDateTimeOffset();
            Assert.True(ts >= DateTimeOffset.UtcNow.AddMinutes(-1));
        }
    }
}