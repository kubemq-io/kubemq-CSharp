using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueryResponseTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("response-body");
        var tags = new Dictionary<string, string> { ["key"] = "value" };
        var timestamp = DateTimeOffset.UtcNow;

        var response = new QueryResponse
        {
            RequestId = "req-001",
            Executed = true,
            Body = body,
            Tags = tags,
            Timestamp = timestamp,
            Error = null,
            CacheHit = true,
        };

        response.RequestId.Should().Be("req-001");
        response.Executed.Should().BeTrue();
        response.Body.ToArray().Should().BeEquivalentTo(body);
        response.Tags!["key"].Should().Be("value");
        response.Timestamp.Should().Be(timestamp);
        response.Error.Should().BeNull();
        response.CacheHit.Should().BeTrue();
    }

    [Fact]
    public void Body_Property_SetsAndGetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("query-result");

        var response = new QueryResponse
        {
            RequestId = "req-body",
            Body = body,
        };

        response.Body.ToArray().Should().BeEquivalentTo(body);
    }

    [Fact]
    public void Tags_Property_SetsAndGetsCorrectly()
    {
        var tags = new Dictionary<string, string>
        {
            ["source"] = "cache",
            ["version"] = "2",
        };

        var response = new QueryResponse
        {
            RequestId = "req-tags",
            Tags = tags,
        };

        response.Tags.Should().HaveCount(2);
        response.Tags!["source"].Should().Be("cache");
    }

    [Fact]
    public void CacheHit_WhenTrue_IndicatesCachedResponse()
    {
        var response = new QueryResponse
        {
            RequestId = "req-cache",
            CacheHit = true,
        };

        response.CacheHit.Should().BeTrue();
    }

    [Fact]
    public void CacheHit_DefaultIsFalse()
    {
        var response = new QueryResponse { RequestId = "req-default" };

        response.CacheHit.Should().BeFalse();
    }

    [Fact]
    public void Executed_Property_WhenTrue_IndicatesSuccess()
    {
        var response = new QueryResponse
        {
            RequestId = "req-exec",
            Executed = true,
        };

        response.Executed.Should().BeTrue();
    }

    [Fact]
    public void Executed_Property_WhenFalse_IndicatesFailure()
    {
        var response = new QueryResponse
        {
            RequestId = "req-fail",
            Executed = false,
            Error = "not found",
        };

        response.Executed.Should().BeFalse();
    }

    [Fact]
    public void Error_Property_ReturnsErrorMessage()
    {
        var response = new QueryResponse
        {
            RequestId = "req-err",
            Executed = false,
            Error = "service unavailable",
        };

        response.Error.Should().Be("service unavailable");
    }

    [Fact]
    public void DefaultValues_ForOptionalProperties()
    {
        var response = new QueryResponse { RequestId = "req-def" };

        response.Executed.Should().BeFalse();
        response.Body.Length.Should().Be(0);
        response.Tags.Should().BeNull();
        response.Error.Should().BeNull();
        response.CacheHit.Should().BeFalse();
        response.Timestamp.Should().Be(default);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var resp1 = new QueryResponse
        {
            RequestId = "req-eq",
            Executed = true,
            Timestamp = timestamp,
            CacheHit = false,
        };

        var resp2 = new QueryResponse
        {
            RequestId = "req-eq",
            Executed = true,
            Timestamp = timestamp,
            CacheHit = false,
        };

        resp1.Should().Be(resp2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var resp1 = new QueryResponse { RequestId = "req-1" };
        var resp2 = new QueryResponse { RequestId = "req-2" };

        resp1.Should().NotBe(resp2);
    }
}
