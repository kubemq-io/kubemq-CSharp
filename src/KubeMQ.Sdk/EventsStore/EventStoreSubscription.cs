using System;
using System.Linq;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.EventsStore;

/// <summary>
/// Configuration for subscribing to EventStore channels with start position control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is thread-safe. A subscription can be cancelled
/// from any thread via the <see cref="CancellationToken"/> passed to the subscribe method.
/// </para>
/// <para>
/// Default start position is <see cref="EventStoreStartPosition.StartFromNew"/>,
/// which receives only events published after the subscription is established.
/// To replay existing events, set <see cref="StartPosition"/> to
/// <see cref="EventStoreStartPosition.StartFromFirst"/>, <see cref="EventStoreStartPosition.StartAtSequence"/>,
/// <see cref="EventStoreStartPosition.StartAtTime"/>, or <see cref="EventStoreStartPosition.StartAtTimeDelta"/>.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class EventStoreSubscription
{
    /// <summary>Gets or sets the channel name to subscribe to. Supports wildcards if server allows.</summary>
    public required string Channel { get; set; }

    /// <summary>Gets or sets the optional consumer group for load-balanced consumption.</summary>
    public string? Group { get; set; }

    /// <summary>Gets or sets where to begin reading from the store. Default: <see cref="EventStoreStartPosition.StartFromNew"/>.</summary>
    public EventStoreStartPosition StartPosition { get; set; } = EventStoreStartPosition.StartFromNew;

    /// <summary>
    /// Gets or sets the sequence number to start from. Required when <see cref="StartPosition"/> is
    /// <see cref="EventStoreStartPosition.StartAtSequence"/>.
    /// </summary>
    public long? StartSequence { get; set; }

    /// <summary>
    /// Gets or sets the point in time to start from. Required when <see cref="StartPosition"/> is
    /// <see cref="EventStoreStartPosition.StartAtTime"/>.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the relative time offset in seconds from now. Required when <see cref="StartPosition"/> is
    /// <see cref="EventStoreStartPosition.StartAtTimeDelta"/>.
    /// Must be positive (e.g., 3600 means "start from 1 hour ago").
    /// </summary>
    public int? StartTimeDeltaSeconds { get; set; }

    /// <summary>
    /// Validates that all required fields are set and values are within valid ranges.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new KubeMQConfigurationException("EventStoreSubscription: Channel is required.");
        }

        if (Channel.Contains('*') || Channel.Contains('>'))
        {
            throw new KubeMQConfigurationException("Events Store subscription channel cannot contain wildcards.");
        }

        if (Channel.Any(char.IsWhiteSpace))
        {
            throw new KubeMQConfigurationException("Events Store subscription channel cannot contain whitespace.");
        }

        if (Channel.EndsWith('.'))
        {
            throw new KubeMQConfigurationException("Events Store subscription channel cannot end with '.'.");
        }

        switch (StartPosition)
        {
            case EventStoreStartPosition.StartAtSequence:
                if (StartSequence is null or <= 0)
                {
                    throw new KubeMQConfigurationException(
                        "EventStoreSubscription: StartSequence must be a positive value (> 0) when StartPosition is StartAtSequence.");
                }

                break;

            case EventStoreStartPosition.StartAtTime:
                if (StartTime is null)
                {
                    throw new KubeMQConfigurationException(
                        "EventStoreSubscription: StartTime is required when StartPosition is StartAtTime.");
                }

                if (StartTime.Value.ToUnixTimeSeconds() <= 0)
                {
                    throw new KubeMQConfigurationException(
                        "EventStoreSubscription: StartTime must be a valid time after Unix epoch (> 0).");
                }

                break;

            case EventStoreStartPosition.StartAtTimeDelta:
                if (StartTimeDeltaSeconds is null or <= 0)
                {
                    throw new KubeMQConfigurationException(
                        "EventStoreSubscription: StartTimeDeltaSeconds must be a positive value when StartPosition is StartAtTimeDelta.");
                }

                break;
        }
    }
}
