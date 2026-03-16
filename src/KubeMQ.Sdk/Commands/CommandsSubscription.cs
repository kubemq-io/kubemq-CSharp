using System.Linq;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Commands;

/// <summary>
/// Configuration for subscribing to Commands channels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is thread-safe. A subscription can be cancelled
/// from any thread via the <see cref="CancellationToken"/> passed to the subscribe method.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class CommandsSubscription
{
    /// <summary>Gets or sets the channel name to subscribe to.</summary>
    public required string Channel { get; set; }

    /// <summary>Gets or sets the optional consumer group for load-balanced command handling.</summary>
    public string? Group { get; set; }

    /// <summary>
    /// Validates that all required fields are set.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new KubeMQConfigurationException("CommandsSubscription: Channel is required.");
        }

        if (Channel.Contains('*') || Channel.Contains('>'))
        {
            throw new KubeMQConfigurationException("Commands subscription channel cannot contain wildcards.");
        }

        if (Channel.Any(char.IsWhiteSpace))
        {
            throw new KubeMQConfigurationException("Commands subscription channel cannot contain whitespace.");
        }

        if (Channel.EndsWith('.'))
        {
            throw new KubeMQConfigurationException("Commands subscription channel cannot end with '.'.");
        }
    }
}
