using IngestionApi.Models;

namespace IngestionApi.Services;

public interface IMeasurementService
{
    Task<Measurement> AddAsync(Measurement measurement);
    Task<(IReadOnlyList<Measurement> Items, int TotalCount)> QueryAsync(PaginatedQuery query);
}
