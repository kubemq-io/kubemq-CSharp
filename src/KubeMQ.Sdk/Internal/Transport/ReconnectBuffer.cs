using System.Threading.Channels;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Bounded buffer for messages published during the <c>Reconnecting</c> state.
/// Uses <see cref="Channel{T}"/> (per CS-35) with byte-level size tracking.
/// </summary>
internal sealed class ReconnectBuffer : IDisposable
{
    private readonly Channel<BufferedMessage> _channel;
    private readonly int _maxSizeBytes;
    private long _currentSizeBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnectBuffer"/> class.
    /// </summary>
    /// <param name="options">Reconnection configuration including buffer size and overflow mode.</param>
    internal ReconnectBuffer(ReconnectOptions options)
    {
        _maxSizeBytes = options.BufferSize;
        _channel = Channel.CreateBounded<BufferedMessage>(
            new BoundedChannelOptions(capacity: 10_000)
            {
                FullMode = options.BufferFullMode == BufferFullMode.Block
                    ? BoundedChannelFullMode.Wait
                    : BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    /// <summary>
    /// Disposes the channel writer.
    /// </summary>
    public void Dispose()
    {
        _channel.Writer.TryComplete();
    }

    internal async ValueTask EnqueueAsync(
        BufferedMessage message,
        CancellationToken ct)
    {
        int messageSize = message.EstimatedSizeBytes;

        if (Interlocked.Add(ref _currentSizeBytes, messageSize) > _maxSizeBytes)
        {
            Interlocked.Add(ref _currentSizeBytes, -messageSize);
            throw new KubeMQBufferFullException(
                $"Reconnect buffer full ({_maxSizeBytes} bytes)")
            {
                BufferSizeBytes = Interlocked.Read(ref _currentSizeBytes),
                BufferCapacityBytes = _maxSizeBytes,
            };
        }

        await _channel.Writer.WriteAsync(message, ct).ConfigureAwait(false);
    }

    internal async Task FlushAsync(
        Func<BufferedMessage, CancellationToken, Task> sendFunc,
        CancellationToken ct)
    {
        while (_channel.Reader.TryRead(out BufferedMessage message))
        {
            await sendFunc(message, ct).ConfigureAwait(false);
            Interlocked.Add(ref _currentSizeBytes, -message.EstimatedSizeBytes);
        }
    }

    internal int DiscardAll()
    {
        int count = 0;
        while (_channel.Reader.TryRead(out _))
        {
            count++;
        }

        Interlocked.Exchange(ref _currentSizeBytes, 0);
        return count;
    }
}
