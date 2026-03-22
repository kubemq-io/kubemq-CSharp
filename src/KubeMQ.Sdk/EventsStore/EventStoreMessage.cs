using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.EventsStore;

/// <summary>
/// An immutable event store message for persistent publish/subscribe.
/// Messages are stored by the server and can be replayed from any position.
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
/// var message = new EventStoreMessage
/// {
///     Channel = "audit.logs",
///     Body = Encoding.UTF8.GetBytes("{\"action\":\"login\"}"),
///     Tags = new Dictionary&lt;string, string&gt; { ["userId"] = "42" },
/// };
/// var result = await client.SendEventStoreAsync(message);
/// Console.WriteLine($"Stored: {result.Sent}");
/// </code>
/// </example>
public record EventStoreMessage
{
    /// <summary>Gets the optional event ID. Auto-generated UUID if not provided.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the target channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the message payload. Defaults to empty.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the optional client identifier override.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }
}
