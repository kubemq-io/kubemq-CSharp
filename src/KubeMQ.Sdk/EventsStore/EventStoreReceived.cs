using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.EventsStore;

/// <summary>
/// An event store message received from a subscription, with sequence number.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record EventStoreReceived
{
    /// <summary>Gets the server-assigned event identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the channel the event was published to.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the event payload.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the client ID of the publisher.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }

    /// <summary>Gets the server-assigned sequence number within the channel.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the server timestamp when the event was stored.</summary>
    public DateTimeOffset Timestamp { get; init; }
}
