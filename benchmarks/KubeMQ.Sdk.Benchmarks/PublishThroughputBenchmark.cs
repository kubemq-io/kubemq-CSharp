using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Measures publish throughput in messages/sec at 1KB payload.
/// Requires a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MarkdownExporterAttribute.GitHub]
public class PublishThroughputBenchmark
{
    private KubeMQClient _client = null!;
    private EventMessage _message = null!;

    private const int MessageCount = 1000;

    [Params(1024)]
    public int PayloadSize { get; set; }

    [Params(true, false)]
    public bool ReuseMessage { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var options = new KubeMQClientOptions
        {
            Address = BenchmarkEnvironment.ServerAddress,
            ClientId = "bench-throughput",
        };

        _client = new KubeMQClient(options);
        await _client.ConnectAsync().ConfigureAwait(false);

        var body = new byte[PayloadSize];
        Random.Shared.NextBytes(body);
        _message = new EventMessage
        {
            Channel = "bench-throughput",
            Body = body,
        };
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public async Task PublishEvents()
    {
        for (var i = 0; i < MessageCount; i++)
        {
            var msg = ReuseMessage ? _message : _message with { Body = new byte[PayloadSize] };
            await _client.SendEventAsync(msg).ConfigureAwait(false);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
