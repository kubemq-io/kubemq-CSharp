using System.Linq;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Events;

/// <summary>
/// Configuration for subscribing to Events channels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is thread-safe. A subscription can be cancelled
/// from any thread via the <see cref="CancellationToken"/> passed to the subscribe method.
/// </para>
/// <para>
/// Channel names support wildcard patterns if the server is configured to allow them
/// (e.g., <c>"orders.*"</c> to receive events on all order sub-channels).
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class EventsSubscription
{
    /// <summary>Gets or sets the channel name to subscribe to. Supports wildcards if server allows.</summary>
    public required string Channel { get; set; }

    /// <summary>Gets or sets the optional consumer group for load-balanced consumption.</summary>
    public string? Group { get; set; }

    /// <summary>
    /// Validates that all required fields are set and values are within valid ranges.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new KubeMQConfigurationException("EventsSubscription: Channel is required.");
        }

        if (Channel.Any(char.IsWhiteSpace))
        {
            throw new KubeMQConfigurationException("Events subscription channel cannot contain whitespace.");
        }

        if (Channel.EndsWith('.'))
        {
            throw new KubeMQConfigurationException("Events subscription channel cannot end with '.'.");
        }
    }
}
