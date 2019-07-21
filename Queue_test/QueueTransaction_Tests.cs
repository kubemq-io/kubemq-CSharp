using System;
using System.Threading;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Queue;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Queue_test
{
    [TestClass]
    public class QueueTransaction_Tests
    {

        [TestMethod]
        public void SendReciveTranAck_Pass()
        {
            Queue queue = initLocalQueue("SendReciveTranAck_Pass");
            var smres =queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack"   ,
                MessageID = KubeMQ.SDK.csharp.Tools.IDGenerator.ReqID.Getid()
            });
            Transaction tr = queue.CreateTransaction();
            var recms=  tr.Receive();
            var ackms= tr.AckMessage(recms.Message);


            Assert.IsTrue(!ackms.IsError);

        }

        [TestMethod]
        public void SendReciveTranAcknRecive_Fail()
        {
            Queue queue = initLocalQueue("SendReciveTranAcknRecive_Pass");
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack",
                MessageID = KubeMQ.SDK.csharp.Tools.IDGenerator.ReqID.Getid()
            });
            Transaction tr = queue.CreateTransaction();
            var recms = tr.Receive();
            var ackms = tr.AckMessage(recms.Message);
            try
            {
                var recms2 = tr.Receive();
            }
            catch (Exception ex)
            {

                Assert.AreEqual(ex.InnerException.Message, "No current element is available.");
            }  
          


            Assert.IsTrue(!ackms.IsError);

        }

        [TestMethod]
        public void SendReciveTranVisabilityExpire_Fail()
        {
            Queue queue = initLocalQueue("SendReciveTranAcknRecive_Pass");
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack",
                MessageID = KubeMQ.SDK.csharp.Tools.IDGenerator.ReqID.Getid(),
               
            });
            Transaction tr = queue.CreateTransaction();
            var recms = tr.Receive();

            Thread.Sleep(tr.VisibilitySeconds+1 * 1000);
            var ackms = tr.AckMessage(recms.Message);

            Assert.AreEqual(ackms.Error, "Error 129: current visibility timer expired");

        }

        [TestMethod]
        public void SendReciveTranVisabilityModAck_Pass()
        {
            Queue queue = initLocalQueue("SendReciveTranAcknRecive_Pass");
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack",
                MessageID = KubeMQ.SDK.csharp.Tools.IDGenerator.ReqID.Getid()
            });
            Transaction tr = queue.CreateTransaction();
            var recms = tr.Receive();
            tr.ModifyVisibility(recms.Message, 5);
            Thread.Sleep(4 * 1000);
            var ackms = tr.AckMessage(recms.Message);

            Assert.IsTrue(!ackms.IsError);
        }


        [TestMethod]
        public void ModifyNewMassage_Fail()
        {
            Queue queue = initLocalQueue();

            bool pass;
            
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack"              
            });
            pass = !smres.IsError;

            Transaction tr =  queue.CreateTransaction();
            var recms = tr.Receive();
            pass = !recms.IsError;
            try
            {

            var resMod = tr.ModifiedMessage(recms.Message);
                pass = !resMod.IsError;

                var recms2 = tr.Receive();

            }
            catch (Grpc.Core.RpcException rpc)
            {

                pass = false;
            }

            catch  (Exception ex)
            {
                pass = ex.Message == "One or more errors occurred. (No current element is available.)";
            }

            Assert.IsTrue(pass);

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

        private Queue initLocalQueue(string name="test")
        {
            return new Queue(name, "test", "localhost:50000");
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
