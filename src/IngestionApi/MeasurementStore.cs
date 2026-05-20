namespace IngestionApi;

public interface IMeasurementStore
{
    Task AddAsync(Measurement m);

    Task<IEnumerable<Measurement>> QueryAsync(string? type, DateTimeOffset since);
}

public class InMemoryStore : IMeasurementStore
{
    private readonly List<Measurement> _items = [];
    private readonly object _lock = new();

    public Task AddAsync(Measurement m) { lock (_lock) _items.Add(m); return Task.CompletedTask; }

    public Task<IEnumerable<Measurement>> QueryAsync(string? type, DateTimeOffset since)
    {
        IEnumerable<Measurement> q = _items.Where(x => x.Timestamp >= since);

        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(q.TakeLast(500)); // prevent overfetch
    }
}