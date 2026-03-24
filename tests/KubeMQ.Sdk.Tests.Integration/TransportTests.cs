using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

/// <summary>
/// Integration tests that exercise GrpcTransport-level paths:
/// multi-channel connections, error handling, close/reconnect, and round-robin.
/// </summary>
public class TransportTests : IntegrationTestBase
{
    [Fact]
    public async Task MultiChannelConnect_WithGrpcChannelCount3_PingSucceeds()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "multi-ch-test",
            GrpcChannelCount = 3,
        };
        await using var client = new KubeMQClient(options);
        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        // Ping multiple times to exercise round-robin across channels
        for (var i = 0; i < 10; i++)
        {
            var info = await client.PingAsync();
            info.Should().NotBeNull();
            info.Host.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task MultiChannelConnect_SendAndReceiveEvents_Works()
    {
        var pubOptions = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "multi-ch-pub",
            GrpcChannelCount = 3,
        };
        var subOptions = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "multi-ch-sub",
            GrpcChannelCount = 2,
        };

        await using var publisher = new KubeMQClient(pubOptions);
        await publisher.ConnectAsync();

        await using var subscriber = new KubeMQClient(subOptions);
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("multi-ch-evt");
        var tcs = new TaskCompletionSource<EventReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = channel };
            await foreach (var evt in subscriber.SubscribeToEventsAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await publisher.SendEventAsync(new EventMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("multi-channel-event"),
        });

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Channel.Should().Be(channel);
    }

    [Fact]
    public async Task ConnectAsync_InvalidAddress_ThrowsException()
    {
        var options = new KubeMQClientOptions
        {
            Address = "invalid-host-that-does-not-exist:99999",
            ClientId = "bad-addr-test",
            ConnectionTimeout = TimeSpan.FromSeconds(3),
        };
        var client = new KubeMQClient(options);

        Func<Task> act = () => client.ConnectAsync();

        // Should throw either KubeMQConnectionException or KubeMQTimeoutException
        await act.Should().ThrowAsync<KubeMQException>();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_ShortTimeout_ThrowsTimeoutException()
    {
        // Use a non-routable address so it hangs until timeout
        var options = new KubeMQClientOptions
        {
            Address = "10.255.255.1:50000",
            ClientId = "timeout-test",
            ConnectionTimeout = TimeSpan.FromMilliseconds(500),
        };
        var client = new KubeMQClient(options);

        Func<Task> act = () => client.ConnectAsync();

        // Should throw KubeMQTimeoutException or KubeMQConnectionException
        await act.Should().ThrowAsync<KubeMQException>();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ThenCreateNew_Works()
    {
        // First client: connect, ping, dispose
        var client1 = CreateClient("close-reconnect-1");
        await client1.ConnectAsync();
        var info1 = await client1.PingAsync();
        info1.Should().NotBeNull();

        await client1.DisposeAsync();
        client1.State.Should().Be(ConnectionState.Closed);

        // Second client: connect, ping — verifies we can create a fresh client
        await using var client2 = CreateClient("close-reconnect-2");
        await client2.ConnectAsync();
        var info2 = await client2.PingAsync();
        info2.Should().NotBeNull();
        info2.Host.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConnectAsync_CalledTwice_ThrowsInvalidOperation()
    {
        await using var client = CreateClient("double-connect");
        await client.ConnectAsync();

        // Second connect should throw because state is Ready, not Idle
        Func<Task> act = () => client.ConnectAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PingAsync_MultipleConcurrent_AllSucceed()
    {
        await using var client = CreateClient("concurrent-ping");
        await client.ConnectAsync();

        // Fire 20 concurrent pings
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.PingAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(20);
        results.Should().AllSatisfy(info =>
        {
            info.Should().NotBeNull();
            info.Host.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task GrpcChannelCount1_WorksCorrectly()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "single-ch-test",
            GrpcChannelCount = 1,
        };
        await using var client = new KubeMQClient(options);
        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        var info = await client.PingAsync();
        info.Should().NotBeNull();
        info.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GrpcChannelCountMax_16Channels_ConnectsSuccessfully()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "max-ch-test",
            GrpcChannelCount = 16,
        };
        await using var client = new KubeMQClient(options);
        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        var info = await client.PingAsync();
        info.Should().NotBeNull();
    }

    [Fact]
    public void GrpcChannelCount_OutOfRange_ThrowsConfigException()
    {
        // GrpcChannelCount must be 1..16
        var act = () => new KubeMQClient(new KubeMQClientOptions
        {
            Address = "localhost:50000",
            GrpcChannelCount = 0,
        });

        act.Should().Throw<KubeMQConfigurationException>();

        var act2 = () => new KubeMQClient(new KubeMQClientOptions
        {
            Address = "localhost:50000",
            GrpcChannelCount = 17,
        });

        act2.Should().Throw<KubeMQConfigurationException>();
    }

    [Fact]
    public async Task StateChanged_FiredOnConnectAndDispose()
    {
        var client = CreateClient("state-events");
        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        client.StateChanged += (_, args) => stateChanges.Add(args);

        await client.ConnectAsync();
        stateChanges.Should().Contain(e => e.CurrentState == ConnectionState.Ready);

        await client.DisposeAsync();
        stateChanges.Should().Contain(e => e.CurrentState == ConnectionState.Closed);
    }

    [Fact]
    public async Task MultiChannel_EventsSendViaRoundRobin_AllDelivered()
    {
        var pubOptions = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "rr-pub",
            GrpcChannelCount = 4,
        };

        await using var publisher = new KubeMQClient(pubOptions);
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("rr-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("rr-events");
        var messageCount = 20;
        var received = new List<EventReceived>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = channel };
            await foreach (var evt in subscriber.SubscribeToEventsAsync(subscription, cts.Token))
            {
                received.Add(evt);
                if (received.Count >= messageCount)
                {
                    tcs.TrySetResult(true);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(1000);

        // Send 20 events — these will round-robin across 4 gRPC channels
        for (var i = 0; i < messageCount; i++)
        {
            await publisher.SendEventAsync(new EventMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes($"rr-event-{i}"),
            });
        }

        await tcs.Task;

        received.Should().HaveCount(messageCount);
    }

    [Fact]
    public async Task WaitForReady_False_FailsImmediatelyWhenNotConnected()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "no-wait-test",
            WaitForReady = false,
        };
        await using var client = new KubeMQClient(options);

        // Do NOT connect — attempt to send an event immediately
        // This should throw because WaitForReady=false and not connected
        Func<Task> act = () => client.SendEventAsync(new EventMessage
        {
            Channel = "some-channel",
            Body = Encoding.UTF8.GetBytes("should-fail"),
        });

        // Should throw because client is not connected
        await act.Should().ThrowAsync<Exception>();
    }
}
