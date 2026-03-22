using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Google.Protobuf;
using KubeMQ.Sdk.Internal.Protocol;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public class SpanContextSerializerTests
{
    [Fact]
    public void Serialize_NullActivity_ReturnsByteStringEmpty()
    {
        var result = SpanContextSerializer.Serialize(null);

        result.Should().BeSameAs(ByteString.Empty);
    }

    [Fact]
    public void Serialize_ActivityWithNullId_ReturnsByteStringEmpty()
    {
        // An Activity that has never been started has a null Id
        var activity = new Activity("test-op");

        activity.Id.Should().BeNull();

        var result = SpanContextSerializer.Serialize(activity);

        result.Should().BeSameAs(ByteString.Empty);
    }

    [Fact]
    public void Serialize_ValidActivity_ReturnsTraceparentAndTracestate()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("test-source");
        using var activity = source.StartActivity("test-op");
        activity.Should().NotBeNull();
        activity!.TraceStateString = "key=value";

        var result = SpanContextSerializer.Serialize(activity);

        result.IsEmpty.Should().BeFalse();
        var payload = result.ToString(Encoding.UTF8);
        payload.Should().Contain("\n");

        var parts = payload.Split('\n', 2);
        parts[0].Should().Be(activity.Id);
        parts[1].Should().Be("key=value");
    }

    [Fact]
    public void Deserialize_Null_ReturnsNullTuple()
    {
        var (traceParent, traceState) = SpanContextSerializer.Deserialize(null);

        traceParent.Should().BeNull();
        traceState.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyByteString_ReturnsNullTuple()
    {
        var (traceParent, traceState) = SpanContextSerializer.Deserialize(ByteString.Empty);

        traceParent.Should().BeNull();
        traceState.Should().BeNull();
    }

    [Fact]
    public void Deserialize_PayloadWithoutNewline_ReturnsTraceparentOnly()
    {
        var bytes = ByteString.CopyFrom("00-abc123-def456-01", Encoding.UTF8);

        var (traceParent, traceState) = SpanContextSerializer.Deserialize(bytes);

        traceParent.Should().Be("00-abc123-def456-01");
        traceState.Should().BeNull();
    }

    [Fact]
    public void Deserialize_PayloadWithNewline_ReturnsBothParts()
    {
        var bytes = ByteString.CopyFrom("00-abc123-def456-01\nkey=value", Encoding.UTF8);

        var (traceParent, traceState) = SpanContextSerializer.Deserialize(bytes);

        traceParent.Should().Be("00-abc123-def456-01");
        traceState.Should().Be("key=value");
    }

    [Fact]
    public void Deserialize_PayloadWithEmptyTracestate_ReturnsTraceparentAndNullState()
    {
        var bytes = ByteString.CopyFrom("00-abc123-def456-01\n", Encoding.UTF8);

        var (traceParent, traceState) = SpanContextSerializer.Deserialize(bytes);

        traceParent.Should().Be("00-abc123-def456-01");
        traceState.Should().BeNull();
    }

    [Fact]
    public void Roundtrip_SerializeThenDeserialize_PreservesData()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("roundtrip-source");
        using var activity = source.StartActivity("roundtrip-op");
        activity.Should().NotBeNull();
        activity!.TraceStateString = "rk=rv";

        var serialized = SpanContextSerializer.Serialize(activity);
        var (traceParent, traceState) = SpanContextSerializer.Deserialize(serialized);

        traceParent.Should().Be(activity.Id);
        traceState.Should().Be("rk=rv");
    }
}
