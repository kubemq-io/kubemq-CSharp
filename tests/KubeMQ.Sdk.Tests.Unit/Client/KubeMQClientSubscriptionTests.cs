using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using Google.Protobuf;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientSubscriptionTests
{
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static KubeMQ.Grpc.EventReceive MakeGrpcEventReceive(
        string channel = "evt-ch",
        string body = "payload",
        long timestamp = 1700000000,
        ulong sequence = 0)
    {
        var e = new KubeMQ.Grpc.EventReceive
        {
            Channel = channel,
            Body = ByteString.CopyFromUtf8(body),
            Timestamp = timestamp,
            Sequence = sequence,
            Metadata = "sender-client",
        };
        e.Tags.Add("k1", "v1");
        return e;
    }

    private static KubeMQ.Grpc.Request MakeGrpcRequest(
        string channel = "cmd-ch",
        string body = "cmd-body",
        string requestId = "req-001",
        string replyChannel = "reply-ch")
    {
        var r = new KubeMQ.Grpc.Request
        {
            Channel = channel,
            Body = ByteString.CopyFromUtf8(body),
            RequestID = requestId,
            ReplyChannel = replyChannel,
        };
        r.Tags.Add("tk", "tv");
        return r;
    }

    // --- SubscribeToEventsAsync ---

    [Fact]
    public async Task SubscribeToEventsAsync_ValidSubscription_YieldsEvents()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcEvent1 = MakeGrpcEventReceive("events-ch", "body1");
        var grpcEvent2 = MakeGrpcEventReceive("events-ch", "body2");

        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(grpcEvent1, grpcEvent2));

        var subscription = new EventsSubscription { Channel = "events-ch" };

        var received = new List<EventReceived>();
        await foreach (var evt in client.SubscribeToEventsAsync(subscription))
        {
            received.Add(evt);
        }

        received.Should().HaveCount(2);
        received[0].Channel.Should().Be("events-ch");
        Encoding.UTF8.GetString(received[0].Body.Span).Should().Be("body1");
        received[0].Tags.Should().ContainKey("k1").WhoseValue.Should().Be("v1");
        received[0].ClientId.Should().BeNull();
        received[1].Channel.Should().Be("events-ch");
        Encoding.UTF8.GetString(received[1].Body.Span).Should().Be("body2");
    }

    [Fact]
    public async Task SubscribeToEventsAsync_NullSubscription_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = async () =>
        {
            await foreach (var _ in client.SubscribeToEventsAsync(null!))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SubscribeToEventsAsync_DisposedClient_ThrowsObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();
        await client.DisposeAsync();

        Func<Task> act = async () =>
        {
            await foreach (var _ in client.SubscribeToEventsAsync(
                new EventsSubscription { Channel = "ch" }))
            {
            }
        };

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SubscribeToEventsAsync_WithGroup_PassesGroupToGrpc()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.EventReceive>());

        var subscription = new EventsSubscription { Channel = "ch", Group = "grp1" };

        await foreach (var _ in client.SubscribeToEventsAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.Group.Should().Be("grp1");
        capturedSub.SubscribeTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.SubscribeType.Events);
    }

    // --- SubscribeToEventsStoreAsync ---

    [Fact]
    public async Task SubscribeToEventsStoreAsync_ValidSubscription_YieldsEvents()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcEvent = MakeGrpcEventReceive("store-ch", "stored", sequence: 42);

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
        received[0].Channel.Should().Be("store-ch");
        received[0].Sequence.Should().Be(42);
        Encoding.UTF8.GetString(received[0].Body.Span).Should().Be("stored");
    }

    [Theory]
    [InlineData(EventStoreStartPosition.StartFromNew, KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartNewOnly)]
    [InlineData(EventStoreStartPosition.StartFromFirst, KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartFromFirst)]
    [InlineData(EventStoreStartPosition.StartFromLast, KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartFromLast)]
    public async Task SubscribeToEventsStoreAsync_StartPosition_MapsCorrectly(
        EventStoreStartPosition position,
        KubeMQ.Grpc.Subscribe.Types.EventsStoreType expectedGrpc)
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.EventReceive>());

        var subscription = new EventStoreSubscription
        {
            Channel = "store-ch",
            StartPosition = position,
        };

        await foreach (var _ in client.SubscribeToEventsStoreAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.EventsStoreTypeData.Should().Be(expectedGrpc);
        capturedSub.SubscribeTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.SubscribeType.EventsStore);
    }

    [Fact]
    public async Task SubscribeToEventsStoreAsync_FromSequence_SetsSequenceValue()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.EventReceive>());

        var subscription = new EventStoreSubscription
        {
            Channel = "store-ch",
            StartPosition = EventStoreStartPosition.StartAtSequence,
            StartSequence = 100,
        };

        await foreach (var _ in client.SubscribeToEventsStoreAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.EventsStoreTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtSequence);
        capturedSub.EventsStoreTypeValue.Should().Be(100);
    }

    [Fact]
    public async Task SubscribeToEventsStoreAsync_FromTimeDelta_SetsDeltaValue()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.EventReceive>());

        var subscription = new EventStoreSubscription
        {
            Channel = "store-ch",
            StartPosition = EventStoreStartPosition.StartAtTimeDelta,
            StartTimeDeltaSeconds = 3600,
        };

        await foreach (var _ in client.SubscribeToEventsStoreAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.EventsStoreTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtTimeDelta);
        capturedSub.EventsStoreTypeValue.Should().Be(3600);
    }

    // --- SubscribeToCommandsAsync ---

    [Fact]
    public async Task SubscribeToCommandsAsync_ValidSubscription_YieldsCommands()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcReq = MakeGrpcRequest("cmd-ch", "do-something", "req-42", "reply-42");

        transport
            .Setup(t => t.SubscribeToCommandsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(grpcReq));

        var subscription = new CommandsSubscription { Channel = "cmd-ch" };

        var received = new List<CommandReceived>();
        await foreach (var cmd in client.SubscribeToCommandsAsync(subscription))
        {
            received.Add(cmd);
        }

        received.Should().HaveCount(1);
        received[0].Channel.Should().Be("cmd-ch");
        received[0].RequestId.Should().Be("req-42");
        received[0].ReplyChannel.Should().Be("reply-42");
        Encoding.UTF8.GetString(received[0].Body.Span).Should().Be("do-something");
        received[0].Tags.Should().ContainKey("tk").WhoseValue.Should().Be("tv");
    }

    [Fact]
    public async Task SubscribeToCommandsAsync_NullSubscription_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = async () =>
        {
            await foreach (var _ in client.SubscribeToCommandsAsync(null!))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SubscribeToCommandsAsync_SetsCorrectSubscribeType()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToCommandsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.Request>());

        var subscription = new CommandsSubscription { Channel = "cmd-ch", Group = "g" };

        await foreach (var _ in client.SubscribeToCommandsAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.SubscribeTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.SubscribeType.Commands);
        capturedSub.Channel.Should().Be("cmd-ch");
        capturedSub.Group.Should().Be("g");
    }

    // --- SubscribeToQueriesAsync ---

    [Fact]
    public async Task SubscribeToQueriesAsync_ValidSubscription_YieldsQueries()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcReq = MakeGrpcRequest("query-ch", "lookup", "req-99", "reply-99");
        grpcReq.CacheKey = "ck";

        transport
            .Setup(t => t.SubscribeToQueriesAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(grpcReq));

        var subscription = new QueriesSubscription { Channel = "query-ch" };

        var received = new List<QueryReceived>();
        await foreach (var q in client.SubscribeToQueriesAsync(subscription))
        {
            received.Add(q);
        }

        received.Should().HaveCount(1);
        received[0].Channel.Should().Be("query-ch");
        received[0].RequestId.Should().Be("req-99");
        received[0].ReplyChannel.Should().Be("reply-99");
        received[0].CacheKey.Should().Be("ck");
        Encoding.UTF8.GetString(received[0].Body.Span).Should().Be("lookup");
        received[0].Tags.Should().ContainKey("tk").WhoseValue.Should().Be("tv");
    }

    [Fact]
    public async Task SubscribeToQueriesAsync_NullSubscription_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = async () =>
        {
            await foreach (var _ in client.SubscribeToQueriesAsync(null!))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SubscribeToQueriesAsync_SetsCorrectSubscribeType()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Subscribe? capturedSub = null;
        transport
            .Setup(t => t.SubscribeToQueriesAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Subscribe, CancellationToken>((s, _) => capturedSub = s)
            .Returns(ToAsyncEnumerable<KubeMQ.Grpc.Request>());

        var subscription = new QueriesSubscription { Channel = "q-ch" };

        await foreach (var _ in client.SubscribeToQueriesAsync(subscription))
        {
        }

        capturedSub.Should().NotBeNull();
        capturedSub!.SubscribeTypeData.Should().Be(
            KubeMQ.Grpc.Subscribe.Types.SubscribeType.Queries);
        capturedSub.Channel.Should().Be("q-ch");
    }
}
