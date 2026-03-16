using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeMQ.Sdk.Auth;

/// <summary>
/// Built-in credential provider that returns a static token with no expiry.
/// Used internally when <see cref="Client.KubeMQClientOptions.AuthToken"/>
/// is set directly.
/// </summary>
public sealed class StaticTokenProvider : ICredentialProvider
{
    private readonly CredentialResult _result;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticTokenProvider"/> class.
    /// </summary>
    /// <param name="token">The static authentication token. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when token is null or whitespace.</exception>
    public StaticTokenProvider(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token, nameof(token));
        _result = new CredentialResult(token);
    }

    /// <inheritdoc />
    public Task<CredentialResult> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_result);
    }
}
