using FluentAssertions;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Tests.Unit.Exceptions;

public class ExceptionHierarchyTests
{
    [Fact]
    public void KubeMQException_ParameterlessCtor_SetsDefaults()
    {
        var ex = new KubeMQException();

        ex.ErrorCode.Should().Be(KubeMQErrorCode.Unknown);
        ex.Category.Should().Be(KubeMQErrorCategory.Fatal);
        ex.IsRetryable.Should().BeFalse();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void KubeMQException_MessageCtor_SetsMessage()
    {
        var ex = new KubeMQException("something broke");

        ex.Message.Should().Be("something broke");
        ex.ErrorCode.Should().Be(KubeMQErrorCode.Unknown);
    }

    [Fact]
    public void KubeMQException_InnerExceptionCtor_ChainsInner()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new KubeMQException("wrapped", inner);

        ex.Message.Should().Be("wrapped");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void KubeMQException_FullCtor_SetsAllProperties()
    {
        var inner = new Exception("inner");
        var ex = new KubeMQException(
            "msg",
            KubeMQErrorCode.Unavailable,
            KubeMQErrorCategory.Transient,
            isRetryable: true,
            innerException: inner);

        ex.ErrorCode.Should().Be(KubeMQErrorCode.Unavailable);
        ex.Category.Should().Be(KubeMQErrorCategory.Transient);
        ex.IsRetryable.Should().BeTrue();
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void KubeMQException_ContextCtor_SetsOperationAndChannel()
    {
        var ex = new KubeMQException(
            "msg",
            KubeMQErrorCode.Internal,
            KubeMQErrorCategory.Fatal,
            isRetryable: false,
            requestId: "req-123",
            operation: "PublishEvent",
            channel: "test-ch");

        ex.RequestId.Should().Be("req-123");
        ex.Operation.Should().Be("PublishEvent");
        ex.Channel.Should().Be("test-ch");
    }

    [Fact]
    public void KubeMQException_Timestamp_IsRecentUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var ex = new KubeMQException("test");
        var after = DateTimeOffset.UtcNow;

        ex.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void KubeMQConnectionException_Defaults_AreTransientRetryable()
    {
        var ex = new KubeMQConnectionException("conn fail");

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.ConnectionRefused);
        ex.Category.Should().Be(KubeMQErrorCategory.Transient);
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void KubeMQAuthenticationException_Defaults_AreNotRetryable()
    {
        var ex = new KubeMQAuthenticationException("bad token");

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.AuthenticationFailed);
        ex.Category.Should().Be(KubeMQErrorCategory.Authentication);
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void KubeMQTimeoutException_Defaults_AreRetryable()
    {
        var ex = new KubeMQTimeoutException("deadline exceeded");

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.DeadlineExceeded);
        ex.Category.Should().Be(KubeMQErrorCategory.Timeout);
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void KubeMQConfigurationException_Defaults_AreNotRetryable()
    {
        var ex = new KubeMQConfigurationException("bad config");

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.ConfigurationInvalid);
        ex.Category.Should().Be(KubeMQErrorCategory.Validation);
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void KubeMQOperationException_Defaults_AreNotRetryable()
    {
        var ex = new KubeMQOperationException("op fail");

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.Internal);
        ex.Category.Should().Be(KubeMQErrorCategory.Fatal);
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void KubeMQRetryExhaustedException_FullCtor_SetsAttemptCountAndDuration()
    {
        var inner = new KubeMQConnectionException("last attempt");
        var ex = new KubeMQRetryExhaustedException(
            "exhausted",
            attemptCount: 4,
            totalDuration: TimeSpan.FromSeconds(12.5),
            lastException: inner);

        ex.Should().BeAssignableTo<KubeMQException>();
        ex.AttemptCount.Should().Be(4);
        ex.TotalDuration.Should().Be(TimeSpan.FromSeconds(12.5));
        ex.LastException.Should().BeSameAs(inner);
        ex.InnerException.Should().BeSameAs(inner);
        ex.ErrorCode.Should().Be(KubeMQErrorCode.RetryExhausted);
    }

    [Fact]
    public void KubeMQConnectionException_WithInner_PreservesChain()
    {
        var rpcInner = new InvalidOperationException("rpc error");
        var ex = new KubeMQConnectionException("connect failed", rpcInner);

        ex.InnerException.Should().BeSameAs(rpcInner);
    }

    [Fact]
    public void AllExceptionTypes_DeriveFromKubeMQException()
    {
        new KubeMQConnectionException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQAuthenticationException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQTimeoutException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQConfigurationException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQOperationException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQRetryExhaustedException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQBufferFullException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQStreamBrokenException().Should().BeAssignableTo<KubeMQException>();
        new KubeMQPartialFailureException().Should().BeAssignableTo<KubeMQException>();
    }

    // --- KubeMQBufferFullException ---

    [Fact]
    public void KubeMQBufferFullException_ParameterlessCtor_SetsDefaults()
    {
        var ex = new KubeMQBufferFullException();

        ex.ErrorCode.Should().Be(KubeMQErrorCode.BufferFull);
        ex.Category.Should().Be(KubeMQErrorCategory.Backpressure);
        ex.IsRetryable.Should().BeFalse();
        ex.Message.Should().Be("Reconnect buffer full");
    }

    [Fact]
    public void KubeMQBufferFullException_MessageCtor_SetsMessage()
    {
        var ex = new KubeMQBufferFullException("buffer is full");

        ex.Message.Should().Be("buffer is full");
        ex.ErrorCode.Should().Be(KubeMQErrorCode.BufferFull);
    }

    [Fact]
    public void KubeMQBufferFullException_InnerExceptionCtor_ChainsInner()
    {
        var inner = new InvalidOperationException("io error");
        var ex = new KubeMQBufferFullException("full", inner);

        ex.InnerException.Should().BeSameAs(inner);
        ex.Message.Should().Be("full");
    }

    [Fact]
    public void KubeMQBufferFullException_BufferSizeBytes_CanBeSet()
    {
        var ex = new KubeMQBufferFullException("full")
        {
            BufferSizeBytes = 1024 * 1024,
        };

        ex.BufferSizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void KubeMQBufferFullException_BufferCapacityBytes_CanBeSet()
    {
        var ex = new KubeMQBufferFullException("full")
        {
            BufferCapacityBytes = 10 * 1024 * 1024,
        };

        ex.BufferCapacityBytes.Should().Be(10 * 1024 * 1024);
    }

    // --- KubeMQStreamBrokenException ---

    [Fact]
    public void KubeMQStreamBrokenException_ParameterlessCtor_SetsDefaults()
    {
        var ex = new KubeMQStreamBrokenException();

        ex.ErrorCode.Should().Be(KubeMQErrorCode.StreamBroken);
        ex.Category.Should().Be(KubeMQErrorCategory.Transient);
        ex.IsRetryable.Should().BeTrue();
        ex.Message.Should().Be("Stream broken");
        ex.UnackedMessageIds.Should().BeEmpty();
    }

    [Fact]
    public void KubeMQStreamBrokenException_MessageCtor_SetsMessage()
    {
        var ex = new KubeMQStreamBrokenException("stream died");

        ex.Message.Should().Be("stream died");
        ex.UnackedMessageIds.Should().BeEmpty();
    }

    [Fact]
    public void KubeMQStreamBrokenException_InnerExceptionCtor_ChainsInner()
    {
        var inner = new Exception("transport error");
        var ex = new KubeMQStreamBrokenException("broken", inner);

        ex.InnerException.Should().BeSameAs(inner);
        ex.UnackedMessageIds.Should().BeEmpty();
    }

    [Fact]
    public void KubeMQStreamBrokenException_UnackedIds_SetsMessageIds()
    {
        var ids = new List<string> { "msg-1", "msg-2", "msg-3" };
        var ex = new KubeMQStreamBrokenException("broken", ids);

        ex.UnackedMessageIds.Should().BeEquivalentTo(ids);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void KubeMQStreamBrokenException_UnackedIds_WithInner_SetsAll()
    {
        var ids = new List<string> { "msg-1" };
        var inner = new Exception("root");
        var ex = new KubeMQStreamBrokenException("broken", ids, inner);

        ex.UnackedMessageIds.Should().HaveCount(1);
        ex.InnerException.Should().BeSameAs(inner);
    }

    // --- KubeMQPartialFailureException ---

    [Fact]
    public void KubeMQPartialFailureException_ParameterlessCtor_SetsDefaults()
    {
        var ex = new KubeMQPartialFailureException();

        ex.Should().BeAssignableTo<KubeMQOperationException>();
        ex.ErrorCode.Should().Be(KubeMQErrorCode.Internal);
        ex.Category.Should().Be(KubeMQErrorCategory.Fatal);
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void KubeMQPartialFailureException_MessageCtor_SetsMessage()
    {
        var ex = new KubeMQPartialFailureException("partial fail");

        ex.Message.Should().Be("partial fail");
    }

    [Fact]
    public void KubeMQPartialFailureException_InnerExceptionCtor_ChainsInner()
    {
        var inner = new Exception("batch item error");
        var ex = new KubeMQPartialFailureException("partial", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void KubeMQPartialFailureException_DerivesFromOperationException()
    {
        var ex = new KubeMQPartialFailureException("test");

        ex.Should().BeAssignableTo<KubeMQOperationException>();
        ex.Should().BeAssignableTo<KubeMQException>();
    }

    // --- KubeMQRetryExhaustedException with null lastException ---

    [Fact]
    public void KubeMQRetryExhaustedException_MessageOnlyCtor_SetsDefaults()
    {
        var ex = new KubeMQRetryExhaustedException("all retries failed");

        ex.Message.Should().Be("all retries failed");
        ex.AttemptCount.Should().Be(0);
        ex.TotalDuration.Should().Be(TimeSpan.Zero);
        ex.LastException.Should().BeNull();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void KubeMQRetryExhaustedException_ParameterlessCtor_SetsDefaults()
    {
        var ex = new KubeMQRetryExhaustedException();

        ex.Message.Should().Be("All retry attempts exhausted");
        ex.ErrorCode.Should().Be(KubeMQErrorCode.RetryExhausted);
        ex.AttemptCount.Should().Be(0);
        ex.LastException.Should().BeNull();
    }

    [Fact]
    public void KubeMQRetryExhaustedException_InnerExceptionCtor_ChainsInner()
    {
        var inner = new Exception("connection lost");
        var ex = new KubeMQRetryExhaustedException("retries exhausted", inner);

        ex.InnerException.Should().BeSameAs(inner);
        ex.LastException.Should().BeSameAs(inner);
    }

    // --- ServerAddress and GrpcStatusCode on all exception types ---

    [Fact]
    public void KubeMQException_ServerAddress_CanBeSet()
    {
        var ex = new KubeMQException("test")
        {
            ServerAddress = "kubemq-server:50000",
        };

        ex.ServerAddress.Should().Be("kubemq-server:50000");
    }

    [Fact]
    public void KubeMQException_GrpcStatusCode_CanBeSet()
    {
        var ex = new KubeMQException("test")
        {
            GrpcStatusCode = 14,
        };

        ex.GrpcStatusCode.Should().Be(14);
    }

    [Fact]
    public void KubeMQConnectionException_ServerAddress_CanBeSet()
    {
        var ex = new KubeMQConnectionException("fail")
        {
            ServerAddress = "host:50000",
        };

        ex.ServerAddress.Should().Be("host:50000");
    }

    [Fact]
    public void KubeMQOperationException_GrpcStatusCode_CanBeSet()
    {
        var ex = new KubeMQOperationException("fail")
        {
            GrpcStatusCode = 2,
        };

        ex.GrpcStatusCode.Should().Be(2);
    }

    [Fact]
    public void KubeMQTimeoutException_ServerAddressAndGrpcStatusCode_CanBeSet()
    {
        var ex = new KubeMQTimeoutException("timeout")
        {
            ServerAddress = "server:50000",
            GrpcStatusCode = 4,
        };

        ex.ServerAddress.Should().Be("server:50000");
        ex.GrpcStatusCode.Should().Be(4);
    }

    [Fact]
    public void KubeMQBufferFullException_ServerAddress_CanBeSet()
    {
        var ex = new KubeMQBufferFullException("full")
        {
            ServerAddress = "node:50000",
        };

        ex.ServerAddress.Should().Be("node:50000");
    }

    [Fact]
    public void KubeMQStreamBrokenException_GrpcStatusCode_CanBeSet()
    {
        var ex = new KubeMQStreamBrokenException("broken")
        {
            GrpcStatusCode = 14,
        };

        ex.GrpcStatusCode.Should().Be(14);
    }

    [Fact]
    public void KubeMQException_Operation_CanBeSetInternally()
    {
        var ex = new KubeMQException("test")
        {
            Operation = "PublishEvent",
            Channel = "test-channel",
        };

        ex.Operation.Should().Be("PublishEvent");
        ex.Channel.Should().Be("test-channel");
    }

    [Fact]
    public void KubeMQException_IsRetryable_CanBeSetInternally()
    {
        var ex = new KubeMQException("test")
        {
            IsRetryable = true,
        };

        ex.IsRetryable.Should().BeTrue();
    }
}
