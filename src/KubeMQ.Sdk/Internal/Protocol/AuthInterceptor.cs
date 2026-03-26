using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.Sdk.Auth;
using KubeMQ.Sdk.Exceptions;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// gRPC client interceptor that injects authentication metadata on all four
/// call types (unary, server streaming, client streaming, duplex).
/// </summary>
/// <remarks>
/// <para><b>Unary calls</b> use a fully async token-fetch path when the cache is cold,
/// avoiding the sync-over-async deadlock risk in single-threaded synchronization contexts.</para>
/// <para><b>Streaming calls</b> use the cached token synchronously (never block). The cache
/// is always warm after <c>ConnectAsync</c> pre-warms it. If the token expires during a
/// long-lived stream, the server rejects the call and the SDK reconnects, which re-warms
/// the cache via the async <see cref="GetTokenAsync"/> path.</para>
/// </remarks>
internal sealed class AuthInterceptor : Interceptor, IDisposable
{
    private readonly ICredentialProvider? _credentialProvider;
    private readonly string? _staticToken;
    private readonly Metadata? _cachedStaticHeaders;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private volatile string? _cachedToken;
    private DateTimeOffset? _cachedExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthInterceptor"/> class.
    /// </summary>
    /// <param name="credentialProvider">Optional credential provider for dynamic tokens.</param>
    /// <param name="staticToken">Optional static token (takes lower precedence).</param>
    /// <param name="logger">Optional logger for auth events.</param>
    internal AuthInterceptor(
        ICredentialProvider? credentialProvider,
        string? staticToken,
        ILogger? logger)
    {
        _credentialProvider = credentialProvider;
        _staticToken = staticToken;
        _logger = logger;

        if (_staticToken is not null)
        {
            _cachedStaticHeaders = new Metadata { { "authorization", _staticToken } };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        // Fast path: static token — purely synchronous, no deadlock risk.
        if (_cachedStaticHeaders is not null)
        {
            var staticCtx = InjectHeaders(context, _cachedStaticHeaders);
            var call = continuation(request, staticCtx);
            return new AsyncUnaryCall<TResponse>(
                HandleAuthErrorAsync(call.ResponseAsync),
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

        // Fast path: cached dynamic token still valid — synchronous read of volatile field.
        var cached = _cachedToken;
        if (cached is not null && !IsTokenExpiringSoon())
        {
            var cachedCtx = InjectToken(context, cached);
            var call = continuation(request, cachedCtx);
            return new AsyncUnaryCall<TResponse>(
                HandleAuthErrorAsync(call.ResponseAsync),
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

        // No credential provider — pass through without auth.
        if (_credentialProvider is null)
        {
            var call = continuation(request, context);
            return new AsyncUnaryCall<TResponse>(
                HandleAuthErrorAsync(call.ResponseAsync),
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

        // Cold path: token expired or not yet cached. Fetch asynchronously to avoid
        // sync-over-async deadlock in single-threaded synchronization contexts.
        return new AsyncUnaryCall<TResponse>(
            FetchTokenAndCallUnaryAsync(request, context, continuation),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddCachedAuthMetadata(context);
        return continuation(request, newContext);
    }

    /// <inheritdoc />
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddCachedAuthMetadata(context);
        return continuation(newContext);
    }

    /// <inheritdoc />
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddCachedAuthMetadata(context);
        return continuation(newContext);
    }

    /// <summary>
    /// Async path for obtaining a token — used during ConnectAsync (which IS async)
    /// to pre-warm the cache, avoiding the cold path on the first call.
    /// </summary>
    internal async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_staticToken is not null)
        {
            return _staticToken;
        }

        if (_credentialProvider is null)
        {
            return null;
        }

        if (_cachedToken is not null && !IsTokenExpiringSoon())
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && !IsTokenExpiringSoon())
            {
                return _cachedToken;
            }

            var result = await _credentialProvider
                .GetTokenAsync(cancellationToken)
                .ConfigureAwait(false);

            _cachedToken = result.Token;
            _cachedExpiresAt = result.ExpiresAt;
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the cached token, forcing the next call to re-invoke the provider.
    /// </summary>
    internal void InvalidateCachedToken()
    {
        _tokenLock.Wait();
        try
        {
            _cachedToken = null;
            _cachedExpiresAt = null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static ClientInterceptorContext<TRequest, TResponse> InjectToken<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        string token)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();
        headers.Add("authorization", token);
        var newOptions = context.Options.WithHeaders(headers);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, newOptions);
    }

    private static ClientInterceptorContext<TRequest, TResponse> InjectHeaders<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        Metadata headers)
        where TRequest : class
        where TResponse : class
    {
        var opts = context.Options.WithHeaders(headers);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, opts);
    }

    /// <summary>
    /// Fetches a token asynchronously and then invokes the unary continuation with auth headers.
    /// This avoids the sync-over-async deadlock that <c>GetAwaiter().GetResult()</c> caused
    /// in single-threaded synchronization contexts.
    /// </summary>
    private async Task<TResponse> FetchTokenAndCallUnaryAsync<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        string? token;
        try
        {
            token = await GetTokenAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (KubeMQAuthenticationException)
        {
            throw;
        }
        catch (KubeMQConnectionException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or IOException)
        {
            throw new KubeMQConnectionException(
                "Credential provider infrastructure failure", ex);
        }
        catch (Exception ex)
        {
            throw new KubeMQAuthenticationException(
                "Credential provider failed to supply token", ex);
        }

        var authContext = token is not null ? InjectToken(context, token) : context;
        var call = continuation(request, authContext);
        return await HandleAuthErrorAsync(call.ResponseAsync).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the cached or static token to the context without blocking.
    /// Used by streaming call types where the cache is always warm after ConnectAsync.
    /// </summary>
    private ClientInterceptorContext<TRequest, TResponse> AddCachedAuthMetadata<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        if (_cachedStaticHeaders is not null)
        {
            return InjectHeaders(context, _cachedStaticHeaders);
        }

        // Read the volatile cached token — never blocks.
        var token = _cachedToken;
        if (token is null)
        {
            return context;
        }

        return InjectToken(context, token);
    }

    private bool IsTokenExpiringSoon()
    {
        if (_cachedExpiresAt is null)
        {
            return false;
        }

        return DateTimeOffset.UtcNow >= _cachedExpiresAt.Value.AddSeconds(-30);
    }

    private async Task<TResponse> HandleAuthErrorAsync<TResponse>(Task<TResponse> responseTask)
    {
        try
        {
            return await responseTask.ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            InvalidateCachedToken();
            throw new KubeMQAuthenticationException(
                $"Server rejected authentication: {ex.Status.Detail}", ex);
        }
    }
}
