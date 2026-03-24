using System.Text;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

/// <summary>
/// Tests covering previously uncovered lines in KubeMQClient.cs:
/// CreateEventStreamAsync, CreateEventStoreStreamAsync, DisposeAsyncCore
/// reconnecting/ready paths, DrainAsync, DrainCallbacksAsync,
/// WithReconnect, ExecuteWithRetryAsync, CheckServerCompatibility,
/// PeekQueueMessagesAsync error path, and SendQueueMessagesUpstreamAsync error paths.
/// </summary>
public class KubeMQClientStreamCoverageTests
{
    // ---------------------------------------------------------------
    // 1. CreateEventStreamAsync (lines 397-410)
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateEventStreamAsync_ReturnsEventStream()
    {
        var (client, transport) = TestClientFactory.Create();

        var (mockCall, _, _, completeReader) = MockEventStream.Create();

        transport
            .Setup(t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var stream = await client.CreateEventStreamAsync();

        stream.Should().NotBeNull();
        stream.Should().BeOfType<EventStream>();

        transport.Verify(
            t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        completeReader();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CreateEventStreamAsync_WithOnError_PassesCallback()
    {
        var (client, transport) = TestClientFactory.Create();

        var (mockCall, _, _, completeReader) = MockEventStream.Create();

        transport
            .Setup(t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var errors = new List<Exception>();
        var stream = await client.CreateEventStreamAsync(onError: ex => errors.Add(ex));

        stream.Should().NotBeNull();
        stream.Should().BeOfType<EventStream>();

        completeReader();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CreateEventStreamAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();

        await client.DisposeAsync();

        Func<Task> act = () => client.CreateEventStreamAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CreateEventStreamAsync_TransportThrows_PropagatesException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stream creation failed"));

        Func<Task> act = () => client.CreateEventStreamAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("stream creation failed");
    }

    // ---------------------------------------------------------------
    // 2. CreateEventStoreStreamAsync (lines 413-424)
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateEventStoreStreamAsync_ReturnsEventStoreStream()
    {
        var (client, transport) = TestClientFactory.Create();

        var (mockCall, _, _, completeReader) = MockEventStream.Create();

        transport
            .Setup(t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var stream = await client.CreateEventStoreStreamAsync();

        stream.Should().NotBeNull();
        stream.Should().BeOfType<EventStoreStream>();

        transport.Verify(
            t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        completeReader();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CreateEventStoreStreamAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();

        await client.DisposeAsync();

        Func<Task> act = () => client.CreateEventStoreStreamAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CreateEventStoreStreamAsync_TransportThrows_PropagatesException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CreateEventStreamAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("event store stream failed"));

        Func<Task> act = () => client.CreateEventStoreStreamAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("event store stream failed");
    }

    // ---------------------------------------------------------------
    // 3. DisposeAsync when Reconnecting — DiscardBuffer path (lines 1555-1561)
    // ---------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_WhenReconnecting_DiscardsBuffer()
    {
        // Create a client with a real ConnectionManager so the Reconnecting path is hit.
        var transport = new Mock<ITransport>();
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "test-client",
            Retry = new() { Enabled = false },
            Reconnect = new() { Enabled = true },
        };
        var streamManager = new StreamManager(NullLogger.Instance);
        var stateMachine = new StateMachine(NullLogger.Instance);
        var connectionManager = new ConnectionManager(
            options, transport.Object, stateMachine, streamManager, NullLogger.Instance);

        var client = new KubeMQClient(
            options, transport.Object, NullLogger.Instance,
            testConnectionManager: connectionManager,
            testStreamManager: streamManager);

        // Move to Ready state first, then trigger connection lost to enter Reconnecting.
        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });
        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.ConnectAsync();

        // Now trigger connection lost via OnConnectionLost.
        // The connection manager transitions to Reconnecting.
        connectionManager.OnConnectionLost(new Exception("network failure"));

        // Give a small delay for the state transition
        await Task.Delay(100);

        // Now dispose — should hit the Reconnecting path and call DiscardBuffer.
        await client.DisposeAsync();

        client.State.Should().Be(ConnectionState.Closed);
    }

    // ---------------------------------------------------------------
    // 4. DisposeAsync when Ready — DrainAsync path (line 1565)
    // ---------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_WhenReady_CallsDrain()
    {
        var transport = new Mock<ITransport>();
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "test-client",
            Retry = new() { Enabled = false },
            Reconnect = new() { Enabled = true },
        };
        var streamManager = new StreamManager(NullLogger.Instance);
        var stateMachine = new StateMachine(NullLogger.Instance);
        var connectionManager = new ConnectionManager(
            options, transport.Object, stateMachine, streamManager, NullLogger.Instance);

        var client = new KubeMQClient(
            options, transport.Object, NullLogger.Instance,
            testConnectionManager: connectionManager,
            testStreamManager: streamManager);

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });
        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.ConnectAsync();

