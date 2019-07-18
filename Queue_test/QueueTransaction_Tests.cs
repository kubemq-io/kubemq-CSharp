using System;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Queue;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Queue_test
{
    [TestClass]
    public class QueueTransaction_Tests
    {

        [TestMethod]
        public void ModifyNewMassage_Fail()
        {
            Queue queue = initLocalQueue();
          Transaction transaction =  queue.CreateTransaction();
           transaction.ModifiedMessage(mockMsg());


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
                Body =  Google.Protobuf.ByteString.Empty,
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

        private Queue initLocalQueue()
        {
            return new Queue("test", "test", "localhost:50000");
        }

        [TestMethod]
        public void ReciveVisabilatiyPass10Sec_Pass()
        {


        }
        [TestMethod]
        public void ModifyAfterAck_falil()
        {


        }
    }
}
