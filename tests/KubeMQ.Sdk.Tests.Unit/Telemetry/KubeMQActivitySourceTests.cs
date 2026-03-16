using System.Diagnostics;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Telemetry;

namespace KubeMQ.Sdk.Tests.Unit.Telemetry;

public class KubeMQActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _collected = new();

    public KubeMQActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "KubeMQ.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _collected.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        foreach (var a in _collected)
        {
            a.Dispose();
        }
    }

    [Fact]
    public void StartProducerActivity_SetsAllAttributes()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish", "events-channel", "client-42", "10.0.0.1", 50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Producer);
        activity.DisplayName.Should().Be("publish events-channel");
        activity.GetTagItem(SemanticConventions.MessagingSystem).Should().Be("kubemq");
        activity.GetTagItem(SemanticConventions.MessagingOperationName).Should().Be("publish");
        activity.GetTagItem(SemanticConventions.MessagingOperationType).Should().Be("publish");
        activity.GetTagItem(SemanticConventions.MessagingDestinationName).Should().Be("events-channel");
        activity.GetTagItem(SemanticConventions.MessagingClientId).Should().Be("client-42");
        activity.GetTagItem(SemanticConventions.ServerAddress).Should().Be("10.0.0.1");
        activity.GetTagItem(SemanticConventions.ServerPort).Should().Be(50000);
    }

    [Fact]
    public void StartProducerActivity_WithNullClientId_OmitsClientIdTag()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish", "ch", null, "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.GetTagItem(SemanticConventions.MessagingClientId).Should().BeNull();
    }

    [Fact]
    public void StartConsumerActivity_SetsConsumerKind()
    {
        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive", "queue-channel", "client-1", "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Consumer);
        activity.GetTagItem(SemanticConventions.MessagingOperationName).Should().Be("receive");
    }

    [Fact]
    public void StartConsumerActivity_WithoutLinkedContext_HasNoLinks()
    {
        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive", "ch", "client-1", "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.Links.Should().BeEmpty();
    }

    [Fact]
    public void StartConsumerActivity_WithLinkedContext_CreatesLink()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var linked = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive", "ch", "client-1", "localhost", 50000, linked);

        activity.Should().NotBeNull();
        activity!.Links.Should().ContainSingle();
        activity.Links.First().Context.TraceId.Should().Be(traceId);
    }

    [Fact]
    public void StartConsumerActivity_WithNullClientId_OmitsClientIdTag()
    {
        using var activity = KubeMQActivitySource.StartConsumerActivity(
            "receive", "ch", null, "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.GetTagItem(SemanticConventions.MessagingClientId).Should().BeNull();
    }

    [Fact]
    public void StartClientActivity_SetsClientKindAndSendOperation()
    {
        using var activity = KubeMQActivitySource.StartClientActivity(
            "commands-channel", "client-1", "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client);
        activity.GetTagItem(SemanticConventions.MessagingOperationName)
            .Should().Be(SemanticConventions.OperationSend);
        activity.GetTagItem(SemanticConventions.MessagingOperationType)
            .Should().Be(SemanticConventions.OperationSend);
    }

    [Fact]
    public void StartClientActivity_WithNullClientId_OmitsClientIdTag()
    {
        using var activity = KubeMQActivitySource.StartClientActivity(
            "ch", null, "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.GetTagItem(SemanticConventions.MessagingClientId).Should().BeNull();
    }

    [Fact]
    public void StartServerActivity_SetsServerKindAndProcessOperation()
    {
        using var activity = KubeMQActivitySource.StartServerActivity(
            "commands-channel", "client-1", "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
        activity.GetTagItem(SemanticConventions.MessagingOperationName)
            .Should().Be(SemanticConventions.OperationProcess);
    }

    [Fact]
    public void StartServerActivity_WithLinkedContext_CreatesLink()
    {
        var linked = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        using var activity = KubeMQActivitySource.StartServerActivity(
            "ch", "client-1", "localhost", 50000, linked);

        activity.Should().NotBeNull();
        activity!.Links.Should().ContainSingle();
    }

    [Fact]
    public void StartServerActivity_WithoutLinkedContext_HasNoLinks()
    {
        using var activity = KubeMQActivitySource.StartServerActivity(
            "ch", "client-1", "localhost", 50000);

        activity.Should().NotBeNull();
        activity!.Links.Should().BeEmpty();
    }

    [Fact]
    public void SetError_SetsErrorStatusAndTag()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish", "ch", "client-1", "localhost", 50000);

        var ex = new InvalidOperationException("boom");
        KubeMQActivitySource.SetError(activity, ex);

        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("boom");
        activity.GetTagItem(SemanticConventions.ErrorType)
            .Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void SetError_WithCustomErrorType_UsesCustomType()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish", "ch", "client-1", "localhost", 50000);

        var ex = new Exception("fail");
        KubeMQActivitySource.SetError(activity, ex, "custom_error");

        activity!.GetTagItem(SemanticConventions.ErrorType).Should().Be("custom_error");
    }

    [Fact]
    public void SetError_WithNullActivity_DoesNotThrow()
    {
        var ex = new Exception("fail");
        var act = () => KubeMQActivitySource.SetError(null, ex);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRetryEvent_WithNullActivity_DoesNotThrow()
    {
        var act = () => KubeMQActivitySource.RecordRetryEvent(null, 1, 0.5, "transient");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRetryEvent_AddsEventToActivity()
    {
        using var activity = KubeMQActivitySource.StartProducerActivity(
            "publish", "ch", "client-1", "localhost", 50000);

        KubeMQActivitySource.RecordRetryEvent(activity, 2, 1.5, "transient");

        activity!.Events.Should().ContainSingle();
        var evt = activity.Events.First();
        evt.Name.Should().Be(SemanticConventions.RetryEventName);
        evt.Tags.Should().Contain(t =>
            t.Key == SemanticConventions.RetryAttemptAttribute && (int)t.Value! == 2);
    }

    [Fact]
    public void StartProducerActivity_WithoutListener_ReturnsNull()
    {
        _listener.Dispose();
        _collected.ForEach(a => a.Dispose());
        _collected.Clear();

        using var isolatedSource = new ActivitySource("KubeMQ.Sdk.Isolated");
        var activity = isolatedSource.StartActivity("test", ActivityKind.Producer);

        activity.Should().BeNull();
    }
}
