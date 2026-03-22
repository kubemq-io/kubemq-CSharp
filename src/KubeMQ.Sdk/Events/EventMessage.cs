using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Events;

/// <summary>
/// An immutable event message for publish/subscribe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads. Create a new instance per send operation; do not share
/// mutable state across threads via this type.
/// </para>
/// <para><b>Equality caveat:</b> C# record equality uses <c>EqualityComparer&lt;T&gt;.Default</c>
/// for each property. <see cref="ReadOnlyMemory{T}"/> equality checks reference identity,
/// not byte content. Two <c>EventMessage</c> instances with identical byte content in
/// separately allocated buffers will NOT be considered equal by default record equality.
/// If content-based equality is needed, override <c>Equals</c>/<c>GetHashCode</c> or use
/// <c>body.Span.SequenceEqual(other.Body.Span)</c>.</para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
/// <example>
/// <code>
/// var message = new EventMessage
/// {
///     Channel = "orders.created",
///     Body = Encoding.UTF8.GetBytes("{\"orderId\":123}"),
///     Tags = new Dictionary&lt;string, string&gt; { ["source"] = "web" },
/// };
/// await client.SendEventAsync(message);
/// </code>
/// </example>
public record EventMessage
{
    /// <summary>Gets the optional event ID. Auto-generated UUID if not provided.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the target channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the message payload. Defaults to empty.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// Gets the optional client identifier. If null, the client's configured
    /// <see cref="Client.KubeMQClientOptions.ClientId"/> is used.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }
}
