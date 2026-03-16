using FluentAssertions;
using KubeMQ.Sdk.Internal.Transport;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class BufferedMessageTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        byte[] payload = [1, 2, 3];

        var msg = new BufferedMessage(payload, "my-channel", "event", 42);

        msg.Payload.Should().BeSameAs(payload);
        msg.Channel.Should().Be("my-channel");
        msg.OperationType.Should().Be("event");
        msg.EstimatedSizeBytes.Should().Be(42);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        byte[] payload = [10, 20];
        var msg1 = new BufferedMessage(payload, "ch", "queue", 100);
        var msg2 = new BufferedMessage(payload, "ch", "queue", 100);

        msg1.Should().Be(msg2);
        (msg1 == msg2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentChannel_AreNotEqual()
    {
        byte[] payload = [1];
        var msg1 = new BufferedMessage(payload, "ch1", "event", 10);
        var msg2 = new BufferedMessage(payload, "ch2", "event", 10);

        msg1.Should().NotBe(msg2);
        (msg1 != msg2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentPayloadReference_AreNotEqual()
    {
        var msg1 = new BufferedMessage(new byte[] { 1 }, "ch", "event", 10);
        var msg2 = new BufferedMessage(new byte[] { 1 }, "ch", "event", 10);

        (msg1 == msg2).Should().BeFalse("record struct uses reference equality for arrays");
    }

    [Fact]
    public void Equality_DifferentOperationType_AreNotEqual()
    {
        byte[] payload = [1];
        var msg1 = new BufferedMessage(payload, "ch", "event", 10);
        var msg2 = new BufferedMessage(payload, "ch", "queue", 10);

        msg1.Should().NotBe(msg2);
    }

    [Fact]
    public void Equality_DifferentSize_AreNotEqual()
    {
        byte[] payload = [1];
        var msg1 = new BufferedMessage(payload, "ch", "event", 10);
        var msg2 = new BufferedMessage(payload, "ch", "event", 20);

        msg1.Should().NotBe(msg2);
    }

    [Fact]
    public void EmptyPayload_IsValid()
    {
        var msg = new BufferedMessage(Array.Empty<byte>(), "ch", "event", 0);

        msg.Payload.Should().BeEmpty();
        msg.EstimatedSizeBytes.Should().Be(0);
    }

    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        var msg = new BufferedMessage(new byte[] { 1 }, "test-ch", "event", 99);

        string str = msg.ToString();

        str.Should().Contain("test-ch");
        str.Should().Contain("event");
        str.Should().Contain("99");
    }

    [Fact]
    public void GetHashCode_SamePayloadRef_ProducesConsistentHash()
    {
        byte[] payload = [1, 2, 3];
        var msg1 = new BufferedMessage(payload, "ch", "event", 30);
        var msg2 = new BufferedMessage(payload, "ch", "event", 30);

        msg1.GetHashCode().Should().Be(msg2.GetHashCode());
    }

    [Fact]
    public void Deconstruction_Works()
    {
        byte[] payload = [5];
        var msg = new BufferedMessage(payload, "ch", "queue", 55);

        var (p, c, o, s) = msg;

        p.Should().BeSameAs(payload);
        c.Should().Be("ch");
        o.Should().Be("queue");
        s.Should().Be(55);
    }
}