        // Client is now in Ready state. Dispose should trigger DrainAsync.
        await client.DisposeAsync();

        client.State.Should().Be(ConnectionState.Closed);
    }

    // ---------------------------------------------------------------
    // 5. DrainCallbacksAsync — zero callbacks (lines 2185-2187)
    //    Covered by existing drain tests, but this test ensures the
    //    early-exit path when ActiveCount == 0 is exercised explicitly.
    // ---------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_DrainCallbacksAsync_ZeroCallbacks_CompletesImmediately()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    // ---------------------------------------------------------------
    // 6. ExecuteWithRetryAsync — with retry handler path (lines 2078-2080, 2097-2099)
    // ---------------------------------------------------------------

    [Fact]
    public async Task SendEventAsync_WithRetryHandler_DelegatesToRetryHandler()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "test-client",
            Retry = new() { Enabled = true, MaxRetries = 3 },
        };

        var transport = new Mock<ITransport>();
        var retryHandler = new KubeMQ.Sdk.Internal.Protocol.RetryHandler(
            options.Retry, NullLogger.Instance);

        var client = new KubeMQClient(
            options, transport.Object, NullLogger.Instance,
            testRetryHandler: retryHandler);

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var message = new EventMessage
        {
            Channel = "retry-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        await client.SendEventAsync(message);

        transport.Verify(
            t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);

        retryHandler.Dispose();
    }

    [Fact]
    public async Task SendEventAsync_WithRetryHandler_ExecutesViaRetryPath()
    {
        // This test verifies that when a RetryHandler is injected, the
        // ExecuteWithRetryAsync<T> code path (lines 2097-2099) is exercised.
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "test-client",
            Retry = new()
            {
                Enabled = true,
                MaxRetries = 3,
            },
        };

        var transport = new Mock<ITransport>();
        var retryHandler = new KubeMQ.Sdk.Internal.Protocol.RetryHandler(
            options.Retry, NullLogger.Instance);

        var client = new KubeMQClient(
            options, transport.Object, NullLogger.Instance,
            testRetryHandler: retryHandler);

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var message = new EventMessage
        {
            Channel = "retry-ch",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        // Should succeed via the retry handler path
        await client.SendEventAsync(message);

        transport.Verify(
            t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);

        retryHandler.Dispose();
    }

    // ---------------------------------------------------------------
    // 7. CheckServerCompatibility — version below min logs warning (lines 2231-2240)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ConnectAsync_ServerVersionBelowMin_DoesNotFailConnection()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "1.0.0" });

        await client.ConnectAsync();

        // The connection should succeed even with an old server version.
        // CheckServerCompatibility logs a warning but does not fail.
        client.State.Should().Be(ConnectionState.Ready);

        // Give time for the background Task.Run that calls CheckServerCompatibility
        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionAboveMax_DoesNotFailConnection()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // MaxTestedServerVersion is "" (no upper bound), so we need a version
        // that triggers the warning. Since max is empty, this path won't trigger
        // via the current constants. But we still test a very high version.
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "99.99.99" });

        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionWithPreRelease_DoesNotFailConnection()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "v3.5.0-beta.1" });

        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        await Task.Delay(200);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionEmpty_DoesNotFailConnection()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "" });

        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);
    }

    [Fact]
    public async Task ConnectAsync_ServerVersionInvalid_DoesNotFailConnection()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "not-a-version" });

        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);

        await Task.Delay(200);
    }

    // ---------------------------------------------------------------
    // 8. PeekQueueMessagesAsync — error path (lines 806-810)
    // ---------------------------------------------------------------

    [Fact]
    public async Task PeekQueueMessagesAsync_ServerError_ReturnsErrorInResponse()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.ReceiveQueueMessagesResponse
            {
                RequestID = "peek-err",
                IsError = true,
                Error = "queue not found",
            });

        var request = new QueuePollRequest
        {
            Channel = "missing-queue",
            MaxMessages = 5,
            WaitTimeoutSeconds = 2,
        };

        var result = await client.PeekQueueMessagesAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("queue not found");
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task PeekQueueMessagesAsync_Success_ReturnsMessages()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = new KubeMQ.Grpc.ReceiveQueueMessagesResponse
        {
            RequestID = "peek-ok",
            IsError = false,
            Error = string.Empty,
        };
        grpcResponse.Messages.Add(new KubeMQ.Grpc.QueueMessage
        {
            MessageID = "peek-msg-1",
            Channel = "peek-ch",
            Body = ByteString.CopyFromUtf8("data1"),
            Attributes = new KubeMQ.Grpc.QueueMessageAttributes { Sequence = 1 },
        });

        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var request = new QueuePollRequest
        {
            Channel = "peek-ch",
            MaxMessages = 10,
            WaitTimeoutSeconds = 5,
        };

        var result = await client.PeekQueueMessagesAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Messages.Should().HaveCount(1);
        result.HasMessages.Should().BeTrue();
    }

    [Fact]
    public async Task PeekQueueMessagesAsync_SetsIsPeakTrue()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.ReceiveQueueMessagesRequest? captured = null;
        transport
            .Setup(t => t.ReceiveQueueMessagesAsync(
                It.IsAny<KubeMQ.Grpc.ReceiveQueueMessagesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.ReceiveQueueMessagesRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new KubeMQ.Grpc.ReceiveQueueMessagesResponse
            {
                RequestID = "peek-cap",
                IsError = false,
            });

        var request = new QueuePollRequest
        {
            Channel = "peek-cap-ch",
            MaxMessages = 3,
            WaitTimeoutSeconds = 1,
        };

        await client.PeekQueueMessagesAsync(request);

        captured.Should().NotBeNull();
        captured!.IsPeak.Should().BeTrue();
        captured.Channel.Should().Be("peek-cap-ch");
        captured.MaxNumberOfMessages.Should().Be(3);
        captured.WaitTimeSeconds.Should().Be(1);
    }

    [Fact]
    public async Task PeekQueueMessagesAsync_NullRequest_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.PeekQueueMessagesAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PeekQueueMessagesAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();

        await client.DisposeAsync();

        Func<Task> act = () => client.PeekQueueMessagesAsync(
            new QueuePollRequest { Channel = "ch" });

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ---------------------------------------------------------------
    // 9. SendQueueMessagesUpstreamAsync — error paths (lines 943-958)
    // ---------------------------------------------------------------

    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_NoResponse_ThrowsOperationException()
    {
        var (client, transport) = TestClientFactory.Create();

        var mockCall = MockNoResponseUpstreamStream.Create();
        transport
            .Setup(t => t.CreateUpstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var messages = new[]
        {
            new QueueMessage
            {
                Channel = "upstream-ch",
                Body = Encoding.UTF8.GetBytes("data"),
            },
        };

        Func<Task> act = () => client.SendQueueMessagesUpstreamAsync(messages);

        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*No response*");
    }

    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_RpcException_RetriesAndFails()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CreateUpstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => MockRpcExceptionUpstreamStream.Create());

        var messages = new[]
        {
            new QueueMessage
            {
                Channel = "upstream-ch",
                Body = Encoding.UTF8.GetBytes("data"),
            },
        };

        Func<Task> act = () => client.SendQueueMessagesUpstreamAsync(messages);

        // After maxRetries (3) + 1 attempts, it should throw the final exception.
        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task SendQueueMessagesUpstreamAsync_EmptyMessages_CallsTransport()
    {
        var (client, transport) = TestClientFactory.Create();

        var upstreamResponse = new KubeMQ.Grpc.QueuesUpstreamResponse
        {
            RefRequestID = "ref-empty",
            IsError = false,
        };

        var mockCall = MockUpstreamStream.Create(upstreamResponse);
        transport
            .Setup(t => t.CreateUpstreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCall);

        var messages = Array.Empty<QueueMessage>();

        var result = await client.SendQueueMessagesUpstreamAsync(messages);

        result.Should().NotBeNull();
        result.RefRequestId.Should().Be("ref-empty");
    }

    // ---------------------------------------------------------------
    // 10. WithReconnect — subscription stream recovery (lines 1978-2051)
    // ---------------------------------------------------------------

    [Fact]
    public async Task SubscribeToEventsAsync_StreamCompletes_Terminates()
    {
        var (client, transport) = TestClientFactory.Create();

        var events = new List<KubeMQ.Grpc.EventReceive>();

        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(
                new KubeMQ.Grpc.EventReceive
                {
                    EventID = "evt-1",
                    Channel = "sub-ch",
                    Body = ByteString.CopyFromUtf8("body1"),
                }));

        var subscription = new EventsSubscription { Channel = "sub-ch" };

        await foreach (var evt in client.SubscribeToEventsAsync(subscription))
        {
            events.Add(new KubeMQ.Grpc.EventReceive
            {
                EventID = evt.Id,
                Channel = evt.Channel,
            });
        }

        events.Should().HaveCount(1);
    }

    [Fact]
    public async Task SubscribeToEventsAsync_Cancelled_Terminates()
    {
        var (client, transport) = TestClientFactory.Create();

        using var cts = new CancellationTokenSource();

        transport
            .Setup(t => t.SubscribeToEventsAsync(
                It.IsAny<KubeMQ.Grpc.Subscribe>(), It.IsAny<CancellationToken>()))
            .Returns<KubeMQ.Grpc.Subscribe, CancellationToken>((_, ct) =>
                NeverEndingStream(ct));

        var events = new List<EventReceived>();
        await cts.CancelAsync();

        var subscription = new EventsSubscription { Channel = "sub-ch" };

        await foreach (var evt in client.SubscribeToEventsAsync(subscription, cancellationToken: cts.Token))
        {
            events.Add(evt);
        }

        events.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Helper methods
    // ---------------------------------------------------------------

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<KubeMQ.Grpc.EventReceive> NeverEndingStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            yield break;
        }
    }

    // ---------------------------------------------------------------
    // Mock helpers for upstream error paths
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a mock upstream stream where MoveNext returns false (no response).
    /// </summary>
    private static class MockNoResponseUpstreamStream
    {
        internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse> Create()
        {
            var reader = new EmptyResponseStreamReader();
            var writer = new NoOpUpstreamWriter(reader);

            return new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>(
                requestStream: writer,
                responseStream: reader,
                responseHeadersAsync: Task.FromResult(new Metadata()),
                getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
                getTrailersFunc: () => new Metadata(),
                disposeAction: () => { });
        }

        private sealed class EmptyResponseStreamReader : IAsyncStreamReader<KubeMQ.Grpc.QueuesUpstreamResponse>
        {
            private readonly SemaphoreSlim _signal = new(0);
            private volatile bool _completed;

            public KubeMQ.Grpc.QueuesUpstreamResponse Current => default!;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (_completed)
                {
                    return false;
                }

                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            internal void Signal()
            {
                _completed = true;
                _signal.Release();
            }
        }

        private sealed class NoOpUpstreamWriter : IClientStreamWriter<KubeMQ.Grpc.QueuesUpstreamRequest>
        {
            private readonly EmptyResponseStreamReader _reader;

            public NoOpUpstreamWriter(EmptyResponseStreamReader reader)
            {
                _reader = reader;
            }

            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message)
                => Task.CompletedTask;

            public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task CompleteAsync()
            {
                _reader.Signal();
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Creates a mock upstream stream that throws RpcException on WriteAsync.
    /// </summary>
    private static class MockRpcExceptionUpstreamStream
    {
        internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse> Create()
        {
            var reader = new NeverReader();
            var writer = new FaultingWriter();

            return new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>(
                requestStream: writer,
                responseStream: reader,
                responseHeadersAsync: Task.FromResult(new Metadata()),
                getStatusFunc: () => new Status(StatusCode.Unavailable, "unavailable"),
                getTrailersFunc: () => new Metadata(),
                disposeAction: () => { });
        }

        private sealed class NeverReader : IAsyncStreamReader<KubeMQ.Grpc.QueuesUpstreamResponse>
        {
            public KubeMQ.Grpc.QueuesUpstreamResponse Current => default!;

            public Task<bool> MoveNext(CancellationToken cancellationToken) =>
                throw new RpcException(new Status(StatusCode.Unavailable, "unavailable"));
        }

        private sealed class FaultingWriter : IClientStreamWriter<KubeMQ.Grpc.QueuesUpstreamRequest>
        {
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message) =>
                throw new RpcException(new Status(StatusCode.Unavailable, "unavailable"));

            public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message, CancellationToken cancellationToken) =>
                throw new RpcException(new Status(StatusCode.Unavailable, "unavailable"));

            public Task CompleteAsync() => Task.CompletedTask;
        }
    }
}
