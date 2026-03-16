using System.Diagnostics;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Telemetry;

namespace KubeMQ.Sdk.Tests.Unit.Telemetry;

public sealed class TextMapCarrierTests : IDisposable
{
    private readonly ActivitySource _source = new("TextMapCarrierTests");
    private readonly ActivityListener _listener;

    public TextMapCarrierTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "TextMapCarrierTests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void InjectContext_WithActiveActivity_AddsTraceparentToTags()
    {
        using var activity = _source.StartActivity("test-op", ActivityKind.Producer);
        activity.Should().NotBeNull();

        var tags = TextMapCarrier.InjectContext(null, activity);

        tags.Should().ContainKey("traceparent");
        tags["traceparent"].Should().StartWith("00-");
    }

    [Fact]
    public void InjectContext_WithNoActivity_ReturnsEmptyTags()
    {
        var tags = TextMapCarrier.InjectContext(null, activity: null);

        tags.Should().NotBeNull();
        tags.Should().NotContainKey("traceparent");
    }

    [Fact]
    public void InjectContext_PreservesExistingTags()
    {
        using var activity = _source.StartActivity("test-op", ActivityKind.Producer);
        var existing = new Dictionary<string, string> { { "custom-key", "custom-value" } };

        var tags = TextMapCarrier.InjectContext(existing, activity);

        tags.Should().ContainKey("custom-key");
        tags["custom-key"].Should().Be("custom-value");
        tags.Should().ContainKey("traceparent");
    }

    [Fact]
    public void InjectContext_WithTraceState_AddsTracestateToTags()
    {
        using var activity = _source.StartActivity("test-op", ActivityKind.Producer);
        activity.Should().NotBeNull();
        activity!.TraceStateString = "vendor1=value1";

        var tags = TextMapCarrier.InjectContext(null, activity);

        tags.Should().ContainKey("tracestate");
        tags["tracestate"].Should().Be("vendor1=value1");
    }

    [Fact]
    public void ExtractContext_WithNullTags_ReturnsDefault()
    {
        var ctx = TextMapCarrier.ExtractContext(null);

        ctx.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractContext_WithNoTraceparent_ReturnsDefault()
    {
        var tags = new Dictionary<string, string> { { "other", "value" } };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractContext_WithEmptyTraceparent_ReturnsDefault()
    {
        var tags = new Dictionary<string, string> { { "traceparent", "" } };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractContext_WithValidTraceparent_ReturnsContext()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        string traceparent = $"00-{traceId}-{spanId}-01";

        var tags = new Dictionary<string, string> { { "traceparent", traceparent } };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.TraceId.Should().Be(traceId);
        ctx.SpanId.Should().Be(spanId);
        ctx.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
        ctx.IsRemote.Should().BeTrue();
    }

    [Fact]
    public void ExtractContext_WithUnrecordedFlag_ReturnsNoneFlag()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        string traceparent = $"00-{traceId}-{spanId}-00";

        var tags = new Dictionary<string, string> { { "traceparent", traceparent } };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.TraceFlags.Should().Be(ActivityTraceFlags.None);
    }

    [Fact]
    public void ExtractContext_WithTracestate_PreservesTracestate()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        string traceparent = $"00-{traceId}-{spanId}-01";

        var tags = new Dictionary<string, string>
        {
            { "traceparent", traceparent },
            { "tracestate", "vendor1=value1" },
        };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.TraceState.Should().Be("vendor1=value1");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("00")]
    [InlineData("00-short-short-00")]
    public void ExtractContext_WithMalformedTraceparent_ReturnsDefault(string malformed)
    {
        var tags = new Dictionary<string, string> { { "traceparent", malformed } };

        var ctx = TextMapCarrier.ExtractContext(tags);

        ctx.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void RoundTrip_InjectThenExtract_PreservesTraceInfo()
    {
        using var activity = _source.StartActivity("roundtrip", ActivityKind.Producer);
        activity.Should().NotBeNull();

        var injected = TextMapCarrier.InjectContext(null, activity);
        var extracted = TextMapCarrier.ExtractContext(injected);

        extracted.TraceId.Should().Be(activity!.TraceId);
        extracted.SpanId.Should().Be(activity.SpanId);
    }

    [Fact]
    public void InjectContext_WithNullExistingTags_CreatesNewDictionary()
    {
        using var activity = _source.StartActivity("test-op", ActivityKind.Producer);

        var tags = TextMapCarrier.InjectContext(null, activity);

        tags.Should().NotBeNull();
        tags.Should().BeOfType<Dictionary<string, string>>();
    }
}
