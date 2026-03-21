using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Micro-benchmarks measuring protobuf serialization allocations and throughput.
/// Does NOT require a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class SerializationBenchmarks
{
    private EventMessage _event1Kb = null!;
    private EventMessage _event64Kb = null!;
    private byte[] _serialized1Kb = null!;

    [GlobalSetup]
    public void Setup()
    {
        var body1Kb = new byte[1024];
        Random.Shared.NextBytes(body1Kb);

        var body64Kb = new byte[65536];
        Random.Shared.NextBytes(body64Kb);

        var tags = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
        };

        _event1Kb = new EventMessage
        {
            Channel = "bench-serialize",
            Body = body1Kb,
            Tags = tags,
        };

        _event64Kb = new EventMessage
        {
            Channel = "bench-serialize",
            Body = body64Kb,
            Tags = tags,
        };

        _serialized1Kb = EncodeEvent(_event1Kb, "bench-client").ToByteArray();
    }

    [Benchmark(Baseline = true)]
    public KubeMQ.Grpc.Event Encode1Kb()
    {
        return EncodeEvent(_event1Kb, "bench-client");
    }

    [Benchmark]
    public KubeMQ.Grpc.Event Encode64Kb()
    {
        return EncodeEvent(_event64Kb, "bench-client");
    }

    [Benchmark]
    public byte[] Encode1Kb_ToBytes()
    {
        var evt = EncodeEvent(_event1Kb, "bench-client");
        return evt.ToByteArray();
    }

    [Benchmark]
    public KubeMQ.Grpc.Event Decode1Kb()
    {
        return KubeMQ.Grpc.Event.Parser.ParseFrom(_serialized1Kb);
    }

    /// <summary>
    /// Mirrors the encoding logic in KubeMQClient.SendEventAsync to measure
    /// the exact allocation overhead of proto message construction.
    /// </summary>
    private static KubeMQ.Grpc.Event EncodeEvent(EventMessage message, string clientId)
    {
        var pbEvent = new KubeMQ.Grpc.Event
        {
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? clientId,
            Store = false,
        };

        if (message.Tags is not null)
        {
            foreach (var kvp in message.Tags)
            {
                pbEvent.Tags[kvp.Key] = kvp.Value;
            }
        }

        return pbEvent;
    }
}
