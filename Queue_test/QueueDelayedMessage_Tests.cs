using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Queue;
using KubeMQ.SDK.csharp.Queue.Stream;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Queue_test
{
    [TestClass]
    public class QueueDelayedMessage_Tests
    {
        [TestMethod]
        public void DelayedMessage_pass()
        {
            Queue queue = initLocalQueue($"DelayedMessage_pass_{DateTime.UtcNow.ToBinary().ToString()}");


            var smres = queue.SendQueueMessagesBatch(new Message[] {
                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                    Metadata = "first test Ack",
                    Policy = new QueueMessagePolicy
                    {
                        DelaySeconds =3
                    }

                },

                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi again"),
                    Metadata = "first test Ack",
                     Policy = new QueueMessagePolicy
                    {
                        DelaySeconds =5
                    }
                }
            }); ;


            var recms = queue.ReceiveQueueMessages();
            Assert.IsFalse(recms.IsError);
            Assert.AreEqual(0, new List<Message>(recms.Messages).Count);
            Thread.Sleep(3000);
            recms = queue.ReceiveQueueMessages();
            Assert.IsFalse(recms.IsError);
            Assert.AreEqual(1, new List<Message>(recms.Messages).Count);
            Thread.Sleep(2000);
            recms = queue.ReceiveQueueMessages();
            Assert.IsFalse(recms.IsError);
            Assert.AreEqual(1, new List<Message>(recms.Messages).Count);

        }

        private QueueMessage mockMsg()
        {
            return new QueueMessage()
            {
                Attributes = new QueueMessageAttributes
                {
                    DelayedTo = 1,
                    ExpirationAt = 1,
                    MD5OfBody = "",
                    ReceiveCount = 1,
                    ReRouted = false,
                    ReRoutedFromQueue = "",
                    Sequence = 123,
                    Timestamp = 1

                },
                Body = Google.Protobuf.ByteString.Empty,
                Channel = "123",
                ClientID = "123",
                MessageID = "!23",
                Metadata = "",
                Policy = new QueueMessagePolicy
                {
                    DelaySeconds = 12,
                    ExpirationSeconds = 123,
                    MaxReceiveCount = 1,
                    MaxReceiveQueue = "123"
                },
                Tags = { }
            };
        }

        private Queue initLocalQueue(string name = "test")
        {
            return new Queue(name, "test", "localhost:50000");
        }

    }
}
