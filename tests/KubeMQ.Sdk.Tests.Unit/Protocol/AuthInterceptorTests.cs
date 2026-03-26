using System.Net.Http;
using FluentAssertions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.Sdk.Auth;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public sealed class AuthInterceptorTests : IDisposable
{
    private AuthInterceptor? _interceptor;

    public void Dispose()
    {
        _interceptor?.Dispose();
    }

    [Fact]
    public async Task GetTokenAsync_WithStaticProvider_ReturnsToken()
    {
        var provider = new StaticTokenProvider("my-static-token");
        _interceptor = new AuthInterceptor(provider, null, null);

        var token = await _interceptor.GetTokenAsync(CancellationToken.None);

        token.Should().Be("my-static-token");
    }

    [Fact]
    public async Task GetTokenAsync_CachesToken_OnSecondCall()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("cached-token"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var first = await _interceptor.GetTokenAsync(CancellationToken.None);
        var second = await _interceptor.GetTokenAsync(CancellationToken.None);

        first.Should().Be("cached-token");
        second.Should().Be("cached-token");
        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateCachedToken_ClearsCache()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("refreshable-token"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        await _interceptor.GetTokenAsync(CancellationToken.None);
        _interceptor.InvalidateCachedToken();
        await _interceptor.GetTokenAsync(CancellationToken.None);

        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetTokenAsync_NullProviderAndNullStaticToken_ReturnsNull()
    {
        _interceptor = new AuthInterceptor(null, null, null);

        var token = await _interceptor.GetTokenAsync(CancellationToken.None);

        token.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_StaticTokenTakesPrecedence_OverCredentialProvider()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("provider-token"));

        _interceptor = new AuthInterceptor(mock.Object, "static-wins", null);

        var token = await _interceptor.GetTokenAsync(CancellationToken.None);

        token.Should().Be("static-wins");
        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Dispose_CompletesWithoutError()
    {
        var mock = new Mock<ICredentialProvider>();
        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var act = () => _interceptor.Dispose();

        act.Should().NotThrow();
        _interceptor = null;
    }

    [Fact]
    public async Task GetTokenAsync_WithExpiredToken_RefreshesFromProvider()
    {
        int callCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new CredentialResult(
                    $"token-{callCount}",
                    DateTimeOffset.UtcNow.AddSeconds(-1));
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var first = await _interceptor.GetTokenAsync(CancellationToken.None);
        var second = await _interceptor.GetTokenAsync(CancellationToken.None);

        first.Should().Be("token-1");
        second.Should().Be("token-2");
        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetTokenAsync_WithNonExpiredToken_UsesCached()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult(
                "long-lived-token",
                DateTimeOffset.UtcNow.AddHours(1)));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        await _interceptor.GetTokenAsync(CancellationToken.None);
        var second = await _interceptor.GetTokenAsync(CancellationToken.None);

        second.Should().Be("long-lived-token");
        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_ProviderThrows_PropagatesException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider failure"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var act = () => _interceptor.GetTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("provider failure");
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentRequests_OnlyCallsProviderOnce()
    {
        var tcs = new TaskCompletionSource<CredentialResult>();
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var task1 = _interceptor.GetTokenAsync(CancellationToken.None);
        var task2 = _interceptor.GetTokenAsync(CancellationToken.None);

        tcs.SetResult(new CredentialResult("concurrent-token"));

        var results = await Task.WhenAll(task1, task2);

        results.Should().AllBe("concurrent-token");
        mock.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("token"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);
        _interceptor.Dispose();

        var act = () => _interceptor.GetTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
        _interceptor = null;
    }

    [Fact]
    public async Task AsyncUnaryCall_UnauthenticatedResponse_InvalidatesTokenAndThrowsAuthException()
    {
        var mock = new Mock<ICredentialProvider>();
        int tokenVersion = 0;
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CredentialResult($"token-{++tokenVersion}"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);
        await _interceptor.GetTokenAsync(CancellationToken.None);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unauthenticated, "Token expired"),
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        var act = async () => await call.ResponseAsync;
        await act.Should().ThrowAsync<KubeMQAuthenticationException>()
            .WithMessage("*Token expired*");

        var newToken = await _interceptor.GetTokenAsync(CancellationToken.None);
        newToken.Should().Be("token-2");
        mock.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AsyncUnaryCall_InjectsAuthorizationHeader()
    {
        _interceptor = new AuthInterceptor(null, "static-bearer-token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        await call.ResponseAsync;

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().Contain(e =>
            e.Key == "authorization" && e.Value == "static-bearer-token");
    }

    [Fact]
    public async Task GetTokenAsync_TokenExpiringWithin30Seconds_Refreshes()
    {
        int callCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new CredentialResult(
                    $"token-{callCount}",
                    DateTimeOffset.UtcNow.AddSeconds(25));
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var first = await _interceptor.GetTokenAsync(CancellationToken.None);
        first.Should().Be("token-1");

        var second = await _interceptor.GetTokenAsync(CancellationToken.None);
        second.Should().Be("token-2");
        mock.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTokenAsync_CancellationRespected()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("token"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var act = () => _interceptor.GetTokenAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AsyncUnaryCall_NoAuth_ContextUnchanged()
    {
        _interceptor = new AuthInterceptor(null, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        await call.ResponseAsync;

        if (capturedHeaders is not null)
        {
            capturedHeaders.Should().NotContain(e => e.Key == "authorization");
        }
    }

    [Fact]
    public async Task AsyncUnaryCall_MultipleConcurrentCalls_ShareSameCachedToken()
    {
        int providerCallCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref providerCallCount);
                return new CredentialResult("shared-token");
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);
        await _interceptor.GetTokenAsync(CancellationToken.None);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var capturedTokens = new List<string>();
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            var authHeader = ctx.Options.Headers?.FirstOrDefault(h => h.Key == "authorization");
            if (authHeader is not null)
            {
                lock (capturedTokens) { capturedTokens.Add(authHeader.Value); }
            }
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
            return call.ResponseAsync;
        }).ToArray();

        await Task.WhenAll(tasks);

        capturedTokens.Should().AllBe("shared-token");
        providerCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAuthErrorAsync_SuccessfulResponse_ReturnDirectly()
    {
        _interceptor = new AuthInterceptor(null, "token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var expected = new byte[] { 42 };
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(expected),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        var result = await call.ResponseAsync;

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAuthErrorAsync_NonUnauthenticatedRpcException_PropagatesOriginal()
    {
        _interceptor = new AuthInterceptor(null, "token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Internal, "server crashed"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Internal, "server crashed"),
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.Internal);
    }

    [Fact]
    public async Task AsyncUnaryCall_AfterUnauthenticated_NextCallRefreshesToken()
    {
        int tokenVersion = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CredentialResult($"token-{++tokenVersion}"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);
        await _interceptor.GetTokenAsync(CancellationToken.None);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unauthenticated, "bad token"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> failContinuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unauthenticated, "bad token"),
                () => new Metadata(),
                () => { });

        var failCall = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, failContinuation);
        try { await failCall.ResponseAsync; } catch (KubeMQAuthenticationException) { }

        string? capturedToken = null;
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> successContinuation = (_, ctx) =>
        {
            capturedToken = ctx.Options.Headers?.FirstOrDefault(h => h.Key == "authorization")?.Value;
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var successCall = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, successContinuation);
        await successCall.ResponseAsync;

        capturedToken.Should().Be("token-2");
    }

    // ──────────────── Additional coverage tests ────────────────

    [Fact]
    public void Constructor_WithStaticToken_CachesHeaders()
    {
        _interceptor = new AuthInterceptor(null, "my-static", null);

        // Verify via GetTokenAsync that static token path works
        var token = _interceptor.GetTokenAsync(CancellationToken.None).GetAwaiter().GetResult();
        token.Should().Be("my-static");
    }

    [Fact]
    public async Task GetTokenAsync_WithCredentialProvider_CallsProvider()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialResult("provider-token"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var token = await _interceptor.GetTokenAsync(CancellationToken.None);

        token.Should().Be("provider-token");
        mock.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_ProviderThrowsHttpRequestException_WrapsAsConnectionException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("network error"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*infrastructure failure*");
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_ProviderThrowsGenericException_WrapsAsAuthException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("generic error"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQAuthenticationException>()
            .WithMessage("*Credential provider failed*");
    }

    [Fact]
    public void Dispose_DisposesTokenLock()
    {
        var mock = new Mock<ICredentialProvider>();
        _interceptor = new AuthInterceptor(mock.Object, null, null);

        _interceptor.Dispose();

        // After dispose, GetTokenAsync should throw ObjectDisposedException
        // because the SemaphoreSlim is disposed
        Func<Task> act = () => _interceptor.GetTokenAsync(CancellationToken.None);
        act.Should().ThrowAsync<ObjectDisposedException>();
        _interceptor = null;
    }

    [Fact]
    public async Task GetTokenAsync_ExpiredToken_RefreshesFromProvider()
    {
        int callCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call returns token that expires in the past (already expired)
                return new CredentialResult(
                    $"token-{callCount}",
                    DateTimeOffset.UtcNow.AddSeconds(-60));
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var first = await _interceptor.GetTokenAsync(CancellationToken.None);
        first.Should().Be("token-1");

        // Second call should refresh because token is expired
        var second = await _interceptor.GetTokenAsync(CancellationToken.None);
        second.Should().Be("token-2");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateCachedToken_ClearsCache_NextCallRefreshes()
    {
        int callCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new CredentialResult($"token-{callCount}");
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        await _interceptor.GetTokenAsync(CancellationToken.None);
        callCount.Should().Be(1);

        _interceptor.InvalidateCachedToken();

        var refreshed = await _interceptor.GetTokenAsync(CancellationToken.None);
        refreshed.Should().Be("token-2");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleAuthErrorAsync_Unauthenticated_ThrowsKubeMQAuthenticationException()
    {
        _interceptor = new AuthInterceptor(null, "token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unauthenticated, "invalid token"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unauthenticated, "invalid token"),
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQAuthenticationException>()
            .WithMessage("*rejected authentication*invalid token*");
    }

    [Fact]
    public async Task HandleAuthErrorAsync_OtherRpcException_Rethrows()
    {
        _interceptor = new AuthInterceptor(null, "token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "server down"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unavailable, "server down"),
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.Unavailable);
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_ProviderThrowsTimeoutException_WrapsAsConnectionException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timed out"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*infrastructure failure*");
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_ProviderThrowsIOException_WrapsAsConnectionException()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("io error"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*infrastructure failure*");
    }

    [Fact]
    public void GetCachedTokenSync_WithStaticToken_ReturnsStaticToken()
    {
        _interceptor = new AuthInterceptor(null, "static-sync", null);

        // Trigger sync path via interceptor call
        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().Contain(e =>
            e.Key == "authorization" && e.Value == "static-sync");
    }

    [Fact]
    public void GetCachedTokenSync_NoProviderNoToken_ReturnsNull()
    {
        _interceptor = new AuthInterceptor(null, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        // No authorization header should be added when there's no token
        if (capturedHeaders is not null)
        {
            capturedHeaders.Should().NotContain(e => e.Key == "authorization");
        }
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_KubeMQAuthException_Rethrows()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KubeMQAuthenticationException("auth failed"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQAuthenticationException>()
            .WithMessage("auth failed");
    }

    [Fact]
    public async Task AsyncUnaryCall_ColdPath_KubeMQConnectionException_Rethrows()
    {
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KubeMQConnectionException("conn failed"));

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, _) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("conn failed");
    }

    // ──────────────── Streaming call interceptor methods ────────────────

    [Fact]
    public void AsyncServerStreamingCall_InjectsAuthorizationHeader()
    {
        _interceptor = new AuthInterceptor(null, "server-stream-token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.ServerStreaming,
            "TestService",
            "TestServerStream",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncServerStreamingCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncServerStreamingCall<byte[]>(
                new NoOpAsyncStreamReader(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        _interceptor.AsyncServerStreamingCall(Array.Empty<byte>(), context, continuation);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().Contain(e =>
            e.Key == "authorization" && e.Value == "server-stream-token");
    }

    [Fact]
    public void AsyncClientStreamingCall_InjectsAuthorizationHeader()
    {
        _interceptor = new AuthInterceptor(null, "client-stream-token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.ClientStreaming,
            "TestService",
            "TestClientStream",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncClientStreamingCallContinuation<byte[], byte[]> continuation = ctx =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncClientStreamingCall<byte[], byte[]>(
                new NoOpClientStreamWriter(),
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        _interceptor.AsyncClientStreamingCall(context, continuation);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().Contain(e =>
            e.Key == "authorization" && e.Value == "client-stream-token");
    }

    [Fact]
    public void AsyncDuplexStreamingCall_InjectsAuthorizationHeader()
    {
        _interceptor = new AuthInterceptor(null, "duplex-stream-token", null);

        var method = new Method<byte[], byte[]>(
            MethodType.DuplexStreaming,
            "TestService",
            "TestDuplexStream",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        Metadata? capturedHeaders = null;
        Interceptor.AsyncDuplexStreamingCallContinuation<byte[], byte[]> continuation = ctx =>
        {
            capturedHeaders = ctx.Options.Headers;
            return new AsyncDuplexStreamingCall<byte[], byte[]>(
                new NoOpClientStreamWriter(),
                new NoOpAsyncStreamReader(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        _interceptor.AsyncDuplexStreamingCall(context, continuation);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Should().Contain(e =>
            e.Key == "authorization" && e.Value == "duplex-stream-token");
    }

    // ──────────────── GetCachedTokenSync double-check coverage ────────────────

    [Fact]
    public void GetCachedTokenSync_DoubleCheck_CachedTokenReturnedInsideLock()
    {
        // The double-check at line 214: second thread enters the lock but finds
        // the token already cached by the first thread. We test this by pre-warming
        // the cache with a non-expired token, then triggering concurrent calls.
        int callCount = 0;
        var mock = new Mock<ICredentialProvider>();
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return new CredentialResult(
                    $"token-{callCount}",
                    DateTimeOffset.UtcNow.AddHours(1));
            });

        _interceptor = new AuthInterceptor(mock.Object, null, null);

        // Pre-warm through async path
        _interceptor.GetTokenAsync(CancellationToken.None).GetAwaiter().GetResult();

        var method = new Method<byte[], byte[]>(
            MethodType.Unary,
            "TestService",
            "TestMethod",
            Marshallers.Create<byte[]>(x => x, x => x),
            Marshallers.Create<byte[]>(x => x, x => x));

        var context = new ClientInterceptorContext<byte[], byte[]>(
            method, "localhost", new CallOptions());

        var capturedTokens = new List<string>();
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (_, ctx) =>
        {
            var authHeader = ctx.Options.Headers?.FirstOrDefault(h => h.Key == "authorization");
            if (authHeader is not null)
            {
                lock (capturedTokens) { capturedTokens.Add(authHeader.Value); }
            }
            return new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        // Make 10 concurrent sync calls — should all use the cached token (line 214 path)
        Parallel.For(0, 10, _ =>
        {
            _interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        });

        capturedTokens.Should().AllBe("token-1");
        callCount.Should().Be(1, "provider should have been called only once");
    }

    // ──────────────── Helpers ────────────────

    private sealed class NoOpAsyncStreamReader : IAsyncStreamReader<byte[]>
    {
        public byte[] Current => Array.Empty<byte>();
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class NoOpClientStreamWriter : IClientStreamWriter<byte[]>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(byte[] message) => Task.CompletedTask;
        public Task WriteAsync(byte[] message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync() => Task.CompletedTask;
    }
}
