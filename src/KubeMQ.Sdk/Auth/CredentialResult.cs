using System;

namespace KubeMQ.Sdk.Auth;

/// <summary>
/// Result of a credential provider token request.
/// </summary>
/// <param name="Token">The authentication token string.</param>
/// <param name="ExpiresAt">
/// Optional expiry hint. When provided, the SDK uses it for proactive refresh
/// scheduling — refreshing the token before expiry to prevent request failures.
/// When null, the SDK only refreshes reactively (on UNAUTHENTICATED response).
/// </param>
public sealed record CredentialResult(string Token, DateTimeOffset? ExpiresAt = null);
