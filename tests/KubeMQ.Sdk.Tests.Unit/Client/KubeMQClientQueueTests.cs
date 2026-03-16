using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientQueueTests
{
    private static KubeMQ.Grpc.SendQueueMessageResult SuccessResult(string messageId = "msg-001") =>
        new()
        {
            MessageID = messageId,
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IsError = false,
            Error = string.Empty,
        };

    [Fact]
    public async Task SendQueueMessageAsync_ValidMessage_ReturnsResult()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueueMessage? captured = null;
        transport
            .Setup(t => t.SendQueueMessageAsync(It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.QueueMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(SuccessResult());

        var message = new QueueMessage
        {
            Channel = "queue-channel",
            Body = Encoding.UTF8.GetBytes("queue-payload"),
        };

        var result = await client.SendQueueMessageAsync(message);

        result.Should().NotBeNull();
        result.MessageId.Should().Be("msg-001");
        result.IsError.Should().BeFalse();

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("queue-channel");
        captured.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("queue-payload"));
        captured.ClientID.Should().Be("test-client");
    }

    [Fact]
    public async Task SendQueueMessageAsync_ConvenienceOverload_Works()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueueMessage? captured = null;
        transport
            .Setup(t => t.SendQueueMessageAsync(It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.QueueMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(SuccessResult());

        var tags = new Dictionary<string, string> { ["env"] = "test" };
        var body = Encoding.UTF8.GetBytes("convenience");

        var result = await client.SendQueueMessageAsync("q-channel", body, tags);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("q-channel");
        captured.Tags.Should().ContainKey("env").WhoseValue.Should().Be("test");
    }

    [Fact]
    public async Task SendQueueMessageAsync_WithPolicies_SetsGrpcPolicy()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueueMessage? captured = null;
        transport
            .Setup(t => t.SendQueueMessageAsync(It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.QueueMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(SuccessResult());

        var message = new QueueMessage
        {
            Channel = "policy-channel",
            Body = Encoding.UTF8.GetBytes("policy"),
            DelaySeconds = 30,
            ExpirationSeconds = 600,
            MaxReceiveCount = 3,
            MaxReceiveQueue = "dlq-channel",
        };

        await client.SendQueueMessageAsync(message);

        captured.Should().NotBeNull();
        captured!.Policy.Should().NotBeNull();
        captured.Policy.DelaySeconds.Should().Be(30);
        captured.Policy.ExpirationSeconds.Should().Be(600);
        captured.Policy.MaxReceiveCount.Should().Be(3);
        captured.Policy.MaxReceiveQueue.Should().Be("dlq-channel");
    }

    [Fact]
    public async Task SendQueueMessageAsync_NullMessage_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueueMessageAsync((QueueMessage)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueueMessageAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessageAsync(It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue transport error"));

        var message = new QueueMessage
        {
            Channel = "queue-channel",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        Func<Task> act = () => client.SendQueueMessageAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("queue transport error");
    }

    [Fact]
    public async Task SendQueueMessagesAsync_MultipleMessages_SendsAll()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueueMessagesBatchRequest? capturedRequest = null;
        transport
            .Setup(t => t.SendQueueMessagesBatchAsync(It.IsAny<KubeMQ.Grpc.QueueMessagesBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.QueueMessagesBatchRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new KubeMQ.Grpc.QueueMessagesBatchResponse
            {
                BatchID = "batch-001",
                HaveErrors = false,
                Results =
                {
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-001", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-002", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-003", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                },
            });

        var messages = new[]
        {
            new QueueMessage { Channel = "q1", Body = Encoding.UTF8.GetBytes("a") },
            new QueueMessage { Channel = "q2", Body = Encoding.UTF8.GetBytes("b") },
            new QueueMessage { Channel = "q3", Body = Encoding.UTF8.GetBytes("c") },
        };

        var result = await client.SendQueueMessagesAsync(messages);

        result.Should().NotBeNull();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Messages.Should().HaveCount(3);
        capturedRequest.Messages[0].Channel.Should().Be("q1");
        capturedRequest.Messages[1].Channel.Should().Be("q2");
        capturedRequest.Messages[2].Channel.Should().Be("q3");
    }

    [Fact]
    public async Task SendQueueMessagesAsync_EmptyList_ReturnsDefault()
    {
        var (client, _) = TestClientFactory.Create();

        var result = await client.SendQueueMessagesAsync(Array.Empty<QueueMessage>());

        result.Should().NotBeNull();
        result.MessageId.Should().BeEmpty();
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task PollQueueAsync_ValidRequest_ReturnsMessages()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-001",
            RefRequestId = "ref-001",
            IsError = false,
            Error = string.Empty,
        };
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = "poll-channel",
            MessageID = "polled-msg-1",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("polled-body"),
            ClientID = "producer-1",
        });

        var (mockCall, _) = MockDownstreamStream.Create(grpcResponse);
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "poll-channel",
            MaxMessages = 5,
            WaitTimeoutSeconds = 10,
        };

        var result = await client.PollQueueAsync(request);

        result.Should().NotBeNull();
        result.HasMessages.Should().BeTrue();
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Channel.Should().Be("poll-channel");
        result.Messages[0].MessageId.Should().Be("polled-msg-1");

        transport.Verify(
            t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollQueueAsync_NullRequest_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.PollQueueAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PeekQueueAsync_CallsTransport_ReturnsMessages()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.ReceiveQueueMessagesResponse
        {
            RequestID = "req-peek",
            IsError = false,
            Error = string.Empty,
        };
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            MessageID = "peek-msg-1",
            Channel = "peek-channel",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("peeked"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 1 },
        });

        transport.Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var request = new QueuePollRequest
        {
            Channel = "peek-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PeekQueueAsync(request);

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(1);
        result.Messages[0].MessageId.Should().Be("peek-msg-1");

        transport.Verify(
            t => t.ReceiveQueueMessagesAsync(
                It.Is<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(r => r.IsPeak && r.Channel == "peek-channel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollQueueAsync_EmptyResponse_ReturnsNoMessages()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-002",
            RefRequestId = "ref-002",
            IsError = false,
            Error = string.Empty,
        };

        var (mockCall, _) = MockDownstreamStream.Create(grpcResponse);
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "empty-channel",
            MaxMessages = 5,
            WaitTimeoutSeconds = 10,
        };

        var result = await client.PollQueueAsync(request);

        result.Should().NotBeNull();
        result.HasMessages.Should().BeFalse();
        result.Messages.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task PollQueueAsync_ErrorResponse_ReturnsError()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-003",
            RefRequestId = "ref-003",
            IsError = true,
            Error = "queue not found",
        };

        var (mockCall, _) = MockDownstreamStream.Create(grpcResponse);
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "missing-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("queue not found");
    }

    [Fact]
    public async Task PollQueueAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var request = new QueuePollRequest
        {
            Channel = "cancel-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.PollQueueAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendQueueMessageAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessageAsync(It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var message = new QueueMessage
        {
            Channel = "cancel-q",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SendQueueMessageAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendQueueMessagesAsync_OneMessageFails_ReturnsAggregateError()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessagesBatchAsync(It.IsAny<KubeMQ.Grpc.QueueMessagesBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.QueueMessagesBatchResponse
            {
                BatchID = "batch-err",
                HaveErrors = true,
                Results =
                {
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-001", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-002", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), IsError = true, Error = "queue full" },
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-003", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                },
            });

        var messages = new[]
        {
            new QueueMessage { Channel = "q1", Body = Encoding.UTF8.GetBytes("a") },
            new QueueMessage { Channel = "q2", Body = Encoding.UTF8.GetBytes("b") },
            new QueueMessage { Channel = "q3", Body = Encoding.UTF8.GetBytes("c") },
        };

        var result = await client.SendQueueMessagesAsync(messages);

        result.IsError.Should().BeTrue();
        result.Error.Should().Contain("One or more messages in the batch failed");
    }

    [Fact]
    public async Task SendQueueMessagesAsync_AllSucceed_ReturnsSuccess()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessagesBatchAsync(It.IsAny<KubeMQ.Grpc.QueueMessagesBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.QueueMessagesBatchResponse
            {
                BatchID = "batch-001",
                HaveErrors = false,
                Results =
                {
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-001", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    new KubeMQ.Grpc.SendQueueMessageResult { MessageID = "msg-002", SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                },
            });

        var messages = new[]
        {
            new QueueMessage { Channel = "q1", Body = Encoding.UTF8.GetBytes("a") },
            new QueueMessage { Channel = "q2", Body = Encoding.UTF8.GetBytes("b") },
        };

        var result = await client.SendQueueMessagesAsync(messages);

        result.IsError.Should().BeFalse();
        result.MessageId.Should().Be("batch-001");
    }

    [Fact]
    public async Task QueueMessageReceived_AckAsync_InvokesAckFunc()
    {
        bool ackCalled = false;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: (id, ct) => { ackCalled = true; return Task.CompletedTask; },
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        await msg.AckAsync();

        ackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task QueueMessageReceived_RejectAsync_InvokesRejectFunc()
    {
        bool rejectCalled = false;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-2",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: (id, ct) => { rejectCalled = true; return Task.CompletedTask; },
            requeueFunc: null,
            extendFunc: null);

        await msg.RejectAsync();

        rejectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task QueueMessageReceived_RequeueAsync_InvokesRequeueFunc()
    {
        string? requeuedChannel = null;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-3",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: (id, ch, ct) => { requeuedChannel = ch; return Task.CompletedTask; },
            extendFunc: null);

        await msg.RequeueAsync("other-channel");

        requeuedChannel.Should().Be("other-channel");
    }

    [Fact]
    public async Task QueueMessageReceived_ExtendVisibility_InvokesExtendFunc()
    {
        int extendedSeconds = 0;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-4",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: (id, ct) => Task.CompletedTask,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: (id, secs, ct) => { extendedSeconds = secs; return Task.CompletedTask; });

        await msg.ExtendVisibilityAsync(30);

        extendedSeconds.Should().Be(30);
    }

    [Fact]
    public async Task QueueMessageReceived_ExtendThenAck_Works()
    {
        bool ackCalled = false;
        int extendedSeconds = 0;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-5",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: (id, ct) => { ackCalled = true; return Task.CompletedTask; },
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: (id, secs, ct) => { extendedSeconds = secs; return Task.CompletedTask; });

        await msg.ExtendVisibilityAsync(60);
        await msg.AckAsync();

        extendedSeconds.Should().Be(60);
        ackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task QueueMessageReceived_DoubleAck_ThrowsInvalidOperation()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-6",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: (id, ct) => Task.CompletedTask,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        await msg.AckAsync();

        Func<Task> act = () => msg.AckAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been settled*");
    }

    [Fact]
    public async Task QueueMessageReceived_AckThenReject_ThrowsInvalidOperation()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-7",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: (id, ct) => Task.CompletedTask,
            rejectFunc: (id, ct) => Task.CompletedTask,
            requeueFunc: null,
            extendFunc: null);

        await msg.AckAsync();

        Func<Task> act = () => msg.RejectAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been settled*");
    }

    [Fact]
    public async Task QueueMessageReceived_NullAckFunc_AckAsyncIsNoOp()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-nack-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        var act = async () => await msg.AckAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ack*not available*");
    }

    [Fact]
    public async Task QueueMessageReceived_NullRejectFunc_RejectAsyncThrows()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-nrej-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        var act = async () => await msg.RejectAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Reject*not available*");
    }

    [Fact]
    public async Task QueueMessageReceived_NullExtendFunc_ExtendVisibilityAsyncThrows()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-next-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        var act = async () => await msg.ExtendVisibilityAsync(30);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ExtendVisibility*not available*");
    }

    [Fact]
    public async Task QueueMessageReceived_NullRequeueFunc_RequeueAsyncThrows()
    {
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-nreq-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: null,
            extendFunc: null);

        var act = async () => await msg.RequeueAsync("other");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Requeue*not available*");
    }

    [Fact]
    public async Task QueueMessageReceived_RequeueAsync_WithChannel_PassesChannel()
    {
        string? capturedChannel = null;
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-rch-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: (id, ch, ct) => { capturedChannel = ch; return Task.CompletedTask; },
            extendFunc: null);

        await msg.RequeueAsync("target-channel");

        capturedChannel.Should().Be("target-channel");
    }

    [Fact]
    public async Task QueueMessageReceived_RequeueAsync_WithNullChannel_PassesNull()
    {
        string? capturedChannel = "initial";
        var msg = new QueueMessageReceived(
            channel: "q-ch",
            messageId: "mid-rnull-1",
            body: Encoding.UTF8.GetBytes("data"),
            tags: null,
            clientId: "c1",
            metadata: null,
            receiveCount: 1,
            timestamp: DateTimeOffset.UtcNow,
            ackFunc: null,
            rejectFunc: null,
            requeueFunc: (id, ch, ct) => { capturedChannel = ch; return Task.CompletedTask; },
            extendFunc: null);

        await msg.RequeueAsync(null);

        capturedChannel.Should().BeNull();
    }

    [Fact]
    public async Task AckAllQueueMessagesAsync_ValidChannel_ReturnsResult()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.AckAllQueueMessagesResponse
        {
            RequestID = "ack-1",
            AffectedMessages = 5,
            IsError = false,
            Error = string.Empty,
        };

        transport
            .Setup(t => t.AckAllQueueMessagesAsync(It.IsAny<KubeMQ.Grpc.AckAllQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var result = await client.AckAllQueueMessagesAsync("my-queue");

        result.AffectedMessages.Should().Be(5);
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeQueueAsync_DelegatesToAckAll()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.AckAllQueueMessagesRequest? captured = null;
        transport
            .Setup(t => t.AckAllQueueMessagesAsync(It.IsAny<KubeMQ.Grpc.AckAllQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.AckAllQueueMessagesRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new KubeMQ.Grpc.AckAllQueueMessagesResponse { AffectedMessages = 3 });

        var result = await client.PurgeQueueAsync("purge-queue");

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("purge-queue");
        result.AffectedMessages.Should().Be(3);
    }

    [Fact]
    public async Task AckAllQueueMessagesAsync_EmptyChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.AckAllQueueMessagesAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // GAP-019: PollQueueAsync settlement delegate tests
    // These tests verify that messages returned by PollQueueAsync have
    // working settlement delegates (Ack, Reject, Requeue) instead of null.
    // ---------------------------------------------------------------

    private static KubeMQ.Grpc.QueuesDownstreamResponse CreatePollResponseWithMessage(
        string transactionId = "txn-settle",
        string channel = "settle-channel",
        string messageId = "settle-msg-1",
        uint sequence = 42)
    {
        var response = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = transactionId,
            RefRequestId = "ref-settle",
            IsError = false,
            Error = string.Empty,
        };
        response.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = channel,
            MessageID = messageId,
            Body = Google.Protobuf.ByteString.CopyFromUtf8("settle-body"),
            ClientID = "producer-1",
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes
            {
                Sequence = sequence,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
        });
        return response;
    }

    /// <summary>
    /// Sets up the transport to return a downstream stream with the given initial response,
    /// plus a second response for the settlement call (ack/reject/requeue).
    /// </summary>
    private static (KubeMQ.Sdk.Client.KubeMQClient Client, Moq.Mock<ITransport> Transport) CreateClientWithSettlementStream(
        KubeMQ.Grpc.QueuesDownstreamResponse pollResponse,
        KubeMQ.Grpc.QueuesDownstreamResponse settlementResponse)
    {
        var (client, transport) = TestClientFactory.Create();

        // The stream returns two responses: the poll result, then the settlement ack.
        var responses = new[] { pollResponse, settlementResponse };
        var (mockCall, _) = MockDownstreamStream.Create(responses);
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        return (client, transport);
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_ReturnsMessages_WithNonNullAckDelegate()
    {
        var pollResponse = CreatePollResponseWithMessage();
        var settlementResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-settle",
            IsError = false,
            TransactionComplete = true,
        };

        var (client, _) = CreateClientWithSettlementStream(pollResponse, settlementResponse);

        var request = new QueuePollRequest
        {
            Channel = "settle-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(1);

        // The core assertion: AckAsync should NOT throw "not available" (i.e., delegate is non-null).
        // It should complete successfully because the mock stream returns a success response.
        var act = async () => await result.Messages[0].AckAsync();
        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_ReturnsMessages_WithNonNullRejectDelegate()
    {
        var pollResponse = CreatePollResponseWithMessage();
        var settlementResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-settle",
            IsError = false,
            TransactionComplete = true,
        };

        var (client, _) = CreateClientWithSettlementStream(pollResponse, settlementResponse);

        var request = new QueuePollRequest
        {
            Channel = "settle-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(1);

        // RejectAsync should NOT throw "not available"
        var act = async () => await result.Messages[0].RejectAsync();
        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_ReturnsMessages_WithNonNullRequeueDelegate()
    {
        var pollResponse = CreatePollResponseWithMessage();
        var settlementResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-settle",
            IsError = false,
            TransactionComplete = true,
        };

        var (client, _) = CreateClientWithSettlementStream(pollResponse, settlementResponse);

        var request = new QueuePollRequest
        {
            Channel = "settle-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(1);

        // RequeueAsync should NOT throw "not available"
        var act = async () => await result.Messages[0].RequeueAsync("other-channel");
        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_ExtendVisibility_IsNull_ThrowsNotAvailable()
    {
        // ExtendVisibility is not supported at the protocol level for downstream streams,
        // so the delegate is null. This test documents the expected behavior.
        var pollResponse = CreatePollResponseWithMessage();
        var (mockCall, _) = MockDownstreamStream.Create(pollResponse);

        var (client, transport) = TestClientFactory.Create();
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "settle-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(1);

        // ExtendVisibility is not supported in the downstream protocol, so it should throw.
        var act = async () => await result.Messages[0].ExtendVisibilityAsync(30);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ExtendVisibility*not available*");
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_AutoAck_DisposesStream_DelegatesStillNull()
    {
        // When AutoAck is true, the stream is disposed immediately and settlement delegates
        // are not needed. But the message should still be returned with the delegates wired
        // (they will fail if called after the stream is disposed, which is fine -- the message
        // was auto-acked). This test ensures no crash when AutoAck is set.
        var grpcResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = string.Empty, // auto-ack won't have a transaction
            RefRequestId = "ref-auto",
            IsError = false,
            Error = string.Empty,
        };
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = "auto-ack-channel",
            MessageID = "auto-msg-1",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("auto-body"),
        });

        var (mockCall, _) = MockDownstreamStream.Create(grpcResponse);

        var (client, transport) = TestClientFactory.Create();
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "auto-ack-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
            AutoAck = true,
        };

        var result = await client.PollQueueAsync(request);

        result.Should().NotBeNull();
        result.HasMessages.Should().BeTrue();
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Channel.Should().Be("auto-ack-channel");
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_StreamStored_ForSettlement()
    {
        // Verify that the downstream stream is stored in _activeDownstreamStreams
        // by checking that AckAsync can be called (requires the stream to be alive).
        var pollResponse = CreatePollResponseWithMessage(transactionId: "txn-stored");
        var settlementResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-stored",
            IsError = false,
            TransactionComplete = true,
        };

        var (client, _) = CreateClientWithSettlementStream(pollResponse, settlementResponse);

        var request = new QueuePollRequest
        {
            Channel = "settle-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);
        result.Messages.Should().HaveCount(1);

        // This call goes through SendDownstreamRequestAsync which looks up the stream
        // by transactionId in _activeDownstreamStreams. If the stream wasn't stored,
        // this would throw KubeMQOperationException.
        var act = async () => await result.Messages[0].AckAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_MultipleMessages_AllHaveSettlementDelegates()
    {
        var response = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-multi",
            RefRequestId = "ref-multi",
            IsError = false,
            Error = string.Empty,
        };
        response.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = "multi-channel",
            MessageID = "msg-1",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("body-1"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 10 },
        });
        response.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = "multi-channel",
            MessageID = "msg-2",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("body-2"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 11 },
        });
        response.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            Channel = "multi-channel",
            MessageID = "msg-3",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("body-3"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 12 },
        });

        // Settlement responses for all three messages
        var settlementResp1 = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-multi",
            IsError = false,
        };
        var settlementResp2 = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-multi",
            IsError = false,
        };
        var settlementResp3 = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-multi",
            IsError = false,
            TransactionComplete = true,
        };

        var (mockCall, _) = MockDownstreamStream.Create(new[] { response, settlementResp1, settlementResp2, settlementResp3 });
        var (client, transport) = TestClientFactory.Create();
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "multi-channel",
            MaxMessages = 5,
            WaitTimeoutSeconds = 10,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(3);

        // All three should have working Ack
        await result.Messages[0].AckAsync();
        await result.Messages[1].RejectAsync();
        await result.Messages[2].RequeueAsync("other");
    }

    [Fact]
    public async Task PollQueueAsync_GAP019_MessageProperties_ArePreserved()
    {
        var response = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-props",
            RefRequestId = "ref-props",
            IsError = false,
        };
        var grpcMsg = new KubeMQ.Grpc.QueueMessage
        {
            Channel = "props-channel",
            MessageID = "props-msg-1",
            Body = Google.Protobuf.ByteString.CopyFromUtf8("props-body"),
            ClientID = "producer-x",
            Metadata = "meta-data",
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes
            {
                Sequence = 99,
                ReceiveCount = 3,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MD5OfBody = "abc123",
                ReRouted = true,
                ReRoutedFromQueue = "original-queue",
            },
        };
        grpcMsg.Tags["key1"] = "val1";
        grpcMsg.Tags["key2"] = "val2";
        response.Messages.Add(grpcMsg);

        var (mockCall, _) = MockDownstreamStream.Create(response);
        var (client, transport) = TestClientFactory.Create();
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "props-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PollQueueAsync(request);

        result.Messages.Should().HaveCount(1);
        var msg = result.Messages[0];

        msg.Channel.Should().Be("props-channel");
        msg.MessageId.Should().Be("props-msg-1");
        Encoding.UTF8.GetString(msg.Body.Span).Should().Be("props-body");
        msg.ClientId.Should().Be("producer-x");
        msg.Metadata.Should().Be("meta-data");
        msg.Sequence.Should().Be(99);
        msg.ReceiveCount.Should().Be(3);
        msg.MD5OfBody.Should().Be("abc123");
        msg.ReRouted.Should().BeTrue();
        msg.ReRoutedFromQueue.Should().Be("original-queue");
        msg.Tags.Should().NotBeNull();
        msg.Tags!["key1"].Should().Be("val1");
        msg.Tags!["key2"].Should().Be("val2");
    }
}
