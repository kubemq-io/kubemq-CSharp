using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// An immutable queue message for point-to-point delivery.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads. Create a new instance per send operation; do not share
/// mutable state across threads via this type.
/// </para>
/// <para><b>Equality caveat:</b> <see cref="ReadOnlyMemory{T}"/> equality checks reference
/// identity, not byte content. See <see cref="Events.EventMessage"/> remarks for details.</para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
/// <example>
/// <code>
/// var message = new QueueMessage
/// {
///     Channel = "tasks.pending",
///     Body = Encoding.UTF8.GetBytes("{\"task\":\"process-order\"}"),
///     DelaySeconds = 30,
///     ExpirationSeconds = 3600,
///     MaxReceiveCount = 3,
///     MaxReceiveQueue = "tasks.dlq",
/// };
/// var result = await client.SendQueueMessageAsync(message);
/// </code>
/// </example>
public record QueueMessage
{
    /// <summary>Gets the target queue channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the message payload. Defaults to empty.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the optional client identifier override.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }

    /// <summary>Gets the delay delivery by this many seconds. Null means immediate delivery.</summary>
    public int? DelaySeconds { get; init; }

    /// <summary>Gets the expiration in seconds after which the message expires. Null means no expiration.</summary>
    public int? ExpirationSeconds { get; init; }

    /// <summary>
    /// Gets the maximum number of receive attempts before the message is moved to the DLQ.
    /// Null means no DLQ limit.
    /// </summary>
    public int? MaxReceiveCount { get; init; }

    /// <summary>
    /// Gets the dead letter queue channel name. Used with <see cref="MaxReceiveCount"/>.
    /// </summary>
    public string? MaxReceiveQueue { get; init; }
}
