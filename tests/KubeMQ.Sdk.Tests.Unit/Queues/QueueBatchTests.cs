using System.Collections.Concurrent;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Queues;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Queues;

public class QueueBatchTests : IAsyncDisposable
{
    private readonly ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> _captured;
    private readonly DownstreamStreamHandle _handle;

    public QueueBatchTests()
    {
        var response = new KubeMQ.Grpc.QueuesDownstreamResponse { IsError = false };
        var (call, captured) = MockDownstreamStream.Create(new[] { response, response, response });
        _captured = captured;
        _handle = new DownstreamStreamHandle(call, "test-client", NullLogger.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _handle.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static QueueBatch CreateBatch(
        DownstreamStreamHandle? handle,
        string transactionId = "txn-1",
        IReadOnlyList<QueueMessageReceived>? messages = null,
        bool isError = false,
        string? error = null,
        string clientId = "test-client")
    {
        return new QueueBatch(
            transactionId,
            messages ?? Array.Empty<QueueMessageReceived>(),
            isError,
            error,
            handle,
            clientId);
    }

    [Fact]
    public async Task AckAllAsync_WritesAckAllRequest()
    {
        var batch = CreateBatch(_handle);

        await batch.AckAllAsync();

        // The write goes through Channel<T> asynchronously; give the writer loop time to process.
        await Task.Delay(200);

        _captured.Should().ContainSingle();
        _captured.TryDequeue(out var req).Should().BeTrue();
        req!.RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.AckAll);
        req.RefTransactionId.Should().Be("txn-1");
        req.ClientID.Should().Be("test-client");
    }

    [Fact]
    public async Task NackAllAsync_WritesNackAllRequest()
    {
        var batch = CreateBatch(_handle);

        await batch.NackAllAsync();

        // The write goes through Channel<T> asynchronously; give the writer loop time to process.
        await Task.Delay(200);

        _captured.Should().ContainSingle();
        _captured.TryDequeue(out var req).Should().BeTrue();
        req!.RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.NackAll);
        req.RefTransactionId.Should().Be("txn-1");
    }

    [Fact]
    public async Task ReQueueAllAsync_WritesReQueueAllRequest()
    {
        var batch = CreateBatch(_handle);

        await batch.ReQueueAllAsync("other-channel");

        // The write goes through Channel<T> asynchronously; give the writer loop time to process.
        await Task.Delay(200);

        _captured.Should().ContainSingle();
        _captured.TryDequeue(out var req).Should().BeTrue();
        req!.RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.ReQueueAll);
        req.RefTransactionId.Should().Be("txn-1");
        req.ReQueueChannel.Should().Be("other-channel");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReQueueAllAsync_EmptyChannel_ThrowsArgumentException(string channel)
    {
        var batch = CreateBatch(_handle);

        var act = () => batch.ReQueueAllAsync(channel);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AckAllAsync_AutoAckBatch_ThrowsInvalidOperationException()
    {
        var batch = CreateBatch(handle: null);

        var act = () => batch.AckAllAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AutoAck*");
    }

    [Fact]
    public async Task AckAllAsync_DoubleSettle_ThrowsInvalidOperationException()
    {
        var batch = CreateBatch(_handle);
        await batch.AckAllAsync();

        var act = () => batch.NackAllAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been settled*");
    }

    [Fact]
    public void HasMessages_EmptyBatch_ReturnsFalse()
    {
        var batch = CreateBatch(_handle, messages: Array.Empty<QueueMessageReceived>());

        batch.HasMessages.Should().BeFalse();
    }

    [Fact]
    public void HasMessages_WithMessages_ReturnsTrue()
    {
        var msg = new QueueMessageReceived(
            "ch", "msg-1", System.Text.Encoding.UTF8.GetBytes("data"),
            tags: null, clientId: "c1", metadata: null,
            receiveCount: 1, timestamp: DateTimeOffset.UtcNow,
            ackFunc: null, nackFunc: null, requeueFunc: null);

        var batch = CreateBatch(_handle, messages: new[] { msg });

        batch.HasMessages.Should().BeTrue();
    }
}
