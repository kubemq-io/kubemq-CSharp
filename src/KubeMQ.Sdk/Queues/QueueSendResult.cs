using System;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Result of a queue send operation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record QueueSendResult
{
    /// <summary>Gets the server-assigned message identifier.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets when the message was accepted by the server.</summary>
    public DateTimeOffset SentAt { get; init; }

    /// <summary>Gets a value indicating whether the server reported an error.</summary>
    public bool IsError { get; init; }

    /// <summary>Gets the error message from the server, if any.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the epoch seconds when a delayed message will become visible. Null if not delayed.</summary>
    public int? DelayedTo { get; init; }

    /// <summary>Gets the epoch seconds when the message will expire. Null if no expiration.</summary>
    public int? ExpiresAt { get; init; }

    /// <summary>
    /// Gets per-message results when this result represents a batch send.
    /// Null for single-message sends.
    /// </summary>
    public IReadOnlyList<QueueSendResult>? BatchResults { get; init; }
}
