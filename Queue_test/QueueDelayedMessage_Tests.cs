using System;
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
                        DelaySeconds =10
                    }

                },

                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi again"),
                    Metadata = "first test Ack",
                     Policy = new QueueMessagePolicy
                    {
                        DelaySeconds =15
                    }
                }
            }); ;

            Transaction tr = queue.CreateTransaction();

            var recms = tr.Receive();
            Assert.IsTrue(recms.IsError);
            Assert.AreEqual(recms.Error, "Error 138: no new message in queue, wait time expired");

            try
            {
                recms = tr.Receive();
            }
            catch (Exception ex)
            {

                throw;
            }


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
