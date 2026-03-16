using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Telemetry;

namespace KubeMQ.Sdk.Tests.Unit.Telemetry;

public class TelemetryTests : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly List<Activity> _collectedActivities = new();

    public TelemetryTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "KubeMQ.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _collectedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        foreach (var activity in _collectedActivities)
        {
            activity.Dispose();
        }
    }

    [Fact]
    public void StartProducerActivity_CreatesActivityWithAttributes()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish",
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Producer);
        activity.GetTagItem(SemanticConventions.MessagingSystem)
            .Should().Be(SemanticConventions.MessagingSystemValue);
        activity.GetTagItem(SemanticConventions.MessagingOperationName)
            .Should().Be("publish");
        activity.GetTagItem(SemanticConventions.MessagingDestinationName)
            .Should().Be("test-channel");
        activity.GetTagItem(SemanticConventions.MessagingClientId)
            .Should().Be("client-1");
        activity.GetTagItem(SemanticConventions.ServerAddress)
            .Should().Be("localhost");
        activity.GetTagItem(SemanticConventions.ServerPort)
            .Should().Be(50000);
    }

    [Fact]
    public void StartConsumerActivity_WithLinkedContext_CreatesLink()
    {
        var parentTraceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var linkedContext = new ActivityContext(
            parentTraceId, parentSpanId, ActivityTraceFlags.Recorded);

        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive",
            "test-channel",
            "client-1",
            "localhost",
            50000,
            linkedContext);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Consumer);
        activity.Links.Should().ContainSingle();
        activity.Links.First().Context.TraceId.Should().Be(parentTraceId);
        activity.Links.First().Context.SpanId.Should().Be(parentSpanId);
    }

    [Fact]
    public void StartClientActivity_SetsCorrectKind()
    {
        using var activity = KubeMQActivitySource.StartClientActivity(
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client);
        activity.GetTagItem(SemanticConventions.MessagingOperationName)
            .Should().Be(SemanticConventions.OperationSend);
    }

    [Fact]
    public void SetError_SetsErrorStatus()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish",
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();

        var exception = new InvalidOperationException("something went wrong");
        KubeMQActivitySource.SetError(activity, exception);

        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("something went wrong");
        activity.GetTagItem(SemanticConventions.ErrorType)
            .Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void RecordMessageSent_IncrementsCounter()
    {
        long sentCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricSentMessages)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref sentCount, measurement);
        });
        listener.Start();

        long baseline = Interlocked.Read(ref sentCount);

        KubeMQMetrics.RecordMessageSent("publish", "metrics-test-channel");

        long delta = Interlocked.Read(ref sentCount) - baseline;
        delta.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void RecordOperationDuration_RecordsHistogram()
    {
        double recordedDuration = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricOperationDuration)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) =>
        {
            recordedDuration = measurement;
        });
        listener.Start();

        KubeMQMetrics.RecordOperationDuration(1.5, "publish", "duration-test-channel");

        recordedDuration.Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void RecordRetryAttempt_IncrementsRetryCounter()
    {
        long retryCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricRetryAttempts)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref retryCount, measurement);
        });
        listener.Start();

        long baseline = Interlocked.Read(ref retryCount);

        KubeMQMetrics.RecordRetryAttempt("publish", "transient");

        long delta = Interlocked.Read(ref retryCount) - baseline;
        delta.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void ShouldIncludeChannel_RespectsCardinalityThreshold()
    {
        string prefix = $"card-{Guid.NewGuid():N}-";
        KubeMQMetrics.ConfigureCardinality(threshold: 1000);
        for (int i = 0; i < 1000; i++)
        {
            KubeMQMetrics.ShouldIncludeChannel($"{prefix}{i}");
        }

        KubeMQMetrics.ShouldIncludeChannel($"{prefix}overflow").Should().BeFalse();

        KubeMQMetrics.ConfigureCardinality(threshold: 100_000);
    }

    [Fact]
    public void RecordRetryExhausted_IncrementsCounter()
    {
        long exhaustedCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricRetryExhausted)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref exhaustedCount, measurement);
        });
        listener.Start();

        long baseline = Interlocked.Read(ref exhaustedCount);

        KubeMQMetrics.RecordRetryExhausted("publish", "transient");

        long delta = Interlocked.Read(ref exhaustedCount) - baseline;
        delta.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void ShouldIncludeChannel_WithEmptyString_ReturnsTrue()
    {
        KubeMQMetrics.ConfigureCardinality(threshold: 100_000);

        KubeMQMetrics.ShouldIncludeChannel(string.Empty).Should().BeTrue();
    }
}
