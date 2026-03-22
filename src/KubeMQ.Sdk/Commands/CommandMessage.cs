using System;
using System.Collections.Generic;

namespace KubeMQ.Sdk.Commands;

/// <summary>
/// An immutable command message for fire-and-await-ack RPC.
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
/// var command = new CommandMessage
/// {
///     Channel = "device.reboot",
///     Body = Encoding.UTF8.GetBytes("{\"deviceId\":\"sensor-1\"}"),
///     TimeoutInSeconds = 10,
/// };
/// var response = await client.SendCommandAsync(command);
/// Console.WriteLine($"Executed: {response.Executed}");
/// </code>
/// </example>
public record CommandMessage
{
    /// <summary>Gets the target channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets the command payload. Defaults to empty.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Gets the optional client identifier override.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Gets the server-side timeout in seconds for command execution.
    /// If null, the client's <see cref="Client.KubeMQClientOptions.DefaultTimeout"/> is used.
    /// </summary>
    public int? TimeoutInSeconds { get; init; }
}
