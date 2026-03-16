using BenchmarkDotNet.Attributes;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Benchmarks;

/// <summary>
/// Benchmarks message construction and validation overhead to verify that
/// fail-fast validation adds negligible cost per operation.
/// Does NOT require a running KubeMQ server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class MessageValidationBenchmarks
{
    private byte[] _body1Kb = null!;
    private IReadOnlyDictionary<string, string> _tags = null!;
    private EventMessage _validEvent = null!;
    private QueueMessage _validQueue = null!;
    private CommandMessage _validCommand = null!;

    [GlobalSetup]
    public void Setup()
    {
        _body1Kb = new byte[1024];
        Random.Shared.NextBytes(_body1Kb);

        _tags = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            ["key3"] = "value3",
        };

        _validEvent = new EventMessage
        {
            Channel = "bench-validate",
            Body = _body1Kb,
            Tags = _tags,
        };

        _validQueue = new QueueMessage
        {
            Channel = "bench-validate",
            Body = _body1Kb,
            Tags = _tags,
            DelaySeconds = 10,
            ExpirationSeconds = 3600,
            MaxReceiveCount = 3,
        };

        _validCommand = new CommandMessage
        {
            Channel = "bench-validate",
            Body = _body1Kb,
            Tags = _tags,
            TimeoutInSeconds = 30,
        };
    }

    [Benchmark(Baseline = true)]
    public EventMessage CreateEventMessage()
    {
        return new EventMessage
        {
            Channel = "bench-validate",
            Body = _body1Kb,
            Tags = _tags,
        };
    }

    [Benchmark]
    public QueueMessage CreateQueueMessage()
    {
        return new QueueMessage
        {
            Channel = "bench-validate",
            Body = _body1Kb,
            Tags = _tags,
            DelaySeconds = 10,
            ExpirationSeconds = 3600,
        };
    }

    [Benchmark]
    public bool ValidateEventMessage()
    {
        return ValidateEvent(_validEvent);
    }

    [Benchmark]
    public bool ValidateQueueMessage()
    {
        return ValidateQueue(_validQueue);
    }

    [Benchmark]
    public bool ValidateCommandMessage()
    {
        return ValidateCommand(_validCommand);
    }

    [Benchmark]
    public EventMessage CreateEventWithCopy()
    {
        return _validEvent with { Channel = "bench-validate-copy" };
    }

    /// <summary>
    /// Mirrors MessageValidator.ValidateEventMessage (internal) to measure
    /// the validation overhead without needing InternalsVisibleTo access.
    /// </summary>
    private static bool ValidateEvent(EventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return !string.IsNullOrWhiteSpace(message.Channel);
    }

    private static bool ValidateQueue(QueueMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Channel))
            return false;
        if (message.DelaySeconds is < 0)
            return false;
        if (message.ExpirationSeconds is < 0)
            return false;
        if (message.MaxReceiveCount is < 0)
            return false;
        return true;
    }

    private static bool ValidateCommand(CommandMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Channel))
            return false;
        if (message.TimeoutInSeconds is <= 0)
            return false;
        return true;
    }
}
