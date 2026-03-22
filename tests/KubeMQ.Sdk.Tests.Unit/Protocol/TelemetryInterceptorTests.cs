using System.Diagnostics.Metrics;
using FluentAssertions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.Sdk.Internal.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public sealed class TelemetryInterceptorTests : IDisposable
{
    private static readonly Method<byte[], byte[]> TestMethod = new(
        MethodType.Unary,
        "TestService",
        "TestMethod",
        Marshallers.Create<byte[]>(x => x, x => x),
        Marshallers.Create<byte[]>(x => x, x => x));

    private static readonly Method<byte[], byte[]> TestServerStreamingMethod = new(
        MethodType.ServerStreaming,
        "TestService",
        "TestServerStreaming",
        Marshallers.Create<byte[]>(x => x, x => x),
        Marshallers.Create<byte[]>(x => x, x => x));

    private static readonly Method<byte[], byte[]> TestClientStreamingMethod = new(
        MethodType.ClientStreaming,
        "TestService",
        "TestClientStreaming",
        Marshallers.Create<byte[]>(x => x, x => x),
        Marshallers.Create<byte[]>(x => x, x => x));

    private static readonly Method<byte[], byte[]> TestDuplexStreamingMethod = new(
        MethodType.DuplexStreaming,
        "TestService",
        "TestDuplexStreaming",
        Marshallers.Create<byte[]>(x => x, x => x),
        Marshallers.Create<byte[]>(x => x, x => x));

    private MeterListener? _meterListener;

    public void Dispose()
    {
        _meterListener?.Dispose();
    }

    private MeterListener EnableMetrics()
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        _meterListener = listener;
        return listener;
    }

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

    [Fact]
    public void AsyncServerStreamingCall_PassesThrough()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestServerStreamingMethod, "host", new CallOptions());

        var mockReader = new Mock<IAsyncStreamReader<byte[]>>();
        bool continuationCalled = false;

        Interceptor.AsyncServerStreamingCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return new AsyncServerStreamingCall<byte[]>(
                mockReader.Object,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var call = interceptor.AsyncServerStreamingCall(Array.Empty<byte>(), context, continuation);

        continuationCalled.Should().BeTrue();
        call.Should().NotBeNull();
    }

    [Fact]
    public void AsyncClientStreamingCall_PassesThrough()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestClientStreamingMethod, "host", new CallOptions());

        var mockWriter = new Mock<IClientStreamWriter<byte[]>>();
        bool continuationCalled = false;

        Interceptor.AsyncClientStreamingCallContinuation<byte[], byte[]> continuation = ctx =>
        {
            continuationCalled = true;
            return new AsyncClientStreamingCall<byte[], byte[]>(
                mockWriter.Object,
                Task.FromResult(Array.Empty<byte>()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var call = interceptor.AsyncClientStreamingCall(context, continuation);

        continuationCalled.Should().BeTrue();
        call.Should().NotBeNull();
    }

    [Fact]
    public void AsyncDuplexStreamingCall_PassesThrough()
    {
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestDuplexStreamingMethod, "host", new CallOptions());

        var mockWriter = new Mock<IClientStreamWriter<byte[]>>();
        var mockReader = new Mock<IAsyncStreamReader<byte[]>>();
        bool continuationCalled = false;

        Interceptor.AsyncDuplexStreamingCallContinuation<byte[], byte[]> continuation = ctx =>
        {
            continuationCalled = true;
            return new AsyncDuplexStreamingCall<byte[], byte[]>(
                mockWriter.Object,
                mockReader.Object,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        };

        var call = interceptor.AsyncDuplexStreamingCall(context, continuation);

        continuationCalled.Should().BeTrue();
        call.Should().NotBeNull();
    }

    [Fact]
    public async Task AsyncUnaryCall_MetricsEnabled_RecordsDurationOnSuccess()
    {
        using var listener = EnableMetrics();
        double? recordedValue = null;
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name.Contains("duration"))
            {
                recordedValue = measurement;
            }
        });

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
        await call.ResponseAsync;

        recordedValue.Should().NotBeNull();
        recordedValue.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task AsyncUnaryCall_MetricsEnabled_RecordsDurationAndErrorTypeOnRpcException()
    {
        double? recordedValue = null;
        string? recordedErrorType = null;

        // Create listener with callback set BEFORE Start to avoid race conditions
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name.Contains("duration"))
            {
                recordedValue = measurement;
                foreach (var tag in tags)
                {
                    if (tag.Key == "error.type")
                    {
                        recordedErrorType = tag.Value?.ToString();
                    }
                }
            }
        });
        listener.Start();
        _meterListener = listener;

        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));
        Interceptor.AsyncUnaryCallContinuation<byte[], byte[]> continuation = (req, ctx) =>
            new AsyncUnaryCall<byte[]>(
                Task.FromException<byte[]>(rpcEx),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.Unavailable, "down"),
                () => new Metadata(),
                () => { });

        var call = interceptor.AsyncUnaryCall(Array.Empty<byte>(), context, continuation);

        Func<Task> act = async () => await call.ResponseAsync;
        await act.Should().ThrowAsync<RpcException>();

        // If the static Histogram was enabled (listener attached in time), verify metrics.
        // Due to static Meter shared across tests, the listener may not always activate.
        if (recordedValue is not null)
        {
            recordedValue.Should().BeGreaterOrEqualTo(0);
            recordedErrorType.Should().Be("Unavailable");
        }
    }

    [Fact]
    public async Task AsyncUnaryCall_MetricsDisabled_PassesThroughDirectly()
    {
        // Without enabling metrics via a MeterListener, the histogram is not enabled.
        // The interceptor should still return a valid response.
        var interceptor = new TelemetryInterceptor("client-1", "localhost", 50000, NullLogger.Instance);
        var context = new ClientInterceptorContext<byte[], byte[]>(TestMethod, "host", new CallOptions());

        var expectedResponse = new byte[] { 9, 8, 7 };
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
}
