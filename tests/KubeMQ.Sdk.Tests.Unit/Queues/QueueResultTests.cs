using FluentAssertions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Queues;

public class QueueResultTests
{
    public class QueueReceiveResultTests
    {
        [Fact]
        public void Default_RequestId_IsEmptyString()
        {
            var result = new QueueReceiveResult();

            result.RequestId.Should().BeEmpty();
        }

        [Fact]
        public void Default_Messages_IsEmptyList()
        {
            var result = new QueueReceiveResult();

            result.Messages.Should().BeEmpty();
        }

        [Fact]
        public void Default_MessagesReceived_IsZero()
        {
            var result = new QueueReceiveResult();

            result.MessagesReceived.Should().Be(0);
        }

        [Fact]
        public void Default_MessagesExpired_IsZero()
        {
            var result = new QueueReceiveResult();

            result.MessagesExpired.Should().Be(0);
        }

        [Fact]
        public void Default_IsPeak_IsFalse()
        {
            var result = new QueueReceiveResult();

            result.IsPeak.Should().BeFalse();
        }

        [Fact]
        public void Default_IsError_IsFalse()
        {
            var result = new QueueReceiveResult();

            result.IsError.Should().BeFalse();
        }

        [Fact]
        public void Default_Error_IsEmptyString()
        {
            var result = new QueueReceiveResult();

            result.Error.Should().BeEmpty();
        }

        [Fact]
        public void CanSet_AllProperties_ViaInit()
        {
            var result = new QueueReceiveResult
            {
                RequestId = "req-123",
                MessagesReceived = 5,
                MessagesExpired = 2,
                IsPeak = true,
                IsError = true,
                Error = "something went wrong",
            };

            result.RequestId.Should().Be("req-123");
            result.MessagesReceived.Should().Be(5);
            result.MessagesExpired.Should().Be(2);
            result.IsPeak.Should().BeTrue();
            result.IsError.Should().BeTrue();
            result.Error.Should().Be("something went wrong");
        }

        [Fact]
        public void RecordEquality_SameValues_AreEqual()
        {
            var a = new QueueReceiveResult
            {
                RequestId = "req-1",
                MessagesReceived = 3,
                MessagesExpired = 1,
                IsPeak = false,
                IsError = false,
                Error = string.Empty,
            };

            var b = new QueueReceiveResult
            {
                RequestId = "req-1",
                MessagesReceived = 3,
                MessagesExpired = 1,
                IsPeak = false,
                IsError = false,
                Error = string.Empty,
            };

            a.Should().Be(b);
        }

        [Fact]
        public void RecordEquality_DifferentValues_AreNotEqual()
        {
            var a = new QueueReceiveResult { RequestId = "req-1" };
            var b = new QueueReceiveResult { RequestId = "req-2" };

            a.Should().NotBe(b);
        }

        [Fact]
        public void WithExpression_CreatesModifiedCopy()
        {
            var original = new QueueReceiveResult
            {
                RequestId = "req-1",
                MessagesReceived = 3,
                IsError = false,
            };

            var modified = original with { IsError = true, Error = "fail" };

            modified.RequestId.Should().Be("req-1");
            modified.MessagesReceived.Should().Be(3);
            modified.IsError.Should().BeTrue();
            modified.Error.Should().Be("fail");

            // Original is unchanged
            original.IsError.Should().BeFalse();
            original.Error.Should().BeEmpty();
        }
    }

    public class QueueUpstreamResultTests
    {
        [Fact]
        public void Default_RefRequestId_IsEmptyString()
        {
            var result = new QueueUpstreamResult();

            result.RefRequestId.Should().BeEmpty();
        }

        [Fact]
        public void Default_Results_IsEmptyList()
        {
            var result = new QueueUpstreamResult();

            result.Results.Should().BeEmpty();
        }

        [Fact]
        public void Default_IsError_IsFalse()
        {
            var result = new QueueUpstreamResult();

            result.IsError.Should().BeFalse();
        }

        [Fact]
        public void Default_Error_IsEmptyString()
        {
            var result = new QueueUpstreamResult();

            result.Error.Should().BeEmpty();
        }

        [Fact]
        public void CanSet_AllProperties_ViaInit()
        {
            var sendResult = new QueueSendResult
            {
                MessageId = "msg-1",
                SentAt = DateTimeOffset.UtcNow,
                IsError = false,
            };

            var result = new QueueUpstreamResult
            {
                RefRequestId = "ref-456",
                Results = new[] { sendResult },
                IsError = true,
                Error = "partial failure",
            };

            result.RefRequestId.Should().Be("ref-456");
            result.Results.Should().HaveCount(1);
            result.Results[0].MessageId.Should().Be("msg-1");
            result.IsError.Should().BeTrue();
            result.Error.Should().Be("partial failure");
        }

        [Fact]
        public void RecordEquality_SameValues_AreEqual()
        {
            var a = new QueueUpstreamResult
            {
                RefRequestId = "ref-1",
                IsError = false,
                Error = string.Empty,
            };

            var b = new QueueUpstreamResult
            {
                RefRequestId = "ref-1",
                IsError = false,
                Error = string.Empty,
            };

            a.Should().Be(b);
        }

        [Fact]
        public void RecordEquality_DifferentValues_AreNotEqual()
        {
            var a = new QueueUpstreamResult { RefRequestId = "ref-1" };
            var b = new QueueUpstreamResult { RefRequestId = "ref-2" };

            a.Should().NotBe(b);
        }

        [Fact]
        public void WithExpression_CreatesModifiedCopy()
        {
            var original = new QueueUpstreamResult
            {
                RefRequestId = "ref-1",
                IsError = false,
            };

            var modified = original with { IsError = true, Error = "oops" };

            modified.RefRequestId.Should().Be("ref-1");
            modified.IsError.Should().BeTrue();
            modified.Error.Should().Be("oops");

            // Original is unchanged
            original.IsError.Should().BeFalse();
            original.Error.Should().BeEmpty();
        }
    }
}
