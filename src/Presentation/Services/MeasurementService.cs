using System.Net.Http.Json;
using Presentation.Models;

namespace Presentation.Services;

public class MeasurementService
{
    private readonly HttpClient _httpClient;

    public MeasurementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public virtual async Task<IReadOnlyList<MeasurementDto>> GetMeasurementsAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        var url = "/api/v1/measurements";
        if (!string.IsNullOrWhiteSpace(type))
            url += $"?type={Uri.EscapeDataString(type)}";

        var result = await _httpClient.GetFromJsonAsync<List<MeasurementDto>>(url, cancellationToken);
        return result ?? [];
    }

    public virtual async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/healthz", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
