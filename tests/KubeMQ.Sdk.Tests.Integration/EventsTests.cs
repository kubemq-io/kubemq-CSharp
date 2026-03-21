using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class EventsTests : IntegrationTestBase
{
    [Fact]
    public async Task PublishEvent_DoesNotThrow()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("publish-event");
        var message = new EventMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("hello-event"),
        };

        var act = () => client.SendEventAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeAndPublish_ReceivesEvent()
    {
        await using var publisher = CreateClient("events-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("events-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("sub-events");
        var payload = Encoding.UTF8.GetBytes("sub-event-body");
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
            Body = payload,
        });

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Channel.Should().Be(channel);
        received.Body.ToArray().Should().BeEquivalentTo(payload);
    }

    [Fact]
    public async Task SubscribeAndPublish_PreservesTags()
    {
        await using var publisher = CreateClient("events-tag-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("events-tag-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("sub-events-tags");
        var tags = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" };
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
            Body = Encoding.UTF8.GetBytes("tagged-event"),
            Tags = tags,
        });

        var received = await tcs.Task;

        received.Tags.Should().NotBeNull();
        received.Tags.Should().ContainKey("key1").WhoseValue.Should().Be("value1");
        received.Tags.Should().ContainKey("key2").WhoseValue.Should().Be("value2");
    }

    [Fact]
    public async Task SubscribeToEvents_WildcardChannel()
    {
        await using var publisher = CreateClient("events-wild-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("events-wild-sub");
        await subscriber.ConnectAsync();

        var guid = Guid.NewGuid().ToString("N");
        var subscribeChannel = $"test-wild-{guid}.*";
        var publishChannel = $"test-wild-{guid}.sub";
        var payload = Encoding.UTF8.GetBytes("wildcard-event");
        var tcs = new TaskCompletionSource<EventReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = subscribeChannel };
            await foreach (var evt in subscriber.SubscribeToEventsAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await publisher.SendEventAsync(new EventMessage
        {
            Channel = publishChannel,
            Body = payload,
        });

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Channel.Should().Be(publishChannel);
        received.Body.ToArray().Should().BeEquivalentTo(payload);
    }

    [Fact]
    public async Task SubscribeToEvents_GroupSubscription()
    {
        await using var publisher = CreateClient("events-grp-pub");
        await publisher.ConnectAsync();

        await using var sub1 = CreateClient("events-grp-sub1");
        await sub1.ConnectAsync();

        await using var sub2 = CreateClient("events-grp-sub2");
        await sub2.ConnectAsync();

        var channel = UniqueChannel("sub-events-group");
        var group = "test-group";
        var receivedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = channel, Group = group };
            await foreach (var evt in sub1.SubscribeToEventsAsync(subscription, cts.Token))
            {
                Interlocked.Increment(ref receivedCount);
                break;
            }
        }, cts.Token);

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = channel, Group = group };
            await foreach (var evt in sub2.SubscribeToEventsAsync(subscription, cts.Token))
            {
                Interlocked.Increment(ref receivedCount);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await publisher.SendEventAsync(new EventMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("group-event"),
        });

        await Task.Delay(2000);

        receivedCount.Should().Be(1, "only one subscriber in the group should receive the event");
    }

    [Fact]
    public async Task PublishEvent_MultipleMessages_AllReceived()
    {
        await using var publisher = CreateClient("events-multi-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("events-multi-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("sub-events-multi");
        var messageCount = 3;
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

        for (var i = 0; i < messageCount; i++)
        {
            await publisher.SendEventAsync(new EventMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes($"multi-event-{i}"),
            });
        }

        await tcs.Task;

        received.Should().HaveCount(messageCount);
    }
}
