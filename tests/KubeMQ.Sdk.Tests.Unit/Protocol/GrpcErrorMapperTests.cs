using System.Reflection;
using System.Security.Authentication;
using FluentAssertions;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public class GrpcErrorMapperTests
{
    private const string Operation = "PublishEvent";
    private const string Channel = "test-channel";
    private const string ServerAddr = "localhost:50000";

    [Fact]
    public void Cancelled_WithCallerTokenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var rpcEx = new RpcException(new Status(StatusCode.Cancelled, "cancelled"));

        var act = () => GrpcErrorMapper.MapException(rpcEx, Operation, Channel, cts.Token, ServerAddr);

        act.Should().Throw<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Cancelled_WithoutCallerCancellation_ReturnsTransientRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Cancelled, "server cancelled"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Transient);
        mapped.IsRetryable.Should().BeTrue();
        mapped.ErrorCode.Should().Be(ErrorCode.Cancelled);
        mapped.Operation.Should().Be(Operation);
        mapped.Channel.Should().Be(Channel);
        mapped.ServerAddress.Should().Be(ServerAddr);
        mapped.GrpcStatusCode.Should().Be((int)StatusCode.Cancelled);
    }

    [Fact]
    public void InvalidArgument_ReturnsValidationNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.InvalidArgument, "bad field"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Validation);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.InvalidArgument);
    }

    [Fact]
    public void DeadlineExceeded_ReturnsTimeoutException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQTimeoutException>();
        mapped.Category.Should().Be(ErrorCategory.Timeout);
        mapped.IsRetryable.Should().BeTrue();
        mapped.ErrorCode.Should().Be(ErrorCode.DeadlineExceeded);
    }

    [Fact]
    public void NotFound_ReturnsNotFoundNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "channel not found"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.NotFound);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public void PermissionDenied_ReturnsAuthorizationNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.PermissionDenied, "no access"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Authorization);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.PermissionDenied);
    }

    [Fact]
    public void ResourceExhausted_ReturnsThrottlingRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.ResourceExhausted, "rate limited"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Throttling);
        mapped.IsRetryable.Should().BeTrue();
        mapped.ErrorCode.Should().Be(ErrorCode.ResourceExhausted);
    }

    [Fact]
    public void Unauthenticated_ReturnsAuthenticationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unauthenticated, "bad token"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Category.Should().Be(ErrorCategory.Authentication);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.AuthenticationFailed);
    }

    [Fact]
    public void Unavailable_ReturnsConnectionException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "server down"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConnectionException>();
        mapped.Category.Should().Be(ErrorCategory.Transient);
        mapped.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void Unavailable_WithSslInDetail_ReturnsConnectionExceptionForTls()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "SSL handshake failed"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConnectionException>();
        mapped.Message.Should().Contain("TLS handshake failed");
    }

    [Fact]
    public void Unavailable_WithExpiredCertInDetail_ReturnsAuthenticationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "certificate expired"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Message.Should().Contain("TLS certificate validation failed");
    }

    [Fact]
    public void Unavailable_WithUntrustedCertInDetail_ReturnsAuthenticationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "certificate untrusted root"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Message.Should().Contain("TLS certificate validation failed");
    }

    [Fact]
    public void Unavailable_WithHostnameMismatchInDetail_ReturnsAuthenticationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "TLS hostname mismatch"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Message.Should().Contain("TLS certificate validation failed");
    }

    [Fact]
    public void Unavailable_WithProtocolErrorInDetail_ReturnsConfigurationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "TLS protocol mismatch"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConfigurationException>();
        mapped.Message.Should().Contain("TLS version/cipher negotiation failed");
    }

    [Fact]
    public void Unavailable_WithCipherInDetail_ReturnsConfigurationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "SSL cipher suite not supported"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConfigurationException>();
        mapped.Message.Should().Contain("TLS version/cipher negotiation failed");
    }

    [Fact]
    public void Unavailable_WithVersionInDetail_ReturnsConfigurationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "TLS version not supported"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConfigurationException>();
        mapped.Message.Should().Contain("TLS version/cipher negotiation failed");
    }

    [Fact]
    public void Unavailable_WithValidationProcedureInDetail_ReturnsTlsException()
    {
        var rpcEx = new RpcException(
            new Status(StatusCode.Unavailable, "The remote certificate was rejected by the validation procedure"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeAssignableTo<KubeMQException>();
        mapped.Message.Should().Contain("TLS");
    }

    [Fact]
    public void Unavailable_WithAuthExceptionInner_CertValidation_ReturnsAuthException()
    {
        var inner = new AuthenticationException("The remote certificate is invalid");
        var rpcEx = CreateRpcExceptionWithInner(StatusCode.Unavailable, "Connection failed", inner);

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Message.Should().Contain("TLS certificate validation failed");
    }

    [Fact]
    public void Unavailable_WithAuthExceptionInner_ProtocolError_ReturnsConfigException()
    {
        var inner = new AuthenticationException("protocol version not supported");
        var rpcEx = CreateRpcExceptionWithInner(StatusCode.Unavailable, "Connection failed", inner);

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConfigurationException>();
        mapped.Message.Should().Contain("TLS version/cipher negotiation failed");
    }

    [Fact]
    public void Unavailable_WithAuthExceptionInner_CipherError_ReturnsConfigException()
    {
        var inner = new AuthenticationException("cipher negotiation failed");
        var rpcEx = CreateRpcExceptionWithInner(StatusCode.Unavailable, "Connection failed", inner);

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQConfigurationException>();
        mapped.Message.Should().Contain("TLS version/cipher negotiation failed");
    }

    [Fact]
    public void Internal_ReturnsFatalNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Internal, "server error"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Fatal);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.Internal);
    }

    [Fact]
    public void Unknown_ReturnsTransientRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unknown, "unknown"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Transient);
        mapped.IsRetryable.Should().BeTrue();
        mapped.ErrorCode.Should().Be(ErrorCode.Unknown);
    }

    [Fact]
    public void OK_ThrowsInvalidOperationException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.OK, "ok"));

        var act = () => GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OK*");
    }

    [Fact]
    public void AlreadyExists_ReturnsValidationNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.AlreadyExists, "exists"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Validation);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.AlreadyExists);
    }

    [Fact]
    public void FailedPrecondition_ReturnsValidationNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.FailedPrecondition, "precondition"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Validation);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.FailedPrecondition);
    }

    [Fact]
    public void Aborted_ReturnsTransientRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Aborted, "conflict"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Transient);
        mapped.IsRetryable.Should().BeTrue();
        mapped.ErrorCode.Should().Be(ErrorCode.Aborted);
    }

    [Fact]
    public void OutOfRange_ReturnsValidationNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.OutOfRange, "out of range"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Validation);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.OutOfRange);
    }

    [Fact]
    public void Unimplemented_ReturnsFatalNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unimplemented, "not implemented"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Fatal);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.Unimplemented);
    }

    [Fact]
    public void DataLoss_ReturnsFatalNonRetryable()
    {
        var rpcEx = new RpcException(new Status(StatusCode.DataLoss, "data lost"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQOperationException>();
        mapped.Category.Should().Be(ErrorCategory.Fatal);
        mapped.IsRetryable.Should().BeFalse();
        mapped.ErrorCode.Should().Be(ErrorCode.DataLoss);
    }

    [Fact]
    public void MappedException_PreservesRpcExceptionAsInner()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Internal, "server error"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.InnerException.Should().BeSameAs(rpcEx);
    }

    [Fact]
    public void MappedException_SetsAllContextProperties()
    {
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "not found"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Operation.Should().Be(Operation);
        mapped.Channel.Should().Be(Channel);
        mapped.ServerAddress.Should().Be(ServerAddr);
        mapped.GrpcStatusCode.Should().Be((int)StatusCode.NotFound);
    }

    [Fact]
    public void MappedException_WithNullChannel_FormatsMessageWithoutChannel()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Internal, "error"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, null, CancellationToken.None, ServerAddr);

        mapped.Message.Should().NotContain("on channel");
        mapped.Channel.Should().BeNull();
    }

    [Fact]
    public void MappedException_WithNullServerAddress_FormatsMessageWithoutServer()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Internal, "error"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, null);

        mapped.Message.Should().NotContain("(server:");
        mapped.ServerAddress.Should().BeNull();
    }

    [Fact]
    public void MappedException_WithEmptyDetail_FormatsCleanMessage()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Internal, string.Empty));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Message.Should().Contain(Operation);
        mapped.Message.Should().Contain("Suggestion:");
    }

    [Fact]
    public void MappedException_WithAllContext_FormatsFullMessage()
    {
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "queue missing"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Message.Should().Contain(Operation);
        mapped.Message.Should().Contain(Channel);
        mapped.Message.Should().Contain("queue missing");
        mapped.Message.Should().Contain(ServerAddr);
        mapped.Message.Should().Contain("Suggestion:");
    }

    [Fact]
    public void Unavailable_WithInvalidCertInDetail_ReturnsAuthException()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "SSL certificate invalid for host"));

        var mapped = GrpcErrorMapper.MapException(rpcEx, Operation, Channel, CancellationToken.None, ServerAddr);

        mapped.Should().BeOfType<KubeMQAuthenticationException>();
        mapped.Message.Should().Contain("TLS certificate validation failed");
    }

    private static RpcException CreateRpcExceptionWithInner(
        StatusCode code, string detail, Exception inner)
    {
        var rpcEx = new RpcException(new Status(code, detail));
        typeof(Exception)
            .GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(rpcEx, inner);
        return rpcEx;
    }
}
