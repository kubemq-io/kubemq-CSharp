using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queries;

/// <summary>
/// An immutable query message for request-reply RPC with optional caching.
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
public record QueryMessage
{
    /// <summary>Gets the target channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the query payload. Defaults to empty.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the optional client identifier override.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Gets the server-side timeout in seconds for query execution.
    /// If null, the client's <see cref="Client.KubeMQClientOptions.DefaultTimeout"/> is used.
    /// </summary>
    public int? TimeoutInSeconds { get; init; }

    /// <summary>Gets the optional cache key for server-side response caching.</summary>
    public string? CacheKey { get; init; }

    /// <summary>Gets the cache TTL in seconds. Used with <see cref="CacheKey"/>.</summary>
    public int? CacheTtlSeconds { get; init; }
}
