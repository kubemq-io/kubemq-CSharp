using FluentAssertions;
using KubeMQ.Sdk.Commands;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class CommandResponseTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var response = new CommandResponse
        {
            RequestId = "req-001",
            Executed = true,
            Timestamp = timestamp,
            Error = null,
        };

        response.RequestId.Should().Be("req-001");
        response.Executed.Should().BeTrue();
        response.Timestamp.Should().Be(timestamp);
        response.Error.Should().BeNull();
    }

    [Fact]
    public void Executed_Property_WhenTrue_IndicatesSuccess()
    {
        var response = new CommandResponse
        {
            RequestId = "req-ok",
            Executed = true,
        };

        response.Executed.Should().BeTrue();
    }

    [Fact]
    public void Executed_Property_WhenFalse_IndicatesFailure()
    {
        var response = new CommandResponse
        {
            RequestId = "req-fail",
            Executed = false,
            Error = "timeout",
        };

        response.Executed.Should().BeFalse();
    }

    [Fact]
    public void Error_Property_ReturnsErrorMessage()
    {
        var response = new CommandResponse
        {
            RequestId = "req-err",
            Executed = false,
            Error = "handler not found",
        };

        response.Error.Should().Be("handler not found");
    }

    [Fact]
    public void DefaultValues_ForOptionalProperties()
    {
        var response = new CommandResponse
        {
            RequestId = "req-def",
        };

        response.Executed.Should().BeFalse();
        response.Error.Should().BeNull();
        response.Timestamp.Should().Be(default);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var resp1 = new CommandResponse
        {
            RequestId = "req-eq",
            Executed = true,
            Timestamp = timestamp,
        };

        var resp2 = new CommandResponse
        {
            RequestId = "req-eq",
            Executed = true,
            Timestamp = timestamp,
        };

        resp1.Should().Be(resp2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var resp1 = new CommandResponse { RequestId = "req-1", Executed = true };
        var resp2 = new CommandResponse { RequestId = "req-2", Executed = false };

        resp1.Should().NotBe(resp2);
    }
}
