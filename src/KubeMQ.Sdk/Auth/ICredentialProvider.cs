using System.Threading;
using System.Threading.Tasks;

namespace KubeMQ.Sdk.Auth;

/// <summary>
/// Pluggable credential provider for obtaining authentication tokens.
/// Implement this interface for custom token sources (Vault, OIDC, cloud IAM).
/// </summary>
/// <remarks>
/// <para><b>Thread safety:</b> Implementations do NOT need to be thread-safe.
/// The SDK serializes calls to <see cref="GetTokenAsync"/> — at most one
/// outstanding call at a time.</para>
/// <para><b>Invocation triggers:</b> The provider is called when no cached token
/// exists, when the cached token is invalidated by a server UNAUTHENTICATED
/// response, or when proactive refresh determines the token is approaching
/// expiry (within 30 seconds of <see cref="CredentialResult.ExpiresAt"/>).</para>
/// </remarks>
public interface ICredentialProvider
{
    /// <summary>
    /// Obtains a token for authenticating gRPC requests.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation token. During CONNECTING, this is the connection timeout token.
    /// During RECONNECTING, this is the reconnection attempt timeout token.
    /// </param>
    /// <returns>
    /// A <see cref="CredentialResult"/> containing the token and optional expiry hint.
    /// </returns>
    /// <exception cref="Exceptions.KubeMQAuthenticationException">
    /// Thrown when credentials are definitively invalid (expired API key, revoked access).
    /// The SDK treats this as non-retryable.
    /// </exception>
    /// <exception cref="Exceptions.KubeMQConnectionException">
    /// Thrown when the credential store is temporarily unavailable (Vault down, network timeout).
    /// The SDK treats this as retryable/transient.
    /// </exception>
    Task<CredentialResult> GetTokenAsync(CancellationToken cancellationToken = default);
}
