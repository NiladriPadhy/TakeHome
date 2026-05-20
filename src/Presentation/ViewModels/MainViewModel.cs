using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Presentation.Models;
using Presentation.Services;

namespace Presentation.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly MeasurementService _measurementService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly System.Timers.Timer _pollTimer;
    private readonly SynchronizationContext? _syncContext;

    [ObservableProperty]
    private ObservableCollection<MeasurementDto> _measurements = [];

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    public MainViewModel(MeasurementService measurementService, ILogger<MainViewModel> logger)
    {
        _measurementService = measurementService;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;

        _pollTimer = new System.Timers.Timer(1000);
        _pollTimer.Elapsed += async (_, _) => await PollAsync();
        _pollTimer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            var results = await _measurementService.GetMeasurementsAsync();
            UpdateOnUI(() =>
            {
                if (!MeasurementsEqual(results))
                {
                    Measurements = new ObservableCollection<MeasurementDto>(results);
                }
                StatusMessage = $"Loaded {results.Count} measurements at {DateTimeOffset.Now:T}";
            });
            _logger.LogDebug("Refreshed {Count} measurements", results.Count);
        }
        catch (HttpRequestException ex)
        {
            UpdateOnUI(() => StatusMessage = $"Error: {ex.Message}");
            _logger.LogWarning(ex, "Failed to fetch measurements");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool MeasurementsEqual(IReadOnlyList<MeasurementDto> incoming)
    {
        if (Measurements.Count != incoming.Count) return false;
        for (int i = 0; i < incoming.Count; i++)
        {
            if (Measurements[i].MeasurementId != incoming[i].MeasurementId)
                return false;
        }
        return true;
    }

    private async Task PollAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Poll cycle failed");
        }
    }

    private void UpdateOnUI(Action action)
    {
        if (_syncContext is not null)
            _syncContext.Post(_ => action(), null);
        else
            action();
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
    }
}
