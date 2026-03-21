using System.Diagnostics;
using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace KubeMQ.Sdk.Tests.Integration;

/// <summary>
/// Performance integration tests validating throughput improvements.
/// These require a live broker on localhost:50000.
/// </summary>
public class PerformanceTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Commands_ShouldSustain4000PerSecond_SingleChannel()
    {
        await using var sender = CreateClient("perf-cmd-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("perf-cmd-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("perf-cmd-4k");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Start responder
        _ = Task.Run(async () =>
        {
            var subscription = new CommandsSubscription { Channel = channel };
            await foreach (var cmd in handler.SubscribeToCommandsAsync(subscription, cts.Token))
            {
                _ = handler.SendCommandResponseAsync(new CommandResponse
                {
                    RequestId = cmd.RequestId,
                    ReplyChannel = cmd.ReplyChannel!,
                    Executed = true,
                });
            }
        }, cts.Token);

        await Task.Delay(1000);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            await sender.SendCommandAsync(new CommandMessage
            {
                Channel = channel,
                Body = new byte[1024],
                TimeoutInSeconds = 5,
            });
        }

        // Measure throughput
        const int targetRate = 4000;
        const int durationSeconds = 5;
        int totalSent = 0;
        int errors = 0;

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();

        while (sw.Elapsed.TotalSeconds < durationSeconds)
        {
            int batchSize = Math.Min(50, targetRate - (int)(totalSent / Math.Max(sw.Elapsed.TotalSeconds, 0.001)));
            if (batchSize <= 0)
            {
                batchSize = 10;
            }

            for (int i = 0; i < batchSize; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await sender.SendCommandAsync(new CommandMessage
                        {
                            Channel = channel,
                            Body = new byte[1024],
                            TimeoutInSeconds = 5,
                        });
                        Interlocked.Increment(ref totalSent);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }));
            }

            // Drain completed tasks to avoid memory buildup
            if (tasks.Count > 500)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        double actualRate = totalSent / sw.Elapsed.TotalSeconds;
        _output.WriteLine($"Commands: {totalSent} in {sw.Elapsed.TotalSeconds:F1}s = {actualRate:F0}/s (errors: {errors})");

        actualRate.Should().BeGreaterThan(3500, "commands should sustain at least 3500/s after optimization");
    }

    [Fact]
    public async Task Queries_ShouldSustain4000PerSecond_SingleChannel()
    {
        await using var sender = CreateClient("perf-qry-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("perf-qry-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("perf-qry-4k");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Start responder
        _ = Task.Run(async () =>
        {
            var subscription = new QueriesSubscription { Channel = channel };
            await foreach (var qry in handler.SubscribeToQueriesAsync(subscription, cts.Token))
            {
                _ = handler.SendQueryResponseAsync(new QueryResponse
                {
                    RequestId = qry.RequestId,
                    ReplyChannel = qry.ReplyChannel!,
                    Executed = true,
                    Body = qry.Body,
                });
            }
        }, cts.Token);

        await Task.Delay(1000);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            await sender.SendQueryAsync(new QueryMessage
            {
                Channel = channel,
                Body = new byte[1024],
                TimeoutInSeconds = 5,
            });
        }

        // Measure throughput
        const int durationSeconds = 5;
        int totalSent = 0;
        int errors = 0;

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();

        while (sw.Elapsed.TotalSeconds < durationSeconds)
        {
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await sender.SendQueryAsync(new QueryMessage
                        {
                            Channel = channel,
                            Body = new byte[1024],
                            TimeoutInSeconds = 5,
                        });
                        Interlocked.Increment(ref totalSent);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }));
            }

            if (tasks.Count > 500)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        double actualRate = totalSent / sw.Elapsed.TotalSeconds;
        _output.WriteLine($"Queries: {totalSent} in {sw.Elapsed.TotalSeconds:F1}s = {actualRate:F0}/s (errors: {errors})");

        actualRate.Should().BeGreaterThan(3500, "queries should sustain at least 3500/s after optimization");
    }

    [Fact]
    public async Task Events_StreamThroughput_ShouldExceed5000PerSecond()
    {
        await using var publisher = CreateClient("perf-evt-pub");
        await publisher.ConnectAsync();

        await using var subscriber = CreateClient("perf-evt-sub");
        await subscriber.ConnectAsync();

        var channel = UniqueChannel("perf-evt-5k");
        int received = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        _ = Task.Run(async () =>
        {
            var subscription = new EventsSubscription { Channel = channel };
            await foreach (var _ in subscriber.SubscribeToEventsAsync(subscription, cts.Token))
            {
                Interlocked.Increment(ref received);
            }
        }, cts.Token);

        await Task.Delay(1000);

        var stream = await publisher.CreateEventStreamAsync();

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            await stream.SendAsync(
                new EventMessage { Channel = channel, Body = new byte[1024] },
                "perf-evt-pub");
        }

        // Measure
        const int totalMessages = 25000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < totalMessages; i++)
        {
            await stream.SendAsync(
                new EventMessage { Channel = channel, Body = new byte[1024] },
                "perf-evt-pub");
        }

        sw.Stop();
        double sendRate = totalMessages / sw.Elapsed.TotalSeconds;

        await stream.CloseAsync();
        _output.WriteLine($"Events: {totalMessages} in {sw.Elapsed.TotalSeconds:F1}s = {sendRate:F0}/s");

        sendRate.Should().BeGreaterThan(5000, "event stream publish should exceed 5000/s");
    }

    [Fact]
    public async Task EventsStore_StreamThroughput_ShouldExceed3000PerSecond()
    {
        await using var publisher = CreateClient("perf-es-pub");
        await publisher.ConnectAsync();

        var channel = UniqueChannel("perf-es-3k");

        var stream = await publisher.CreateEventStoreStreamAsync();

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            await stream.SendAsync(
                new EventStoreMessage { Channel = channel, Body = new byte[1024] },
                "perf-es-pub");
        }

        // Measure
        const int totalMessages = 15000;
        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>(totalMessages);

        for (int i = 0; i < totalMessages; i++)
        {
            tasks.Add(stream.SendAsync(
                new EventStoreMessage { Channel = channel, Body = new byte[1024] },
                "perf-es-pub"));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        double sendRate = totalMessages / sw.Elapsed.TotalSeconds;

        await stream.CloseAsync();
        _output.WriteLine($"EventsStore: {totalMessages} in {sw.Elapsed.TotalSeconds:F1}s = {sendRate:F0}/s");

        sendRate.Should().BeGreaterThan(3000, "event store stream publish should exceed 3000/s");
    }
}
