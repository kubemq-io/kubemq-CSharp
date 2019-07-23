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
    public class QueueTransaction_Tests
    {

        [TestMethod]
        public void Get_Messages_pass()
        {
            Queue queue = initLocalQueue($"Get_Messages_pass{DateTime.UtcNow.ToBinary().ToString()}");


            var smres = queue.SendQueueMessagesBatch(new Message[] {
                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                    Metadata = "first test Ack",
                    MessageID = "test1"


                },

                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi again"),
                    Metadata = "sec  test Ack",
                    MessageID = "test2"


                }
            }); ;

            var tr = queue.CreateTransaction();
            var recms = tr.Receive(3);
            Assert.IsFalse(recms.IsError);
            Assert.IsFalse(tr.AckMessage(recms.Message.Attributes.Sequence).IsError);
            Assert.AreEqual(1, new List<Message>(queue.PeakQueueMessage().Messages).Count);
            Assert.IsFalse(tr.Receive().IsError);
            tr.Close();
        }



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
            var recms=  tr.Receive(5);
            Assert.IsFalse(tr.AckMessage(recms.Message.Attributes.Sequence).IsError);
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
            var recms = tr.Receive(5);
            var ackms = tr.AckMessage(recms.Message.Attributes.Sequence);
            var recms2 = tr.Receive(5);
            Assert.IsFalse(ackms.IsError);

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
            var recms = tr.Receive(10);

            Thread.Sleep(11 * 1000);
            var ackms = tr.AckMessage(recms.Message.Attributes.Sequence);

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
            tr.ExtendVisibility(5);
            Thread.Sleep(4 * 1000);
            var ackms = tr.AckMessage(recms.Message.Attributes.Sequence);

            Assert.IsFalse(ackms.IsError,$"{ackms.Error}");

        }


        [TestMethod]
        public void ModifyNewMassage_pass()
        {
            Queue queue = initLocalQueue($"ModifyNewMassage_pass{DateTime.UtcNow.ToBinary().ToString()}");

            
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack"              
            });
            Assert.IsFalse(smres.IsError, "$SendQueueMessage error:{smres.Error}");



            Transaction tr =  queue.CreateTransaction();

            var recms = tr.Receive(10);
            Assert.IsFalse(recms.IsError, "$SendQueueMessage error:{recms.Error}");
            var modMsg = new Message()
            {
              Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("well hello"),
              Queue = queue.QueueName
            };
            var resMod = tr.Modifiy(modMsg);
            Assert.IsFalse(resMod.IsError, $"SendQueueMessage error:{resMod.Error}");
            tr.Close();
           Thread.Sleep(10);
            var recms2 = tr.Receive(3,5);
            Assert.AreEqual("well hello", KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(recms2.Message.Body));
           tr.Close();
        }

      
      
        [TestMethod]
        public void ModifyAfterAck_fail()
        {
            Queue queue = initLocalQueue("ModifyAfterAck_falil");


            var smres = queue.SendQueueMessagesBatch(new Message[] {
                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                    Metadata = "first test Ack"
                },

                new Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi again"),
                    Metadata = "first test Ack"
                }
            });
       
            Transaction tr = queue.CreateTransaction();
            var recms = tr.Receive(5);
            var ackms = tr.AckMessage(recms.Message.Attributes.Sequence);
            Thread.Sleep(100);
            var recMod = tr.ExtendVisibility(5);
            Assert.IsTrue(recMod.IsError);

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
