using System.Text;
using System.Text.Json;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

/// <summary>
/// Tests covering previously uncovered lines in KubeMQClient.cs.
/// </summary>
public class KubeMQClientCoverageGapTests
{
    private static KubeMQ.Grpc.Response DefaultManagementResponse() =>
        new()
        {
            RequestID = "mgmt-001",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
        };

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // Lines 332-333: SendEventAsync when server returns Sent=false
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendEventAsync_ServerReturnsSentFalse_ThrowsKubeMQOperationException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { Sent = false, Error = "queue full" });

        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        Func<Task> act = () => client.SendEventAsync(message);

        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*Event send failed*queue full*");
    }

    [Fact]
    public async Task SendEventAsync_ServerReturnsSentFalse_NullError_ThrowsWithDefaultMessage()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { Sent = false });

        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        Func<Task> act = () => client.SendEventAsync(message);

        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*server returned Sent=false*");
    }

    // ---------------------------------------------------------------
    // Lines 648-654: SendQueueMessagesAsync batch with policies
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendQueueMessagesAsync_WithPolicies_SetsGrpcPolicy()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueueMessagesBatchRequest? capturedRequest = null;
        transport
            .Setup(t => t.SendQueueMessagesBatchAsync(
                It.IsAny<KubeMQ.Grpc.QueueMessagesBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.QueueMessagesBatchRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new KubeMQ.Grpc.QueueMessagesBatchResponse
            {
                BatchID = "batch-pol",
                HaveErrors = false,
            });

        var messages = new[]
        {
            new QueueMessage
            {
                Channel = "q1",
                Body = Encoding.UTF8.GetBytes("a"),
                DelaySeconds = 10,
                ExpirationSeconds = 300,
                MaxReceiveCount = 5,
                MaxReceiveQueue = "dlq",
            },
        };

        await client.SendQueueMessagesAsync(messages);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Messages.Should().HaveCount(1);
        var grpcMsg = capturedRequest.Messages[0];
        grpcMsg.Policy.Should().NotBeNull();
        grpcMsg.Policy.DelaySeconds.Should().Be(10);
        grpcMsg.Policy.ExpirationSeconds.Should().Be(300);
        grpcMsg.Policy.MaxReceiveCount.Should().Be(5);
        grpcMsg.Policy.MaxReceiveQueue.Should().Be("dlq");
    }

    // ---------------------------------------------------------------
    // Lines 824-865: ReceiveQueueMessagesAsync(string, int, int) — legacy pull API
    // ---------------------------------------------------------------
    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_ValidChannel_ReturnsMessages()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.ReceiveQueueMessagesResponse
        {
            RequestID = "req-legacy",
            MessagesReceived = 2,
            MessagesExpired = 0,
            IsPeak = false,
            IsError = false,
            Error = string.Empty,
        };
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            MessageID = "leg-msg-1",
            Channel = "legacy-ch",
            Body = ByteString.CopyFromUtf8("body1"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 1 },
        });
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            MessageID = "leg-msg-2",
            Channel = "legacy-ch",
            Body = ByteString.CopyFromUtf8("body2"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 2 },
        });

        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var result = await client.ReceiveQueueMessagesAsync("legacy-ch", maxMessages: 5, waitTimeSeconds: 2);

        result.Should().NotBeNull();
        result.RequestId.Should().Be("req-legacy");
        result.Messages.Should().HaveCount(2);
        result.MessagesReceived.Should().Be(2);
        result.IsPeak.Should().BeFalse();
        result.IsError.Should().BeFalse();
        result.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_NullChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.ReceiveQueueMessagesAsync(null!, 1, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_EmptyChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.ReceiveQueueMessagesAsync("", 1, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_SetsCorrectGrpcFields()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.ReceiveQueueMessagesRequest? captured = null;
        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.ReceiveQueueMessagesRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new KubeMQ.Grpc.ReceiveQueueMessagesResponse
            {
                RequestID = "req-1",
                IsError = false,
            });

        await client.ReceiveQueueMessagesAsync("my-queue", maxMessages: 10, waitTimeSeconds: 5);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("my-queue");
        captured.MaxNumberOfMessages.Should().Be(10);
        captured.WaitTimeSeconds.Should().Be(5);
        captured.IsPeak.Should().BeFalse();
        captured.ClientID.Should().Be("test-client");
    }

    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_WithError_ReturnsErrorInResult()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.ReceiveQueueMessagesResponse
            {
                RequestID = "req-err",
                IsError = true,
                Error = "queue not found",
            });

        var result = await client.ReceiveQueueMessagesAsync("missing-queue");

        result.IsError.Should().BeTrue();
        result.Error.Should().Be("queue not found");
    }

    // ---------------------------------------------------------------
    // Lines 872-956: SendQueueMessagesUpstreamAsync
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_NullMessages_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueueMessagesUpstreamAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_ValidMessages_ReturnsResult()
    {
        var (client, transport) = TestClientFactory.Create();

        var upstreamResponse = new KubeMQ.Grpc.QueuesUpstreamResponse
        {
            RefRequestID = "ref-up-1",
            IsError = false,
            Error = string.Empty,
        };
        upstreamResponse.Results.Add(new KubeMQ.Grpc.SendQueueMessageResult
        {
            MessageID = "up-msg-1",
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IsError = false,
            Error = string.Empty,
        });

        var mockCall = MockUpstreamStream.Create(upstreamResponse);
        transport
            .Setup(t => t.CreateUpstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var messages = new[]
        {
            new QueueMessage
            {
                Channel = "upstream-ch",
                Body = Encoding.UTF8.GetBytes("up-body"),
            },
        };

        var result = await client.SendQueueMessagesUpstreamAsync(messages);

        result.Should().NotBeNull();
        result.RefRequestId.Should().Be("ref-up-1");
        result.IsError.Should().BeFalse();
        result.Results.Should().HaveCount(1);
        result.Results[0].MessageId.Should().Be("up-msg-1");
    }

    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_WithPolicies_SetsGrpcPolicy()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.QueuesUpstreamRequest? captured = null;
        var upstreamResponse = new KubeMQ.Grpc.QueuesUpstreamResponse
        {
            RefRequestID = "ref-pol",
            IsError = false,
        };
        upstreamResponse.Results.Add(new KubeMQ.Grpc.SendQueueMessageResult
        {
            MessageID = "pol-msg-1",
            IsError = false,
        });

        var mockCall = MockUpstreamStream.CreateCapturing(upstreamResponse, req => captured = req);
        transport
            .Setup(t => t.CreateUpstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var messages = new[]
        {
            new QueueMessage
            {
                Channel = "policy-ch",
                Body = Encoding.UTF8.GetBytes("data"),
                DelaySeconds = 15,
                ExpirationSeconds = 120,
                MaxReceiveCount = 3,
                MaxReceiveQueue = "dlq-ch",
            },
        };

        var result = await client.SendQueueMessagesUpstreamAsync(messages);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.Messages.Should().HaveCount(1);
        var msg = captured.Messages[0];
        msg.Policy.Should().NotBeNull();
        msg.Policy.DelaySeconds.Should().Be(15);
        msg.Policy.ExpirationSeconds.Should().Be(120);
        msg.Policy.MaxReceiveCount.Should().Be(3);
        msg.Policy.MaxReceiveQueue.Should().Be("dlq-ch");
    }

    // ---------------------------------------------------------------
    // Lines 1030: SendCommandAsync response with tags
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendCommandAsync_ResponseWithTags_ReturnsTags()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.Response
        {
            RequestID = "req-cmd-tags",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
        };
        grpcResponse.Tags.Add("resp-key", "resp-val");

        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            Body = Encoding.UTF8.GetBytes("cmd"),
            TimeoutInSeconds = 10,
        };

        var response = await client.SendCommandAsync(message);

        response.Tags.Should().NotBeNull();
        response.Tags!["resp-key"].Should().Be("resp-val");
    }

    // ---------------------------------------------------------------
    // Lines 1245-1262: ListChannelsAsync retry on DeadlineExceeded & snapshot not ready
    // ---------------------------------------------------------------
    [Fact]
    public async Task ListChannelsAsync_SnapshotNotReady_RetriesAndSucceeds()
    {
        var (client, transport) = TestClientFactory.Create();

        int callCount = 0;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new KubeMQ.Grpc.Response
                    {
                        RequestID = "mgmt-retry",
                        Executed = true,
                        Error = "cluster snapshot not ready yet",
                    };
                }

                var json = JsonSerializer.Serialize(new[]
                {
                    new { name = "ch1", type = "events", lastActivity = 0L, isActive = true },
                });
                return new KubeMQ.Grpc.Response
                {
                    RequestID = "mgmt-ok",
                    Executed = true,
                    Error = string.Empty,
                    Body = ByteString.CopyFrom(Encoding.UTF8.GetBytes(json)),
                };
            });

        var channels = await client.ListChannelsAsync("events");

        channels.Should().HaveCount(1);
        channels[0].Name.Should().Be("ch1");
        callCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ListChannelsAsync_DeadlineExceeded_RetriesAndSucceeds()
    {
        var (client, transport) = TestClientFactory.Create();

        int callCount = 0;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Returns<KubeMQ.Grpc.Request, CancellationToken>((_, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));
                }

                var json = JsonSerializer.Serialize(new[]
                {
                    new { name = "retry-ch", type = "events", lastActivity = 0L, isActive = true },
                });
                return Task.FromResult(new KubeMQ.Grpc.Response
                {
                    RequestID = "mgmt-retry-ok",
                    Executed = true,
                    Error = string.Empty,
                    Body = ByteString.CopyFrom(Encoding.UTF8.GetBytes(json)),
                });
            });

        var channels = await client.ListChannelsAsync("events");

        channels.Should().HaveCount(1);
        channels[0].Name.Should().Be("retry-ch");
        callCount.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // Lines 1332-1388: Channel convenience methods
    // ---------------------------------------------------------------
    [Fact]
    public async Task CreateEventsChannelAsync_DelegatesToCreateChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateEventsChannelAsync("my-events");
        captured!.Metadata.Should().Be("create-channel");
        captured.Tags["channel_type"].Should().Be("events");
        captured.Tags["channel"].Should().Be("my-events");
    }

    [Fact]
    public async Task CreateEventsStoreChannelAsync_DelegatesToCreateChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateEventsStoreChannelAsync("my-store");
        captured!.Tags["channel_type"].Should().Be("events_store");
    }

    [Fact]
    public async Task CreateCommandsChannelAsync_DelegatesToCreateChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateCommandsChannelAsync("my-cmds");
        captured!.Tags["channel_type"].Should().Be("commands");
    }

    [Fact]
    public async Task CreateQueriesChannelAsync_DelegatesToCreateChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateQueriesChannelAsync("my-queries");
        captured!.Tags["channel_type"].Should().Be("queries");
    }

    [Fact]
    public async Task CreateQueuesChannelAsync_DelegatesToCreateChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateQueuesChannelAsync("my-queues");
        captured!.Tags["channel_type"].Should().Be("queues");
    }

    [Fact]
    public async Task DeleteEventsChannelAsync_DelegatesToDeleteChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteEventsChannelAsync("del-events");
        captured!.Metadata.Should().Be("delete-channel");
        captured.Tags["channel_type"].Should().Be("events");
    }

    [Fact]
    public async Task DeleteEventsStoreChannelAsync_DelegatesToDeleteChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteEventsStoreChannelAsync("del-store");
        captured!.Tags["channel_type"].Should().Be("events_store");
    }

    [Fact]
    public async Task DeleteCommandsChannelAsync_DelegatesToDeleteChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteCommandsChannelAsync("del-cmds");
        captured!.Tags["channel_type"].Should().Be("commands");
    }

    [Fact]
    public async Task DeleteQueriesChannelAsync_DelegatesToDeleteChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteQueriesChannelAsync("del-queries");
        captured!.Tags["channel_type"].Should().Be("queries");
    }

    [Fact]
    public async Task DeleteQueuesChannelAsync_DelegatesToDeleteChannelAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteQueuesChannelAsync("del-queues");
        captured!.Tags["channel_type"].Should().Be("queues");
    }

    [Fact]
    public async Task ListEventsChannelsAsync_DelegatesToListChannelsAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListEventsChannelsAsync();
        captured!.Tags["channel_type"].Should().Be("events");
    }

    [Fact]
    public async Task ListEventsStoreChannelsAsync_DelegatesToListChannelsAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListEventsStoreChannelsAsync();
        captured!.Tags["channel_type"].Should().Be("events_store");
    }

    [Fact]
    public async Task ListCommandsChannelsAsync_DelegatesToListChannelsAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListCommandsChannelsAsync();
        captured!.Tags["channel_type"].Should().Be("commands");
    }

    [Fact]
    public async Task ListQueriesChannelsAsync_DelegatesToListChannelsAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListQueriesChannelsAsync();
        captured!.Tags["channel_type"].Should().Be("queries");
    }

    [Fact]
    public async Task ListQueuesChannelsAsync_DelegatesToListChannelsAsync()
    {
        var (client, transport) = TestClientFactory.Create();
        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListQueuesChannelsAsync();
        captured!.Tags["channel_type"].Should().Be("queues");
    }

    // ---------------------------------------------------------------
    // Lines 1621-1623: SubscribeToEventsStoreAsync with StartAtTime
    // ---------------------------------------------------------------
    [Fact]
    public async Task SubscribeToEventsStoreAsync_StartAtTime_SetsCorrectGrpcFields()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.EventReceive>());

        var startTime = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var subscription = new EventStoreSubscription
        {
            Channel = "store-ch",
            StartPosition = EventStoreStartPosition.StartAtTime,
            StartTime = startTime,
        };

        await foreach (var _ in client.SubscribeToEventsStoreAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.EventsStoreTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtTime);
        capturedSub.EventsStoreTypeValue.Should().Be(startTime.ToUnixTimeSeconds());
    }

    // ---------------------------------------------------------------
    // Lines 500-501: SubscribeToEventsStoreAsync reconnect with lastSequence
    // ---------------------------------------------------------------
    [Fact]
    public async Task SubscribeToEventsStoreAsync_YieldsEventWithSequence_TracksLastSequence()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcEvent = new KubeMQ.Grpc.EventReceive
        {
            Channel = "store-ch",
            Body = ByteString.CopyFromUtf8("data"),
            Timestamp = 1700000000,
            Sequence = 42,
        };

        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(grpcEvent));

        var subscription = new EventStoreSubscription
        {
            Channel = "store-ch",
            StartPosition = EventStoreStartPosition.StartFromFirst,
        };

        var received = new List<EventStoreReceived>();
        await foreach (var evt in client.SubscribeToEventsStoreAsync(subscription))
        {
            received.Add(evt);
        }

        received.Should().HaveCount(1);
        received[0].Sequence.Should().Be(42);
    }

    // ---------------------------------------------------------------
    // Lines 1736: MapToQueueMessageReceived with tags
    // ---------------------------------------------------------------
    [Fact]
    public async Task ReceiveQueueMessagesAsync_LegacyPull_WithTags_MapsTags()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.ReceiveQueueMessagesResponse
        {
            RequestID = "req-tags",
            IsError = false,
        };
        var msg = new KubeMQ.Grpc.QueueMessage
        {
            MessageID = "tag-msg",
            Channel = "tag-ch",
            Body = ByteString.CopyFromUtf8("tagged"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 5 },
        };
        msg.Tags.Add("env", "prod");
        msg.Tags.Add("region", "us-east");
        grpcResponse.Messages.Add(msg);

        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var result = await client.ReceiveQueueMessagesAsync("tag-ch");

        result.Messages.Should().HaveCount(1);
        result.Messages[0].Tags.Should().NotBeNull();
        result.Messages[0].Tags!["env"].Should().Be("prod");
        result.Messages[0].Tags!["region"].Should().Be("us-east");
    }

    // ---------------------------------------------------------------
    // Lines 803-807: ReceiveQueueMessagesAsync poll with error response
    // (the QueuePollResponse path with isError)
    // ---------------------------------------------------------------
    [Fact]
    public async Task ReceiveQueueMessagesAsync_PollApi_ErrorResponse_ReturnsQueuePollResponseWithError()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            TransactionId = "txn-err",
            RefRequestId = "ref-err",
            IsError = true,
            Error = "channel not found",
        };

        var (mockCall, _) = MockDownstreamStream.Create(grpcResponse);
        transport
            .Setup(t => t.CreateDownstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var request = new QueuePollRequest
        {
            Channel = "err-channel",
            MaxMessages = 1,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.ReceiveQueueMessagesAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("channel not found");
    }

    // ---------------------------------------------------------------
    // Lines 241, 244: ConnectAsync — ping failure during best-effort compatibility check
    // ---------------------------------------------------------------
    [Fact]
    public async Task ConnectAsync_PingFailsDuringCompatibilityCheck_DoesNotFailConnect()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "server down")));

        await client.ConnectAsync();

        // Connection should succeed despite ping failure
        client.State.Should().Be(ConnectionState.Ready);
    }

    // ---------------------------------------------------------------
    // Lines 2106, 2108-2109: StateChanged handler throws exception
    // ---------------------------------------------------------------
    [Fact]
    public async Task StateChanged_HandlerThrows_DoesNotCrashClient()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });

        client.StateChanged += (_, _) => throw new InvalidOperationException("handler error");

        // Should not throw despite handler exception
        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        // Wait for background handler to execute
        await Task.Delay(200);
    }

    // ---------------------------------------------------------------
    // Lines 1766, 1772-1773: NormalizeVersion with pre-release and partial versions
    // (tested indirectly via ConnectAsync+compatibility check)
    // ---------------------------------------------------------------
    [Fact]
    public async Task ConnectAsync_ServerVersionWithPreRelease_DoesNotFail()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "v3.5.0-beta.1" });

        await client.ConnectAsync();
        client.State.Should().Be(ConnectionState.Ready);

        // Give the background task time to run
        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionWithSinglePart_DoesNotFail()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3" });

        await client.ConnectAsync();
        client.State.Should().Be(ConnectionState.Ready);
        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionWithTwoParts_DoesNotFail()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5" });

        await client.ConnectAsync();
        client.State.Should().Be(ConnectionState.Ready);
        await Task.Delay(200);
    }

    // ---------------------------------------------------------------
    // Lines 1837: NanosToDateTimeOffset max value capping
    // (tested indirectly through queue send result)
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendQueueMessageAsync_VeryLargeNanoTimestamp_ConvertsToDate()
    {
        var (client, transport) = TestClientFactory.Create();

        // long.MaxValue / 100 = 92233720368547758 ticks, which is within DateTimeOffset range
        // This exercises the NanosToDateTimeOffset conversion for large values
        transport
            .Setup(t => t.SendQueueMessageAsync(
                It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.SendQueueMessageResult
            {
                MessageID = "big-ts",
                SentAt = long.MaxValue,
                IsError = false,
            });

        var message = new QueueMessage
        {
            Channel = "ts-ch",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        var result = await client.SendQueueMessageAsync(message);

        // Should convert without error — the resulting date is in the future but valid
        result.SentAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    // ---------------------------------------------------------------
    // Lines 1866-1877: SafeFromUnixTimeSecondsNullable
    // (tested via queue message with zero/large expiration timestamps)
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendQueueMessageAsync_ZeroDelayedTo_ReturnsNullDelayedTo()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessageAsync(
                It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.SendQueueMessageResult
            {
                MessageID = "zero-delay",
                SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsError = false,
                DelayedTo = 0,
                ExpirationAt = 0,
            });

        var message = new QueueMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        var result = await client.SendQueueMessageAsync(message);

        result.DelayedTo.Should().BeNull();
        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task SendQueueMessageAsync_PositiveDelayedTo_ReturnsValue()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueueMessageAsync(
                It.IsAny<KubeMQ.Grpc.QueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.SendQueueMessageResult
            {
                MessageID = "has-delay",
                SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsError = false,
                DelayedTo = 100,
                ExpirationAt = 200,
            });

        var message = new QueueMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        var result = await client.SendQueueMessageAsync(message);

        result.DelayedTo.Should().Be(100);
        result.ExpiresAt.Should().Be(200);
    }

    // ---------------------------------------------------------------
    // Lines 1814: TryParseChannelStats returns null for missing property
    // ---------------------------------------------------------------
    [Fact]
    public async Task ListChannelsAsync_ChannelWithoutStats_ReturnsNullStats()
    {
        var (client, transport) = TestClientFactory.Create();

        var channelData = new[]
        {
            new { name = "bare-ch", type = "events", lastActivity = 0L, isActive = false },
        };
        var json = JsonSerializer.Serialize(channelData);

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Response
            {
                RequestID = "mgmt-null-stats",
                Executed = true,
                Error = string.Empty,
                Body = ByteString.CopyFrom(Encoding.UTF8.GetBytes(json)),
            });

        var channels = await client.ListChannelsAsync("events");

        channels.Should().HaveCount(1);
        channels[0].Incoming.Should().BeNull();
        channels[0].Outgoing.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Lines 208: ConnectAsync with null transport/stateMachine
    // ---------------------------------------------------------------
    [Fact]
    public async Task ConnectAsync_UninitializedClient_ThrowsInvalidOperation()
    {
        var client = new TestableKubeMQClient();

        Func<Task> act = () => client.ConnectAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not properly initialized*");
    }

    // ---------------------------------------------------------------
    // Lines 278: PingAsync with null transport
    // ---------------------------------------------------------------
    [Fact]
    public async Task PingAsync_UninitializedClient_ThrowsInvalidOperation()
    {
        var client = new TestableKubeMQClient();

        Func<Task> act = () => client.PingAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not properly initialized*");
    }

    // ---------------------------------------------------------------
    // Lines 1554-1557: DisposeAsync when Reconnecting state — discards buffer
    // (We can't easily trigger Reconnecting state without connection manager,
    // but we can at least verify the Dispose path works)
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Lines 2078: WaitForReadyIfNeeded with null transport
    // ---------------------------------------------------------------
    [Fact]
    public async Task SendEventAsync_UninitializedClient_ThrowsInvalidOperation()
    {
        var client = new TestableKubeMQClient();

        Func<Task> act = () => client.SendEventAsync(
            new EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not properly initialized*");
    }

    // ---------------------------------------------------------------
    // Lines 1959-1961: SubscribeToEventsAsync cancellation during stream creation
    // ---------------------------------------------------------------
    [Fact]
    public async Task SubscribeToEventsAsync_CancelledDuringStreamCreation_YieldsNothing()
    {
        var (client, transport) = TestClientFactory.Create();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        var subscription = new EventsSubscription { Channel = "cancel-ch" };
        var received = new List<EventReceived>();

        await foreach (var evt in client.SubscribeToEventsAsync(subscription, cts.Token))
        {
            received.Add(evt);
        }

        received.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Lines 1984-1986: SubscribeToCommandsAsync cancellation during MoveNext
    // ---------------------------------------------------------------
    [Fact]
    public async Task SubscribeToCommandsAsync_CancelledDuringIteration_StopsGracefully()
    {
        var (client, transport) = TestClientFactory.Create();

        using var cts = new CancellationTokenSource();

        transport
            .Setup(t => t.SubscribeToCommandsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(CancellingAsyncEnumerable<KubeMQ.Grpc.Request>(cts));

        var subscription = new CommandsSubscription { Channel = "cmd-cancel-ch" };
        var received = new List<CommandReceived>();

        await foreach (var cmd in client.SubscribeToCommandsAsync(subscription, cts.Token))
        {
            received.Add(cmd);
        }

        received.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Lines 2135-2138: DrainAsync with timeout
    // (tested via DisposeAsync — the drain path runs if state is Ready)
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Lines 2148-2164: DrainCallbacksAsync with active callbacks
    // (tested via DisposeAsync — the drain path runs if callbacks are active)
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Lines 1631-1632: EncodeEventStoreSubscription default case
    // (dead code path — can't be triggered with valid enum values)
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Lines 2171, 2176: CheckServerCompatibility with empty/unparseable version
    // ---------------------------------------------------------------
    [Fact]
    public async Task ConnectAsync_ServerVersionEmpty_DoesNotFail()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = string.Empty });

        await client.ConnectAsync();
        client.State.Should().Be(ConnectionState.Ready);
        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionUnparseable_DoesNotFail()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "not-a-version" });

        await client.ConnectAsync();
        client.State.Should().Be(ConnectionState.Ready);
        await Task.Delay(200);
    }

    // ---------------------------------------------------------------
    // Helper: creates an async enumerable that cancels via CTS on first MoveNext
    // ---------------------------------------------------------------
    private static async IAsyncEnumerable<T> CancellingAsyncEnumerable<T>(
        CancellationTokenSource cts)
    {
        await cts.CancelAsync();
        throw new OperationCanceledException(cts.Token);
#pragma warning disable CS0162 // Unreachable code
        yield break;
#pragma warning restore CS0162
    }

    // ---------------------------------------------------------------
    // Disposable client for null transport/stateMachine tests
    // ---------------------------------------------------------------
    private class TestableKubeMQClient : KubeMQClient
    {
        public TestableKubeMQClient() : base() { }
    }
}
