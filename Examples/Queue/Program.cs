using System;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using KubeMQ.SDK.csharp.Queue;
using KubeMQ.SDK.csharp.Queue.Stream;

namespace Queue
{
    class Program
    {
        /// <summary>
        /// KubeMQ ClientID for tracing and tracking.
        /// </summary>
        private static string ClientID = Environment.GetEnvironmentVariable("CLIENT") ?? $"MSMQ_Demo_{Environment.MachineName}";
        /// <summary>
        /// KubeMQ Command Chanel subscriber for handling  command request.
        /// </summary>
        private static string QueueName = Environment.GetEnvironmentVariable("QUEUENAME") ?? "QUEUE_DEMO";

        private static string KubeMQServerAddress = Environment.GetEnvironmentVariable("KUBEMQSERVERADDRESS") ?? "localhost:50000";

        private static string testGui = DateTime.UtcNow.ToBinary().ToString();


        static void Main(string[] args)
        {

            Console.WriteLine("Hello World!");
            Console.WriteLine($"[Demo] ClientID:{ClientID}");
            Console.WriteLine($"[Demo] QueueName:{QueueName}");
            Console.WriteLine($"[Demo] KubeMQServerAddress:{KubeMQServerAddress}");

            KubeMQ.SDK.csharp.Queue.Queue queue = null;
            try
            {
                queue = new KubeMQ.SDK.csharp.Queue.Queue(QueueName, ClientID, KubeMQServerAddress);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error while pinging to kubeMQ address:{ex.Message}");

                Console.WriteLine($"Error while pinging to kubeMQ address:{queue.ServerAddress}");
                Console.ReadLine();

            }

           #region "non tran"


            //Simple send message
            var res = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi, new message"),
                Metadata = "Meta",
                Tags = new Dictionary<string, string>()
                {
                    {"Action",$"SendQueueMessage_{testGui}" }
                }
            });
            if (res.IsError)
            {
                Console.WriteLine($"message enqueue error, error:{res.Error}");
            }
            else
            {
                Console.WriteLine($"message sent at, {res.SentAt}");
            }


            //Simple peak message
            var peakmsg = queue.PeekQueueMessage(1);
            {
                if (peakmsg.IsError)
                {
                    Console.WriteLine($"message peak error, error:{peakmsg.Error}");
                }
                foreach (var item in peakmsg.Messages)
                {
                    Console.WriteLine($"message received body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
                }
            }


            //Simple send Bulk of messages
            List<Message> msgs = new List<Message>();
            for (int i = 0; i < 5; i++)
            {
                msgs.Add(new KubeMQ.SDK.csharp.Queue.Message
                {
                    MessageID = i.ToString(),
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"im Message {i}"),
                    Metadata = "Meta",
                    Tags = new Dictionary<string, string>()/* ("Action", $"Batch_{testGui}_{i}")*/ 
                    {
                        {"Action",$"Batch_{testGui}_{i}"}
                    }
                });
            }

            //Batch send messages
            var resBatch = queue.SendQueueMessagesBatch(msgs);
            if (resBatch.HaveErrors)
            {
                Console.WriteLine($"message sent batch has errors");
            }
            foreach (var item in resBatch.Results)
            {
                if (item.IsError)
                {
                    Console.WriteLine($"message enqueue error, error:{item.Error}");
                }
                else
                {
                    Console.WriteLine($"message sent at, {item.SentAt}");
                }
            }

            //Queue receive messages
            var msg = queue.ReceiveQueueMessages();
            if (msg.IsError)
            {
                Console.WriteLine($"message dequeue error, error:{msg.Error}");
            }
            foreach (var item in msg.Messages)
            {
                Console.WriteLine($"message received body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");

            }

            #endregion

            #region "Tran"

            //Transaction queue for rerouting messages
            msgs = new List<Message>();
            for (int i = 0; i < 5; i++)
            {
                msgs.Add(new KubeMQ.SDK.csharp.Queue.Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"i'm Tran Message {i}"),
                    Metadata = "Meta",
                    Tags = new Dictionary<string, string>()
                    {
                        {"Action",$"Batch_{testGui}_{i}"}
                    }
                });
            }
            
            resBatch = queue.SendQueueMessagesBatch(msgs);
            if (resBatch.HaveErrors)
            {
                Console.WriteLine($"message sent batch has errors");
            }
            foreach (var item in resBatch.Results)
            {
                if (item.IsError)
                {
                    Console.WriteLine($"message enqueue error, error:{res.Error}");
                }
                else
                {
                    Console.WriteLine($"message sent at, {res.SentAt}");
                }
            }


            //create a new transaction stream instance 
            var transaction = queue.CreateTransaction();
            Console.WriteLine($"Transaction status:");
            TransactionMessagesResponse ms;

            //receive a massage with visibility of 5 seconds
            ms = transaction.Receive(5);
            if (ms.IsError)
            {
                Console.WriteLine($"message dequeue error, error:{ms.Error}");
                return;
            }

            var qm = ms.Message;

            //Modify msg to ExtendVisibility to 5 seconds 
            ms = transaction.ExtendVisibility(5);
            if (ms.IsError)
            {
                Console.WriteLine($"message dequeue error, error:{ms.Error}");
                return;
            }

            Thread.Sleep(1000);

            //Acknowledge message
            transaction.AckMessage(qm.Attributes.Sequence);
            //Close the transaction
            transaction.Close();


            #endregion

        }
    }
}
