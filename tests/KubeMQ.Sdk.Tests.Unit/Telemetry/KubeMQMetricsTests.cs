using System.Diagnostics.Metrics;
using FluentAssertions;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Telemetry;

namespace KubeMQ.Sdk.Tests.Unit.Telemetry;

public class KubeMQMetricsTests
{
    [Fact]
    public void RecordMessageSent_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordMessageSent("publish", "metrics-ch-sent");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordMessageSent_WithCount_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordMessageSent("publish", "metrics-ch-sent-count", count: 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordMessageConsumed_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordMessageConsumed("receive", "metrics-ch-consumed");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordMessageConsumed_IncrementsCounter()
    {
        long consumed = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricConsumedMessages)
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, m, _, _) =>
            Interlocked.Add(ref consumed, m));
        listener.Start();

        long baseline = Interlocked.Read(ref consumed);

        KubeMQMetrics.RecordMessageConsumed("receive", "metrics-ch-consumed-counter");

        long delta = Interlocked.Read(ref consumed) - baseline;
        delta.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void RecordOperationDuration_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordOperationDuration(0.5, "publish", "metrics-ch-dur");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordOperationDuration_WithErrorType_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordOperationDuration(1.0, "publish", "metrics-ch-dur-err", "transient");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRetryAttempt_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordRetryAttempt("publish", "transient");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRetryExhausted_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordRetryExhausted("publish", "transient");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(KubeMQErrorCategory.Transient, "transient")]
    [InlineData(KubeMQErrorCategory.Timeout, "timeout")]
    [InlineData(KubeMQErrorCategory.Throttling, "throttling")]
    [InlineData(KubeMQErrorCategory.Authentication, "authentication")]
    [InlineData(KubeMQErrorCategory.Authorization, "authorization")]
    [InlineData(KubeMQErrorCategory.Validation, "validation")]
    [InlineData(KubeMQErrorCategory.NotFound, "not_found")]
    [InlineData(KubeMQErrorCategory.Fatal, "fatal")]
    [InlineData(KubeMQErrorCategory.Cancellation, "cancellation")]
    [InlineData(KubeMQErrorCategory.Backpressure, "backpressure")]
    public void MapErrorType_ReturnsExpectedString(KubeMQErrorCategory category, string expected)
    {
        KubeMQMetrics.MapErrorType(category).Should().Be(expected);
    }

    [Fact]
    public void MapErrorType_UnknownCategory_ReturnsUnknown()
    {
        KubeMQMetrics.MapErrorType((KubeMQErrorCategory)999).Should().Be("unknown");
    }

    [Fact]
    public void RecordMessageSent_WithEmptyChannel_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordMessageSent("publish", string.Empty);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordOperationDuration_WithEmptyOperation_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordOperationDuration(0.1, string.Empty, "metrics-ch-empty-op");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordMessageConsumed_WithCount_DoesNotThrow()
    {
        var act = () => KubeMQMetrics.RecordMessageConsumed("receive", "metrics-ch-consumed-multi", count: 10);

        act.Should().NotThrow();
    }

    [Fact]
    public void ShouldIncludeChannel_AllowlistedChannel_AlwaysReturnsTrue()
    {
        string unique = $"allowlist-{Guid.NewGuid():N}";
        KubeMQMetrics.ConfigureCardinality(threshold: 1, channelAllowlist: new[] { unique });

        KubeMQMetrics.ShouldIncludeChannel(unique).Should().BeTrue();

        KubeMQMetrics.ConfigureCardinality(threshold: 100_000);
    }

    [Fact]
    public void ShouldIncludeChannel_KnownChannel_ReturnsTrueOnSubsequentCalls()
    {
        string unique = $"known-{Guid.NewGuid():N}";
        KubeMQMetrics.ConfigureCardinality(threshold: 100_000);

        KubeMQMetrics.ShouldIncludeChannel(unique).Should().BeTrue();
        KubeMQMetrics.ShouldIncludeChannel(unique).Should().BeTrue();
    }

    [Fact]
    public void RecordOperationDuration_RecordsCorrectValue()
    {
        double recorded = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricOperationDuration)
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, m, _, _) => recorded = m);
        listener.Start();

        KubeMQMetrics.RecordOperationDuration(2.75, "send", "metrics-dur-verify");

        recorded.Should().BeApproximately(2.75, 0.001);
    }

    [Fact]
    public void RecordMessageSent_IncrementsCorrectAmount()
    {
        long sent = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricSentMessages)
            {
                ml.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, m, _, _) =>
            Interlocked.Add(ref sent, m));
        listener.Start();

        long baseline = Interlocked.Read(ref sent);

        KubeMQMetrics.RecordMessageSent("publish", "metrics-batch-ch", count: 3);

        long delta = Interlocked.Read(ref sent) - baseline;
        delta.Should().Be(3);
    }
}
