using System.Threading.Channels;

namespace IngestionApi.Events;

public interface IMeasurementEventChannel
{
    ValueTask PublishAsync(MeasurementEvent evt, CancellationToken ct = default);
    IAsyncEnumerable<MeasurementEvent> ReadAllAsync(CancellationToken ct = default);
}

public class MeasurementEventChannel : IMeasurementEventChannel
{
    private readonly Channel<MeasurementEvent> _channel = Channel.CreateBounded<MeasurementEvent>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask PublishAsync(MeasurementEvent evt, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(evt, ct);

    public IAsyncEnumerable<MeasurementEvent> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
