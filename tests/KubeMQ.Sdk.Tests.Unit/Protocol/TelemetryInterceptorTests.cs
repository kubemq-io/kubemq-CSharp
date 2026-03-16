using FluentAssertions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.Sdk.Internal.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public sealed class TelemetryInterceptorTests
{
    private static readonly Method<byte[], byte[]> TestMethod = new(
        MethodType.Unary,
        "TestService",
        "TestMethod",
        Marshallers.Create<byte[]>(x => x, x => x),
        Marshallers.Create<byte[]>(x => x, x => x));

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var act = () => new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullClientId_DoesNotThrow()
    {
        var act = () => new TelemetryInterceptor(null, "localhost", 50000, NullLogger.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public void Instance_IsGrpcInterceptor()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);

        interceptor.Should().BeAssignableTo<Interceptor>();
    }

    [Fact]
    public async Task AsyncUnaryCall_SuccessfulContinuation_ReturnsResponse()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var expectedResponse = new byte[] { 1, 2, 3 };
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(expectedResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        var result = await call.ResponseAsync;

        result.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task AsyncUnaryCall_ContinuationThrowsRpcException_PropagatesException()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Internal, "server error"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Internal, "server error"),
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.Internal);
    }

    [Fact]
    public async Task AsyncUnaryCall_ResponseHeadersAsync_IsPassedThrough()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var headers = new Metadata { { "test-header", "test-value" } };
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(headers),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
        var responseHeaders = await call.ResponseHeadersAsync;

        responseHeaders.Should().BeSameAs(headers);
    }

    [Fact]
    public void AsyncUnaryCall_GetStatus_IsPassedThrough()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        call.GetStatus().Should().Be(Status.DefaultSuccess);
    }

    [Fact]
    public async Task AsyncUnaryCall_MultipleSuccessfulCalls_AllComplete()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        for (int i = 0; i < 3; i++)
        {
            var response = new byte[] { (byte)i };
            Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
                new AsyncUnaryCall<byte[]>(
                    Task.FromResult(response),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });

            var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);
            var result = await call.ResponseAsync;
            result.Should().BeSameAs(response);
        }
    }

    [Fact]
    public async Task AsyncUnaryCall_UnauthenticatedRpcException_PropagatesWithCorrectStatusCode()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unauthenticated, "no auth"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unauthenticated, "no auth"),
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.Unauthenticated);
    }
}
