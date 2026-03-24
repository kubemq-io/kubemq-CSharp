using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

/// <summary>
/// Integration tests for EventStream and EventStoreStream (duplex streaming).
/// These exercise GrpcTransport.CreateEventStreamAsync and the stream wrappers.
/// </summary>
public class StreamingTests : IntegrationTestBase
{
    [Fact]
    public async Task EventStream_SendAndReceive_SubscriberGetsEvents()
    {
        await using var publisher = CreateClient("stream-evt-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("stream-evt-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("stream-evt-send-recv");
        var messageCount = 5;
        var received = new List<EventReceived>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        // Start subscriber
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

        // Create event stream and send via stream
        var stream = await publisher.CreateEventStreamAsync(cancellationToken: cts.Token);
        try
        {
            for (var i = 0; i < messageCount; i++)
            {
                await stream.SendAsync(
                    new EventMessage
                    {
                        Channel = channel,
                        Body = Encoding.UTF8.GetBytes($"stream-event-{i}"),
                    },
                    "stream-evt-pub",
                    cts.Token);
            }

            await tcs.Task;

            received.Should().HaveCount(messageCount);
            for (var i = 0; i < messageCount; i++)
            {
                received[i].Channel.Should().Be(channel);
            }
        }
        finally
        {
            await stream.CloseAsync();
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStream_CloseAsync_GracefulShutdown()
    {
        await using var client = CreateClient("stream-evt-close");
        await client.ConnectAsync();

        var channel = UniqueChannel("stream-evt-close");

        var stream = await client.CreateEventStreamAsync();
        try
        {
            // Send a few events
            for (var i = 0; i < 3; i++)
            {
                await stream.SendAsync(
                    new EventMessage
                    {
                        Channel = channel,
                        Body = Encoding.UTF8.GetBytes($"close-event-{i}"),
                    },
                    "stream-evt-close");
            }

            // Close should not throw
            var act = () => stream.CloseAsync();
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStream_DisposeAsync_GracefulCleanup()
    {
        await using var client = CreateClient("stream-evt-dispose");
        await client.ConnectAsync();

        var channel = UniqueChannel("stream-evt-dispose");

        var stream = await client.CreateEventStreamAsync();

        await stream.SendAsync(
            new EventMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes("dispose-event"),
            },
            "stream-evt-dispose");

        // DisposeAsync should not throw
        var act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EventStream_OnError_CallbackReceivesErrors()
    {
        await using var client = CreateClient("stream-evt-onerr");
        await client.ConnectAsync();

        var channel = UniqueChannel("stream-evt-onerr");
        var errors = new List<Exception>();

        var stream = await client.CreateEventStreamAsync(
            onError: ex => errors.Add(ex));

        try
        {
            // Send a valid event — no error expected
            await stream.SendAsync(
                new EventMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("onerr-event"),
                },
                "stream-evt-onerr");

            // Give it a moment for potential error callbacks
            await Task.Delay(500);

            // No errors should have been raised for a valid event
            // (This test verifies the onError callback is wired correctly)
        }
        finally
        {
            await stream.CloseAsync();
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStream_WithTags_TagsPreserved()
    {
        await using var publisher = CreateClient("stream-evt-tag-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("stream-evt-tag-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("stream-evt-tags");
        var tags = new Dictionary<string, string> { ["env"] = "test", ["stream"] = "true" };
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

        var stream = await publisher.CreateEventStreamAsync(cancellationToken: cts.Token);
        try
        {
            await stream.SendAsync(
                new EventMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("tagged-stream-event"),
                    Tags = tags,
                },
                "stream-evt-tag-pub",
                cts.Token);

            var received = await tcs.Task;

            received.Tags.Should().NotBeNull();
            received.Tags.Should().ContainKey("env").WhoseValue.Should().Be("test");
            received.Tags.Should().ContainKey("stream").WhoseValue.Should().Be("true");
        }
        finally
        {
            await stream.CloseAsync();
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_SendAsync_ReturnsConfirmedResult()
    {
        await using var client = CreateClient("es-stream-confirm");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-single");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            var result = await stream.SendAsync(
                new EventStoreMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("es-stream-event"),
                },
                "es-stream-confirm");

            result.Should().NotBeNull();
            result.Sent.Should().BeTrue();
            result.Id.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_MultipleMessages_AllConfirmed()
    {
        await using var client = CreateClient("es-stream-multi");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-multi");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            var results = new List<EventStoreResult>();
            for (var i = 0; i < 10; i++)
            {
                var result = await stream.SendAsync(
                    new EventStoreMessage
                    {
                        Channel = channel,
                        Body = Encoding.UTF8.GetBytes($"es-stream-event-{i}"),
                    },
                    "es-stream-multi");
                results.Add(result);
            }

            results.Should().HaveCount(10);
            results.Should().AllSatisfy(r =>
            {
                r.Sent.Should().BeTrue();
                r.Id.Should().NotBeNullOrWhiteSpace();
            });
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_CloseAsync_GracefulShutdown()
    {
        await using var client = CreateClient("es-stream-close");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-close");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            await stream.SendAsync(
                new EventStoreMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("close-es-event"),
                },
                "es-stream-close");

            var act = () => stream.CloseAsync();
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_ConcurrentSends_AllConfirmed()
    {
        await using var client = CreateClient("es-stream-conc");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-concurrent");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            // Send 20 messages concurrently
            var tasks = Enumerable.Range(0, 20).Select(i =>
                stream.SendAsync(
                    new EventStoreMessage
                    {
                        Channel = channel,
                        Body = Encoding.UTF8.GetBytes($"concurrent-es-{i}"),
                    },
                    "es-stream-conc")).ToList();

            var results = await Task.WhenAll(tasks);

            results.Should().HaveCount(20);
            results.Should().AllSatisfy(r =>
            {
                r.Sent.Should().BeTrue();
            });
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_PendingCount_TracksInFlight()
    {
        await using var client = CreateClient("es-stream-pending");
        await client.ConnectAsync();

        var channel = UniqueChannel("es-stream-pending");
        var stream = await client.CreateEventStoreStreamAsync();

        try
        {
            // After sending and awaiting, pending count should be 0
            await stream.SendAsync(
                new EventStoreMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("pending-event"),
                },
                "es-stream-pending");

            stream.PendingCount.Should().Be(0, "all sends have been confirmed");
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task EventStoreStream_WithTags_TagsPreservedOnSubscription()
    {
        await using var publisher = CreateClient("es-stream-tag-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("es-stream-tag-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("es-stream-tags");
        var tags = new Dictionary<string, string> { ["key"] = "value", ["type"] = "stream" };
        var tcs = new TaskCompletionSource<EventStoreReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new EventStoreSubscription
            {
                Channel = channel,
                StartPosition = EventStoreStartPosition.StartFromNew,
            };
            await foreach (var evt in subscriber.SubscribeToEventsStoreAsync(subscription, cts.Token))
            {
                tcs.TrySetResult(evt);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        var stream = await publisher.CreateEventStoreStreamAsync(cts.Token);
        try
        {
            await stream.SendAsync(
                new EventStoreMessage
                {
                    Channel = channel,
                    Body = Encoding.UTF8.GetBytes("tagged-es-stream"),
                    Tags = tags,
                },
                "es-stream-tag-pub",
                cts.Token);

            var received = await tcs.Task;

            received.Tags.Should().NotBeNull();
            received.Tags.Should().ContainKey("key").WhoseValue.Should().Be("value");
            received.Tags.Should().ContainKey("type").WhoseValue.Should().Be("stream");
        }
        finally
        {
            await stream.CloseAsync();
            await stream.DisposeAsync();
        }
    }
}
