using FluentAssertions;
using KubeMQ.Sdk.Internal.Protocol;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public sealed class ValueStopwatchTests
{
    [Fact]
    public void StartNew_ReturnsNonDefault()
    {
        var sw = ValueStopwatch.StartNew();

        sw.Should().NotBe(default(ValueStopwatch));
    }

    [Fact]
    public void GetElapsedTime_AfterDelay_ReturnsPositiveValue()
    {
        var sw = ValueStopwatch.StartNew();

        Thread.Sleep(15);

        sw.GetElapsedTime().Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetElapsedTime_ImmediatelyAfterStart_ReturnsNonNegative()
    {
        var sw = ValueStopwatch.StartNew();

        sw.GetElapsedTime().Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Default_IsDefaultStruct()
    {
        ValueStopwatch sw = default;

        sw.Should().Be(default(ValueStopwatch));
    }
}
