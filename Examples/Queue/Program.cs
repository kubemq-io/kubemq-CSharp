using System;
using System.Collections.Generic;
using Google.Protobuf;
using KubeMQ.SDK.csharp.Queue;

namespace Queue
{
    class Program
    {
        static void Main(string[] args)
        {
            string testGui= DateTime.UtcNow.ToBinary().ToString();
            Console.WriteLine("Hello World!");

            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("ido1", "test1","localhost:50000");

            try
            {
                queue.Ping();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error while pinging to kubeMQ address:{ex.Message}");

                Console.WriteLine($"Error while pinging to kubeMQ address:{queue.ServerAddress}");
                Console.ReadLine();

            }

            //#region "nontran"
            var res = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                MessageID = "123",            
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi my name is dodo"),
                Metadata = "MetaAleha",
                Tags = new List<(string, string)>() { ("Action", $"SendQueueMessage_{testGui}") },
            });
            if (res.IsError)
            {
                Console.WriteLine($"message enqueue error, error:{res.Error}");
            }
            else
            {
                Console.WriteLine($"message sent at, {res.SentAt}");
            }

            //TODO:Bug, peak when queue 0
            var peakmsg = queue.PeakQueueMessage();
            {
                if (peakmsg.IsError)
                {
                    Console.WriteLine($"message peak error, error:{peakmsg.Error}");
                }
                foreach (var item in peakmsg.Messages)
                {
                    Console.WriteLine($"message received body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body.ToByteArray())}");
                }
            }

            List<Message> msgs = new List<Message>();
            for (int i = 0; i < 5; i++)
            {
                msgs.Add(new KubeMQ.SDK.csharp.Queue.Message
                {
                    MessageID = i.ToString(),
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"im Message {i}"),
                    Metadata = "Meta",
                    Tags = new List<(string, string)>() { ("Action", $"Batch_{testGui}_{i}") },
                });
            }


            var resBatch = queue.SendQueueMessagesBatch(msgs);
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


            //var msg = queue.ReceiveQueueMessages();
            //if (msg.IsError)
            //{
            //    Console.WriteLine($"message dequeue error, error:{msg.Error}");
            //}
            //foreach (var item in msg.Messages)
            //{
            //    Console.WriteLine($"message received body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body.ToByteArray())}");

            //}

            //#endregion

            #region "Tran"

            var transaction = queue.CreateTransaction();
            Console.WriteLine($"Transaction status:{transaction.Status}");
            KubeMQ.Grpc.StreamQueueMessagesResponse ms;
            try
            {
                ms = transaction.Receive();
                if (ms.IsError)
                {
                    Console.WriteLine($"message dequeue error, error:{res.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"message dequeue error, error:{ex.Message}");
                return;
            }

            var qm = ms.Message;
         
            try
            {
               ms = transaction.ModifyVisibility(qm, 100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"message dequeue error, error:{ex.Message}");
                return;
            }
            Console.WriteLine($"{ms.IsError}");

            try
            {
                ms = transaction.AckMessage(qm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"message dequeue error, error:{ex.Message}");
                return;
            }
            Console.WriteLine($"{ms.IsError}");

            #endregion
            

        }
    }
}
