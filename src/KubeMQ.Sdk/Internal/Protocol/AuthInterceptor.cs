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
        var newContext = AddAuthMetadata(context);
        var call = continuation(request, newContext);
        return new AsyncUnaryCall<TResponse>(
            HandleAuthErrorAsync(call.ResponseAsync),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddAuthMetadata(context);
        return continuation(request, newContext);
    }

    /// <inheritdoc />
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddAuthMetadata(context);
        return continuation(newContext);
    }

    /// <inheritdoc />
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = AddAuthMetadata(context);
        return continuation(newContext);
    }

    /// <summary>
    /// Async path for obtaining a token — used during ConnectAsync (which IS async)
    /// to pre-warm the cache, avoiding sync-over-async on the first call.
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

    private ClientInterceptorContext<TRequest, TResponse> AddAuthMetadata<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        if (_cachedStaticHeaders is not null)
        {
            var opts = context.Options.WithHeaders(_cachedStaticHeaders);
            return new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, opts);
        }

        var token = GetCachedTokenSync();
        if (token is null)
        {
            return context;
        }

        var headers = context.Options.Headers ?? new Metadata();
        headers.Add("authorization", token);

        var newOptions = context.Options.WithHeaders(headers);
        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, newOptions);
    }

    /// <remarks>
    /// <b>Known limitation — sync-over-async.</b>
    /// Uses .GetAwaiter().GetResult() because gRPC C# interceptor methods are synchronous.
    /// The SDK pre-warms the token cache during ConnectAsync() (which IS async),
    /// so this sync path is only hit on cache miss or token expiry.
    /// </remarks>
    private string? GetCachedTokenSync()
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

        _tokenLock.Wait();
        try
        {
            if (_cachedToken is not null && !IsTokenExpiringSoon())
            {
                return _cachedToken;
            }

            var result = _credentialProvider
                .GetTokenAsync(CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            _cachedToken = result.Token;
            _cachedExpiresAt = result.ExpiresAt;
            return _cachedToken;
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
        finally
        {
            _tokenLock.Release();
        }
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
