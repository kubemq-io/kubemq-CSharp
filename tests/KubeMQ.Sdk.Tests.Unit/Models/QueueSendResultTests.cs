using FluentAssertions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueueSendResultTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var sentAt = DateTimeOffset.UtcNow;

        var result = new QueueSendResult
        {
            MessageId = "msg-123",
            SentAt = sentAt,
            IsError = false,
            Error = null,
            DelayedTo = 1000,
            ExpiresAt = 2000,
        };

        result.MessageId.Should().Be("msg-123");
        result.SentAt.Should().Be(sentAt);
        result.IsError.Should().BeFalse();
        result.Error.Should().BeNull();
        result.DelayedTo.Should().Be(1000);
        result.ExpiresAt.Should().Be(2000);
    }

    [Fact]
    public void IsError_WhenTrue_IndicatesError()
    {
        var result = new QueueSendResult
        {
            MessageId = "msg-err",
            IsError = true,
            Error = "queue full",
        };

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Error_Property_ReturnsErrorMessage()
    {
        var result = new QueueSendResult
        {
            MessageId = "msg-err-2",
            IsError = true,
            Error = "permission denied",
        };

        result.Error.Should().Be("permission denied");
    }

    [Fact]
    public void MessageId_Property_ReturnsAssignedId()
    {
        var result = new QueueSendResult
        {
            MessageId = "unique-id-456",
            IsError = false,
        };

        result.MessageId.Should().Be("unique-id-456");
    }

    [Fact]
    public void DefaultValues_ForOptionalProperties()
    {
        var result = new QueueSendResult
        {
            MessageId = "msg-def",
        };

        result.IsError.Should().BeFalse();
        result.Error.Should().BeNull();
        result.DelayedTo.Should().BeNull();
        result.ExpiresAt.Should().BeNull();
        result.SentAt.Should().Be(default);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var sentAt = DateTimeOffset.UtcNow;

        var result1 = new QueueSendResult
        {
            MessageId = "msg-eq",
            SentAt = sentAt,
            IsError = false,
        };

        var result2 = new QueueSendResult
        {
            MessageId = "msg-eq",
            SentAt = sentAt,
            IsError = false,
        };

        result1.Should().Be(result2);
    }
}
