using System.Collections.Concurrent;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Queues;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Queues;

public class QueueDownstreamReceiverTests
{
    private static KubeMQ.Grpc.QueuesDownstreamResponse CreateResponseWithMessages(
        string transactionId = "txn-001",
        int messageCount = 1,
        string channel = "test-channel",
        bool isError = false,
        string? error = null)
    {
        var response = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = transactionId,
            IsError = isError,
            Error = error ?? string.Empty,
        };

        for (var i = 0; i < messageCount; i++)
        {
            response.Messages.Add(new KubeMQ.Grpc.QueueMessage
            {
                Channel = channel,
                MessageID = $"msg-{i}",
                Body = Google.Protobuf.ByteString.CopyFromUtf8($"body-{i}"),
                ClientID = "producer-1",
                Attributes = new KubeMQ.Grpc.QueueMessageAttributes
                {
                    Sequence = (uint)(i + 1),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                },
            });
        }

        return response;
    }

    private static (QueueDownstreamReceiver Receiver, DownstreamStreamHandle Handle, ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> Captured)
        CreateReceiver(params KubeMQ.Grpc.QueuesDownstreamResponse[] responses)
    {
        var (call, captured) = MockDownstreamStream.Create(responses);
        var handle = new DownstreamStreamHandle(call, "test-client", NullLogger.Instance);
        var receiver = new QueueDownstreamReceiver(
            handle, "test-client", "localhost", 50000, NullLogger.Instance);
        return (receiver, handle, captured);
    }

    [Fact]
    public async Task PollAsync_ReturnsQueueBatch_WithMessages()
    {
        var response = CreateResponseWithMessages(messageCount: 3);
        var (receiver, handle, _) = CreateReceiver(response);
        await using (handle)
        await using (receiver)
        {
            var request = new QueuePollRequest
            {
                Channel = "test-channel",
                MaxMessages = 5,
                WaitTimeoutSeconds = 10,
                AutoAck = false,
            };

            var batch = await receiver.PollAsync(request);

            batch.Should().NotBeNull();
            batch.Messages.Should().HaveCount(3);
            batch.TransactionId.Should().Be("txn-001");
            batch.IsError.Should().BeFalse();
            batch.Messages[0].MessageId.Should().Be("msg-0");
            batch.Messages[1].MessageId.Should().Be("msg-1");
            batch.Messages[2].MessageId.Should().Be("msg-2");
        }
    }

    [Fact]
    public async Task PollAsync_AutoAck_BatchHasNoSettlement()
    {
        var response = CreateResponseWithMessages(transactionId: "");
        var (receiver, handle, _) = CreateReceiver(response);
        await using (handle)
        await using (receiver)
        {
            var request = new QueuePollRequest
            {
                Channel = "test-channel",
                MaxMessages = 1,
                WaitTimeoutSeconds = 10,
                AutoAck = true,
            };

            var batch = await receiver.PollAsync(request);

            batch.Should().NotBeNull();
            batch.Messages.Should().HaveCount(1);

            var act = () => batch.AckAllAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*AutoAck*");
        }
    }

    [Fact]
    public async Task PollAsync_ManualAck_BatchHasSettlement()
    {
        var response = CreateResponseWithMessages(transactionId: "txn-manual");
        var settlementResponse = new KubeMQ.Grpc.QueuesDownstreamResponse { IsError = false };
        var (receiver, handle, captured) = CreateReceiver(response, settlementResponse);
        await using (handle)
        await using (receiver)
        {
            var request = new QueuePollRequest
            {
                Channel = "test-channel",
                MaxMessages = 1,
                WaitTimeoutSeconds = 10,
                AutoAck = false,
            };

            var batch = await receiver.PollAsync(request);

            batch.Should().NotBeNull();
            batch.TransactionId.Should().Be("txn-manual");

            await batch.AckAllAsync();

            // The settlement write goes through Channel<T> asynchronously; give the writer loop time to process.
            await Task.Delay(200);

            captured.Should().HaveCount(2);
            var requests = captured.ToArray();
            requests[1].RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.AckAll);
            requests[1].RefTransactionId.Should().Be("txn-manual");
        }
    }

    [Fact]
    public async Task PollAsync_ValidationFailure_Throws()
    {
        var response = CreateResponseWithMessages();
        var (receiver, handle, _) = CreateReceiver(response);
        await using (handle)
        await using (receiver)
        {
            var request = new QueuePollRequest
            {
                Channel = "",
                MaxMessages = 1,
                WaitTimeoutSeconds = 10,
            };

            var act = () => receiver.PollAsync(request);

            await act.Should().ThrowAsync<Exception>();
        }
    }

    [Fact]
    public async Task PollAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var response = CreateResponseWithMessages();
        var (receiver, handle, _) = CreateReceiver(response);

        await receiver.DisposeAsync();
        await handle.DisposeAsync();

        var request = new QueuePollRequest
        {
            Channel = "test-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 10,
        };

        var act = () => receiver.PollAsync(request);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PollAsync_ServerError_ReturnsBatchWithIsError()
    {
        var response = CreateResponseWithMessages(
            isError: true, error: "queue not found");
        var (receiver, handle, _) = CreateReceiver(response);
        await using (handle)
        await using (receiver)
        {
            var request = new QueuePollRequest
            {
                Channel = "test-channel",
                MaxMessages = 1,
                WaitTimeoutSeconds = 10,
            };

            var batch = await receiver.PollAsync(request);

            batch.IsError.Should().BeTrue();
            batch.Error.Should().Be("queue not found");
        }
    }

    [Fact]
    public async Task OnError_EventFires_OnSettlementError()
    {
        var pollResponse = CreateResponseWithMessages(transactionId: "txn-err");
        var errorResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            IsError = true,
            Error = "settlement failed",
            RefRequestId = "some-req",
        };
        var (call, captured) = MockDownstreamStream.Create(new[] { pollResponse, errorResponse });
        var handle = new DownstreamStreamHandle(
            call,
            "test-client",
            NullLogger.Instance,
            onError: (txnId, err) =>
            {
                // The handle's onError callback is what the KubeMQClient wires up
                // to call receiver.RaiseOnError.
            });

        var receiver = new QueueDownstreamReceiver(
            handle, "test-client", "localhost", 50000, NullLogger.Instance);
        await using (handle)
        await using (receiver)
        {
            string? firedTxnId = null;
            string? firedError = null;
            receiver.OnError += (_, args) =>
            {
                firedTxnId = args.TransactionId;
                firedError = args.Error;
            };

            receiver.RaiseOnError("txn-err", "settlement failed");

            firedTxnId.Should().Be("txn-err");
            firedError.Should().Be("settlement failed");
        }
    }

    [Fact]
    public async Task DisposeAsync_SendsCloseAndDisposesHandle()
    {
        var response = CreateResponseWithMessages(transactionId: "txn-close");
        var closeResponse = new KubeMQ.Grpc.QueuesDownstreamResponse { IsError = false };
        var (call, captured) = MockDownstreamStream.Create(new[] { response, closeResponse });
        var handle = new DownstreamStreamHandle(call, "test-client", NullLogger.Instance);
        var receiver = new QueueDownstreamReceiver(
            handle, "test-client", "localhost", 50000, NullLogger.Instance);

        var request = new QueuePollRequest
        {
            Channel = "test-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 10,
            AutoAck = false,
        };

        await receiver.PollAsync(request);
        await receiver.DisposeAsync();

        handle.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var response = CreateResponseWithMessages();
        var (receiver, handle, _) = CreateReceiver(response);

        await receiver.DisposeAsync();
        await handle.DisposeAsync();

        // Second dispose should not throw
        var act = async () => await receiver.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
