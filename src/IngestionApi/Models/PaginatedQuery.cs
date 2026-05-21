namespace IngestionApi.Models;

public record PaginatedQuery(
    string? Type = null,
    DateTimeOffset? Since = null,
    int Skip = 0,
    int Take = 50);
