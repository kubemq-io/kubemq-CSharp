using FluentAssertions;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;

namespace KubeMQ.Sdk.Tests.Unit.ErrorClassification;

public class GrpcErrorMapperTests
{
    [Theory]
    [InlineData(StatusCode.Unknown, true)]
    [InlineData(StatusCode.DeadlineExceeded, true)]
    [InlineData(StatusCode.ResourceExhausted, true)]
    [InlineData(StatusCode.Aborted, true)]
    [InlineData(StatusCode.Unavailable, true)]
    [InlineData(StatusCode.InvalidArgument, false)]
    [InlineData(StatusCode.NotFound, false)]
    [InlineData(StatusCode.AlreadyExists, false)]
    [InlineData(StatusCode.PermissionDenied, false)]
    [InlineData(StatusCode.FailedPrecondition, false)]
    [InlineData(StatusCode.OutOfRange, false)]
    [InlineData(StatusCode.Unimplemented, false)]
    [InlineData(StatusCode.Internal, false)]
    [InlineData(StatusCode.DataLoss, false)]
    [InlineData(StatusCode.Unauthenticated, false)]
    public void MapException_ReturnsCorrectIsRetryable(
        StatusCode grpcCode, bool expectedRetryable)
    {
        var rpcException = new RpcException(new Status(grpcCode, "test"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", "test-ch", CancellationToken.None);

        result.IsRetryable.Should().Be(expectedRetryable);
    }

    [Fact]
    public void MapException_Unavailable_ReturnsKubeMQConnectionException()
    {
        var rpcException = new RpcException(new Status(StatusCode.Unavailable, "server down"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", "test-ch", CancellationToken.None);

        result.Should().BeOfType<KubeMQConnectionException>();
        result.ErrorCode.Should().Be(KubeMQErrorCode.Unavailable);
        result.Category.Should().Be(KubeMQErrorCategory.Transient);
        result.InnerException.Should().BeSameAs(rpcException);
    }

    [Fact]
    public void MapException_Unauthenticated_ReturnsKubeMQAuthenticationException()
    {
        var rpcException = new RpcException(new Status(StatusCode.Unauthenticated, "bad token"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None);

        result.Should().BeOfType<KubeMQAuthenticationException>();
        result.ErrorCode.Should().Be(KubeMQErrorCode.AuthenticationFailed);
        result.Category.Should().Be(KubeMQErrorCategory.Authentication);
        result.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void MapException_DeadlineExceeded_ReturnsKubeMQTimeoutException()
    {
        var rpcException = new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", "ch", CancellationToken.None);

        result.Should().BeOfType<KubeMQTimeoutException>();
        result.ErrorCode.Should().Be(KubeMQErrorCode.DeadlineExceeded);
        result.Category.Should().Be(KubeMQErrorCategory.Timeout);
    }

    [Fact]
    public void MapException_PreservesInnerException()
    {
        var rpcException = new RpcException(new Status(StatusCode.Internal, "fail"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None);

        result.InnerException.Should().BeSameAs(rpcException);
    }

    [Fact]
    public void MapException_SetsOperationAndChannel()
    {
        var rpcException = new RpcException(new Status(StatusCode.Internal, "fail"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "PublishEvent", "my-channel", CancellationToken.None);

        result.Operation.Should().Be("PublishEvent");
        result.Channel.Should().Be("my-channel");
    }

    [Fact]
    public void MapException_SetsServerAddress()
    {
        var rpcException = new RpcException(new Status(StatusCode.Unavailable, "down"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None, "localhost:50000");

        result.ServerAddress.Should().Be("localhost:50000");
    }

    [Fact]
    public void MapException_SetsGrpcStatusCode()
    {
        var rpcException = new RpcException(new Status(StatusCode.NotFound, "gone"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None);

        result.GrpcStatusCode.Should().Be((int)StatusCode.NotFound);
    }

    [Fact]
    public void MapException_Cancelled_ClientInitiated_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var rpcException = new RpcException(new Status(StatusCode.Cancelled, "cancelled"));

        var act = () => GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void MapException_Cancelled_ServerInitiated_ReturnsTransientRetryable()
    {
        var rpcException = new RpcException(new Status(StatusCode.Cancelled, "server cancelled"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None);

        result.Category.Should().Be(KubeMQErrorCategory.Transient);
        result.IsRetryable.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusCode.InvalidArgument, KubeMQErrorCategory.Validation)]
    [InlineData(StatusCode.AlreadyExists, KubeMQErrorCategory.Validation)]
    [InlineData(StatusCode.FailedPrecondition, KubeMQErrorCategory.Validation)]
    [InlineData(StatusCode.OutOfRange, KubeMQErrorCategory.Validation)]
    [InlineData(StatusCode.NotFound, KubeMQErrorCategory.NotFound)]
    [InlineData(StatusCode.PermissionDenied, KubeMQErrorCategory.Authorization)]
    [InlineData(StatusCode.Unauthenticated, KubeMQErrorCategory.Authentication)]
    [InlineData(StatusCode.DeadlineExceeded, KubeMQErrorCategory.Timeout)]
    [InlineData(StatusCode.Unavailable, KubeMQErrorCategory.Transient)]
    [InlineData(StatusCode.Aborted, KubeMQErrorCategory.Transient)]
    [InlineData(StatusCode.ResourceExhausted, KubeMQErrorCategory.Throttling)]
    [InlineData(StatusCode.Unimplemented, KubeMQErrorCategory.Fatal)]
    [InlineData(StatusCode.Internal, KubeMQErrorCategory.Fatal)]
    [InlineData(StatusCode.DataLoss, KubeMQErrorCategory.Fatal)]
    public void MapException_ReturnsCorrectCategory(
        StatusCode grpcCode, KubeMQErrorCategory expectedCategory)
    {
        var rpcException = new RpcException(new Status(grpcCode, "test"));

        var result = GrpcErrorMapper.MapException(
            rpcException, "TestOp", null, CancellationToken.None);

        result.Category.Should().Be(expectedCategory);
    }
}
