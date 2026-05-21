using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReceivedExtensions;
using Presentation.Models;
using Presentation.Services;
using Presentation.ViewModels;
using Xunit;

namespace Presentation.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly MeasurementService _mockService;
    private readonly ILogger<MainViewModel> _mockLogger;

    public MainViewModelTests()
    {
        _mockService = Substitute.ForPartsOf<MeasurementService>(new HttpClient());
        _mockLogger = Substitute.For<ILogger<MainViewModel>>();
    }

    [Fact]
    public async Task RefreshCommand_PopulatesMeasurements_WhenServiceReturnsData()
    {
        var measurements = new List<MeasurementDto>
        {
            new(Guid.NewGuid(), DateTimeOffset.UtcNow, "device-1", "patient-1", "HeartRate",
                JsonDocument.Parse("72").RootElement, "bpm"),
            new(Guid.NewGuid(), DateTimeOffset.UtcNow, "device-2", "patient-2", "SpO2",
                JsonDocument.Parse("98").RootElement, "%")
        };

        _mockService.GetMeasurementsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(measurements);

        using var vm = new MainViewModel(_mockService, _mockLogger);
        // Stop auto-poll so we can test manually
        await Task.Delay(50);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Measurements.Count);
        Assert.Contains("2 measurements", vm.StatusMessage);
    }

    [Fact]
    public async Task RefreshCommand_SetsErrorStatus_WhenServiceThrows()
    {
        _mockService.GetMeasurementsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        using var vm = new MainViewModel(_mockService, _mockLogger);
        await Task.Delay(50);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("Error", vm.StatusMessage);
    }

    [Fact]
    public async Task AutoPoll_InvokesServicePeriodically()
    {
        _mockService.GetMeasurementsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MeasurementDto>());

        using var vm = new MainViewModel(_mockService, _mockLogger);

        // Wait for at least 2 poll cycles (1s interval)
        await Task.Delay(2500);

        await _mockService.Received(Quantity.Within(2, 5))
            .GetMeasurementsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
    }
}
