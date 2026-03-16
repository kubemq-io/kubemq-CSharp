using FluentAssertions;
using KubeMQ.Sdk.Internal.Protocol;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public sealed class OperationDefaultsTests
{
    [Fact]
    public void SendPublishTimeout_IsPositive()
    {
        OperationDefaults.SendPublishTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void SubscribeInitialTimeout_IsPositive()
    {
        OperationDefaults.SubscribeInitialTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void RpcTimeout_IsPositive()
    {
        OperationDefaults.RpcTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void QueueReceiveSingleTimeout_IsPositive()
    {
        OperationDefaults.QueueReceiveSingleTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void QueueReceiveStreamTimeout_IsPositive()
    {
        OperationDefaults.QueueReceiveStreamTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void SendPublishTimeout_HasExpectedValue()
    {
        OperationDefaults.SendPublishTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SubscribeInitialTimeout_HasExpectedValue()
    {
        OperationDefaults.SubscribeInitialTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void RpcTimeout_HasExpectedValue()
    {
        OperationDefaults.RpcTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void QueueReceiveSingleTimeout_HasExpectedValue()
    {
        OperationDefaults.QueueReceiveSingleTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void QueueReceiveStreamTimeout_HasExpectedValue()
    {
        OperationDefaults.QueueReceiveStreamTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }
}
