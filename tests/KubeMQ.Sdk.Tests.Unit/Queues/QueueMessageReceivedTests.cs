using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Queues;

public class QueueMessageReceivedTests
{
    private static QueueMessageReceived CreateMessage(
        Func<long, CancellationToken, Task>? ackFunc = null,
        Func<long, CancellationToken, Task>? nackFunc = null,
        Func<long, string?, CancellationToken, Task>? requeueFunc = null,
        long sequence = 42)
    {
        return new QueueMessageReceived(
            channel: "test-channel",
            messageId: "msg-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "client-1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: ackFunc,
            nackFunc: nackFunc,
            requeueFunc: requeueFunc)
        {
            Sequence = sequence,
        };
    }

    [Fact]
    public async Task AckAsync_CallsDelegate_WithSequence()
    {
        long capturedSeq = -1;
        var msg = CreateMessage(
            ackFunc: (seq, _) => { capturedSeq = seq; return Task.CompletedTask; });

        await msg.AckAsync();

        capturedSeq.Should().Be(42);
    }

    [Fact]
    public async Task NackAsync_CallsDelegate_WithSequence()
    {
        long capturedSeq = -1;
        var msg = CreateMessage(
            nackFunc: (seq, _) => { capturedSeq = seq; return Task.CompletedTask; });

        await msg.NackAsync();

        capturedSeq.Should().Be(42);
    }

    [Fact]
    public async Task ReQueueAsync_CallsDelegate_WithSequenceAndChannel()
    {
        long capturedSeq = -1;
        string? capturedChannel = null;
        var msg = CreateMessage(
            requeueFunc: (seq, ch, _) =>
            {
                capturedSeq = seq;
                capturedChannel = ch;
                return Task.CompletedTask;
            });

        await msg.ReQueueAsync("other-ch");

        capturedSeq.Should().Be(42);
        capturedChannel.Should().Be("other-ch");
    }

    [Fact]
    public async Task AckAsync_NoDelegate_ThrowsInvalidOperationException()
    {
        var msg = CreateMessage(ackFunc: null);

        var act = () => msg.AckAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ack*not available*");
    }

    [Fact]
    public async Task NackAsync_NoDelegate_ThrowsInvalidOperationException()
    {
        var msg = CreateMessage(nackFunc: null);

        var act = () => msg.NackAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nack*not available*");
    }

    [Fact]
    public async Task AckAsync_AlreadySettled_ThrowsInvalidOperationException()
    {
        var msg = CreateMessage(
            ackFunc: (_, _) => Task.CompletedTask);
        await msg.AckAsync();

        var act = () => msg.AckAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been settled*");
    }

    [Fact]
    public async Task NackAsync_AfterAck_ThrowsInvalidOperationException()
    {
        var msg = CreateMessage(
            ackFunc: (_, _) => Task.CompletedTask,
            nackFunc: (_, _) => Task.CompletedTask);
        await msg.AckAsync();

        var act = () => msg.NackAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been settled*");
    }

    [Fact]
    public void Properties_ReturnConstructorValues()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var msg = new QueueMessageReceived(
            channel: "ch",
            messageId: "id-1",
            body: new byte[] { 1 },
            tags: new Dictionary<string, string> { ["k"] = "v" },
            clientId: "c1",
            metadata: "meta",
            receiveCount: 3,
            timestamp: ts,
            ackFunc: null,
            nackFunc: null,
            requeueFunc: null)
        {
            Sequence = 99,
            MD5OfBody = "abc123",
        };

        msg.Timestamp.Should().Be(ts);
        msg.ReceiveCount.Should().Be(3);
        msg.Metadata.Should().Be("meta");
        msg.Sequence.Should().Be(99);
        msg.MD5OfBody.Should().Be("abc123");
    }
}
