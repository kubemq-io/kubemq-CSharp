using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class EventStoreTests : IntegrationTestBase
{
    [Fact]
    public async Task PublishEventStore_ReturnsSentTrue()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("publish-es");
        var message = new EventStoreMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("hello-event-store"),
        };

        var act = () => client.PublishEventStoreAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeFromNew_ReceivesNewEvents()
    {
        await using var publisher = CreateClient("es-new-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("es-new-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("es-from-new");
        var payload = Encoding.UTF8.GetBytes("new-event-body");
        var tcs = new TaskCompletionSource<EventStoreReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromNew,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await publisher.PublishEventStoreAsync(new EventStoreMessage
        {
            Channel = channel,
            Body = payload,
        });

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Channel.Should().Be(channel);
        received.Body.ToArray().Should().BeEquivalentTo(payload);
        received.Sequence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SubscribeFromFirst_ReceivesAll()
    {
        await using var publisher = CreateClient("es-first-pub");
        await publisher.ConnectAsync();

        var channel = UniqueChannel("es-from-first");

        // Publish 3 events first
        for (var i = 0; i < 3; i++)
        {
            await publisher.PublishEventStoreAsync(new EventStoreMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes($"event-{i}"),
            });
        }

        await Task.Delay(500);

        await using var subscriber = CreateClient("es-first-sub");
        await subscriber.ConnectAsync();

        var received = new List<EventStoreReceived>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromFirst,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 3)
                {
                    tcs.TrySetResult(true);
                    break;
                }
            }
        }, cts.Token);

        await tcs.Task;

        received.Should().HaveCount(3);
        received[0].Body.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("event-0"));
        received[1].Body.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("event-1"));
        received[2].Body.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("event-2"));
    }

    [Fact]
    public async Task SubscribeFromLast_ReceivesOnlyLast()
    {
        await using var publisher = CreateClient("es-last-pub");
        await publisher.ConnectAsync();

        var channel = UniqueChannel("es-from-last");

        // Publish 3 events first
        for (var i = 0; i < 3; i++)
        {
            await publisher.PublishEventStoreAsync(new EventStoreMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes($"event-{i}"),
            });
        }

        await Task.Delay(500);

        await using var subscriber = CreateClient("es-last-sub");
        await subscriber.ConnectAsync();

        var tcs = new TaskCompletionSource<EventStoreReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromLast,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Body.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("event-2"));
    }

    [Fact]
    public async Task SubscribeFromSequence_ReceivesFromGivenSequence()
    {
        await using var publisher = CreateClient("es-seq-pub");
        await publisher.ConnectAsync();

        var channel = UniqueChannel("es-from-seq");

        // Publish 5 events
        for (var i = 0; i < 5; i++)
        {
            await publisher.PublishEventStoreAsync(new EventStoreMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes($"seq-event-{i}"),
            });
        }

        await Task.Delay(500);

        await using var subscriber = CreateClient("es-seq-sub");
        await subscriber.ConnectAsync();

        var received = new List<EventStoreReceived>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromSequence,
                StartSequence = 3,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 3)
                {
                    tcs.TrySetResult(true);
                    break;
                }
            }
        }, cts.Token);

        await tcs.Task;

        received.Should().HaveCountGreaterOrEqualTo(3);
        received[0].Sequence.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task SubscribeFromTimeDelta_ReceivesRecent()
    {
        await using var publisher = CreateClient("es-delta-pub");
        await publisher.ConnectAsync();

        var channel = UniqueChannel("es-from-delta");

        // Publish an event
        await publisher.PublishEventStoreAsync(new EventStoreMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("recent-event"),
        });

        await Task.Delay(500);

        await using var subscriber = CreateClient("es-delta-sub");
        await subscriber.ConnectAsync();

        var tcs = new TaskCompletionSource<EventStoreReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromTimeDelta,
                StartTimeDeltaSeconds = 60,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        var received = await tcs.Task;

        received.Should().NotBeNull();
        received.Body.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("recent-event"));
    }

    [Fact]
    public async Task SubscribeAndPublish_PreservesTags()
    {
        await using var publisher = CreateClient("es-tag-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("es-tag-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("es-tags");
        var tags = new Dictionary<string, string> { ["env"] = "test", ["version"] = "1.0" };
        var tcs = new TaskCompletionSource<EventStoreReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.FromNew,
            };
            await foreach (var evt in subscriber.SubscribeToEventStoreAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await publisher.PublishEventStoreAsync(new EventStoreMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("tagged-es-event"),
            Tags = tags,
        });

        var received = await tcs.Task;

        received.Tags.Should().NotBeNull();
        received.Tags.Should().ContainKey("env").WhoseValue.Should().Be("test");
        received.Tags.Should().ContainKey("version").WhoseValue.Should().Be("1.0");
    }

    [Fact]
    public async Task CreateEventStoreStream_ConfirmsEachEvent()
    {
        await using var client = CreateClient("es-stream");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-confirm");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            var results = new List<EventSendResult>();
            for (var i = 0; i < 5; i++)
            {
                var result = await stream.SendAsync(
                    new EventStoreMessage
                    {
                        Channel = channel,
                        Body = Encoding.UTF8.GetBytes($"stream-event-{i}"),
                    },
                    "es-stream");
                results.Add(result);
            }

            results.Should().HaveCount(5);
            results.Should().AllSatisfy(r =>
            {
                r.Sent.Should().BeTrue();
                r.EventId.Should().NotBeNullOrWhiteSpace();
            });
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }
}
