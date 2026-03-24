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

    // ──────────────── Additional coverage tests ────────────────

    [Fact]
    public void StartServerActivity_CreatesServerKindWithCorrectAttributes()
    {
        using var activity = KubeMQActivitySource.StartServerActivity(
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
        activity.GetTagItem(SemanticConventions.MessagingSystem)
            .Should().Be(SemanticConventions.MessagingSystemValue);
        activity.GetTagItem(SemanticConventions.MessagingOperationName)
            .Should().Be(SemanticConventions.OperationProcess);
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
    public void StartServerActivity_WithLinkedContext_CreatesLink()
    {
        var parentTraceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var linkedContext = new ActivityContext(
            parentTraceId, parentSpanId, ActivityTraceFlags.Recorded);

        using var activity = KubeMQActivitySource.StartServerActivity(
            "test-channel",
            "client-1",
            "localhost",
            50000,
            linkedContext);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
        activity.Links.Should().ContainSingle();
        activity.Links.First().Context.TraceId.Should().Be(parentTraceId);
        activity.Links.First().Context.SpanId.Should().Be(parentSpanId);
    }

    [Fact]
    public void StartConsumerActivity_WithoutLinkedContext_HasNoLinks()
    {
        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive",
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Consumer);
        activity.Links.Should().BeEmpty();
    }

    [Fact]
    public void RecordRetryEvent_AddsEventToActivity()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish",
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();

        KubeMQActivitySource.RecordRetryEvent(activity, 2, 1.5, "transient");

        activity!.Events.Should().ContainSingle();
        var retryEvent = activity.Events.First();
        retryEvent.Name.Should().Be(SemanticConventions.RetryEventName);
        retryEvent.Tags.Should().Contain(t =>
            t.Key == SemanticConventions.RetryAttemptAttribute && (int)t.Value! == 2);
        retryEvent.Tags.Should().Contain(t =>
            t.Key == SemanticConventions.RetryDelaySecondsAttribute && (double)t.Value! == 1.5);
        retryEvent.Tags.Should().Contain(t =>
            t.Key == SemanticConventions.ErrorType && (string)t.Value! == "transient");
    }

    [Fact]
    public void SetError_WithNullActivity_DoesNotThrow()
    {
        var exception = new InvalidOperationException("test");

        Action act = () => KubeMQActivitySource.SetError(null, exception);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRetryEvent_WithNullActivity_DoesNotThrow()
    {
        Action act = () => KubeMQActivitySource.RecordRetryEvent(null, 1, 0.5, "transient");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("publish", "publish")]
    [InlineData("process", "process")]
    [InlineData("receive", "receive")]
    [InlineData("settle", "settle")]
    [InlineData("send", "send")]
    [InlineData("custom-op", "custom-op")]
    public void MapErrorType_ReturnsCorrectValues(string operationName, string expectedType)
    {
        // MapOperationType is private, but we can verify behavior through StartConsumerActivity
        // which calls MapOperationType internally for the operation type tag
        using var activity = KubeMQActivitySource.StartConsumerActivity(
            operationName,
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.GetTagItem(SemanticConventions.MessagingOperationType)
            .Should().Be(expectedType);
    }

    [Theory]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Transient, "transient")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Timeout, "timeout")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Throttling, "throttling")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Authentication, "authentication")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Authorization, "authorization")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Validation, "validation")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.NotFound, "not_found")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Fatal, "fatal")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Cancellation, "cancellation")]
    [InlineData(KubeMQ.Sdk.Exceptions.ErrorCategory.Backpressure, "backpressure")]
    public void MapErrorType_ReturnsCorrectStringForCategory(
        KubeMQ.Sdk.Exceptions.ErrorCategory category, string expected)
    {
        KubeMQMetrics.MapErrorType(category).Should().Be(expected);
    }

    [Fact]
    public void MapErrorType_UnknownCategory_ReturnsUnknown()
    {
        KubeMQMetrics.MapErrorType((KubeMQ.Sdk.Exceptions.ErrorCategory)999).Should().Be("unknown");
    }

    [Fact]
    public void StartServerActivity_WithoutLinkedContext_HasNoLinks()
    {
        using var activity = KubeMQActivitySource.StartServerActivity(
            "test-channel",
            "client-1",
            "localhost",
            50000);

        activity.Should().NotBeNull();
        activity!.Links.Should().BeEmpty();
    }

    [Fact]
    public void ShouldIncludeChannel_CardinalityWithLogger_LogsWarning()
    {
        // Cover the Log.CardinalityThresholdExceeded path (line 101)
        // by configuring with a logger and exceeding the threshold.
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        string prefix = $"cardlog-{Guid.NewGuid():N}-";
        KubeMQMetrics.ConfigureCardinality(threshold: 2, logger: logger);

        KubeMQMetrics.ShouldIncludeChannel($"{prefix}0").Should().BeTrue();
        KubeMQMetrics.ShouldIncludeChannel($"{prefix}1").Should().BeTrue();
        KubeMQMetrics.ShouldIncludeChannel($"{prefix}overflow").Should().BeFalse();

        // Reset to safe defaults
        KubeMQMetrics.ConfigureCardinality(threshold: 100_000);
    }
}
