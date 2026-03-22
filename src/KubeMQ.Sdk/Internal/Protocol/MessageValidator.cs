using System.Linq;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Fail-fast validation for outgoing message types. Called by
/// <c>KubeMQClient</c> before any transport call.
/// </summary>
internal static class MessageValidator
{
    /// <summary>
    /// Validates an <see cref="EventMessage"/> before publish.
    /// </summary>
    /// <param name="message">The event message to validate.</param>
    /// <param name="maxBodySize">Maximum body size in bytes. 0 disables the check.</param>
    internal static void ValidateEventMessage(EventMessage message, int maxBodySize = 0)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateChannelFormat(message.Channel);
        ValidateBodySize(message.Body, maxBodySize);
    }

    /// <summary>
    /// Validates an <see cref="EventStoreMessage"/> before publish.
    /// </summary>
    /// <param name="message">The event store message to validate.</param>
    /// <param name="maxBodySize">Maximum body size in bytes. 0 disables the check.</param>
    internal static void ValidateEventStoreMessage(EventStoreMessage message, int maxBodySize = 0)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateChannelFormat(message.Channel);
        ValidateBodySize(message.Body, maxBodySize);
    }

    /// <summary>
    /// Validates a <see cref="QueueMessage"/> before send.
    /// </summary>
    /// <param name="message">The queue message to validate.</param>
    /// <param name="maxBodySize">Maximum body size in bytes. 0 disables the check.</param>
    internal static void ValidateQueueMessage(QueueMessage message, int maxBodySize = 0)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateChannelFormat(message.Channel);
        ValidateBodySize(message.Body, maxBodySize);

        if (message.DelaySeconds is < 0)
        {
            throw new KubeMQConfigurationException("QueueMessage: DelaySeconds must be non-negative.");
        }

        if (message.ExpirationSeconds is < 0)
        {
            throw new KubeMQConfigurationException("QueueMessage: ExpirationSeconds must be non-negative.");
        }

        if (message.MaxReceiveCount is < 0)
        {
            throw new KubeMQConfigurationException("QueueMessage: MaxReceiveCount must be non-negative.");
        }
    }

    /// <summary>
    /// Validates a <see cref="CommandMessage"/> before send.
    /// </summary>
    /// <param name="message">The command message to validate.</param>
    /// <param name="maxBodySize">Maximum body size in bytes. 0 disables the check.</param>
    internal static void ValidateCommandMessage(CommandMessage message, int maxBodySize = 0)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateChannelFormat(message.Channel);
        ValidateBodySize(message.Body, maxBodySize);

        if (message.TimeoutInSeconds is <= 0)
        {
            throw new KubeMQConfigurationException(
                "CommandMessage: TimeoutInSeconds must be positive when set.");
        }
    }

    /// <summary>
    /// Validates a <see cref="QueryMessage"/> before send.
    /// </summary>
    /// <param name="message">The query message to validate.</param>
    /// <param name="maxBodySize">Maximum body size in bytes. 0 disables the check.</param>
    internal static void ValidateQueryMessage(QueryMessage message, int maxBodySize = 0)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateChannelFormat(message.Channel);
        ValidateBodySize(message.Body, maxBodySize);

        if (message.TimeoutInSeconds is <= 0)
        {
            throw new KubeMQConfigurationException(
                "QueryMessage: TimeoutInSeconds must be positive when set.");
        }

        if (message.CacheTtlSeconds is <= 0)
        {
            throw new KubeMQConfigurationException(
                "QueryMessage: CacheTtlSeconds must be positive when set.");
        }

        if (!string.IsNullOrEmpty(message.CacheKey) && (message.CacheTtlSeconds == null || message.CacheTtlSeconds <= 0))
        {
            throw new KubeMQConfigurationException(
                "CacheTtlSeconds must be greater than 0 when CacheKey is set.");
        }
    }

    /// <summary>
    /// Validates that a message body does not exceed the configured maximum size.
    /// </summary>
    /// <param name="body">The message body.</param>
    /// <param name="maxSize">Maximum size in bytes. 0 disables the check.</param>
    internal static void ValidateBodySize(ReadOnlyMemory<byte> body, int maxSize)
    {
        if (maxSize > 0 && body.Length > maxSize)
        {
            throw new KubeMQConfigurationException(
                $"Message body size ({body.Length:N0} bytes) exceeds configured maximum " +
                $"({maxSize:N0} bytes). Increase KubeMQClientOptions.MaxMessageBodySize " +
                "if larger messages are needed.");
        }
    }

    /// <summary>
    /// Validates channel name format: non-empty, no embedded whitespace,
    /// no trailing dot, and optionally no wildcards.
    /// </summary>
    /// <param name="channel">The channel name to validate.</param>
    /// <param name="allowWildcards">Whether to allow wildcard characters.</param>
    private static void ValidateChannelFormat(string channel, bool allowWildcards = false)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new KubeMQConfigurationException("Channel is required.");
        }

        if (!allowWildcards && (channel.Contains('*') || channel.Contains('>')))
        {
            throw new KubeMQConfigurationException("Channel cannot contain wildcards.");
        }

        if (channel.Any(char.IsWhiteSpace))
        {
            throw new KubeMQConfigurationException("Channel cannot contain whitespace.");
        }

        if (channel.EndsWith('.'))
        {
            throw new KubeMQConfigurationException("Channel cannot end with '.'.");
        }
    }
}
