using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Queries;

/// <summary>
/// A query received from a subscription, requiring a response with data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is an immutable record. Instances are safe to read
/// from multiple threads.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public record QueryReceived
{
    /// <summary>Gets the channel the query was sent to.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the server-assigned request identifier for correlation.</summary>
    public required string RequestId { get; init; }

    /// <summary>Gets the query payload.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }

    /// <summary>Gets the reply channel for sending the response back.</summary>
    public string? ReplyChannel { get; init; }

    /// <summary>Gets the cache key if this query supports caching.</summary>
    public string? CacheKey { get; init; }
}
