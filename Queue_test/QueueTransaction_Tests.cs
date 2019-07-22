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
    public class QueueTransaction_Tests
    {

        [TestMethod]
        public void Get_2_messages_pass()
        {
            Queue queue = initLocalQueue($"Get_2_messages_pass{DateTime.UtcNow.ToBinary().ToString()}");


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

            Transaction tr = new Transaction(queue, 10);
            try
            {


                var recms = tr.Receive();
             //   Transaction tr = new Transaction(queue, 10);
                var recms2 = tr.Receive();
                //var ack =    tr.AckMessage(recms.Message);

                //var pek = queue.PeakQueueMessage();
                //var nextm = tr.next();


            }
            catch (Exception)
            {

                throw;
            }

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
            tr.ExtendVisibility(recms.Message, 5);
            Thread.Sleep(4 * 1000);
            var ackms = tr.AckMessage(recms.Message);

            Assert.IsFalse(ackms.IsError);
        }


        [TestMethod]
        public void ModifyNewMassage_Fail()
        {
            Queue queue = initLocalQueue();

            
            var smres = queue.SendQueueMessage(new Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi there"),
                Metadata = "first test Ack"              
            });
            Assert.IsFalse(smres.IsError, "$SendQueueMessage error:{smres.Error}");

            Transaction tr =  queue.CreateTransaction();
            var recms = tr.Receive();


            Assert.IsFalse(recms.IsError, "$SendQueueMessage error:{recms.Error}");
        
            try
            {

            var resMod = tr.Modifiy(recms.Message);
                Assert.IsFalse(resMod.IsError, "$SendQueueMessage error:{resMod.Error}");
             
                var recms2 = tr.Receive();

            }
            catch (Grpc.Core.RpcException rpc)
            {
                Assert.Fail(rpc.Message);
            }

            catch  (Exception ex)
            {
                Assert.AreEqual(ex.Message, "One or more errors occurred. (No current element is available.)");
            }
        }

      
        [TestMethod]
        public void ReciveVisabilatiyPass10Sec_Pass()
        {


        }
        [TestMethod]
        public void ModifyAfterAck_falil()
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
            var recms = tr.Receive();
            var ackms = tr.AckMessage(recms.Message);
            try
            {
                var recMod = tr.ExtendVisibility(recms.Message, 5);
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.InnerException.Message, "No current element is available.", $"ex:{ex.InnerException.Message}");
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
