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
            string testGui= DateTime.UtcNow.ToLongDateString();
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

            #region "nontran"
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
            var peekres = queue.PeakQueueMessage();
            {
                Console.WriteLine($"message peekID:{peekres.Message.MessageID} body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(peekres.Message.Body.ToByteArray())}");

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


            var msg = queue.ReceiveQueueMessages();
            if (msg.IsError)
            {
                Console.WriteLine($"message dequeue error, error:{msg.Error}");
            }
            foreach (var item in msg.Messages)
            {
                Console.WriteLine($"message received body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body.ToByteArray())}");

            }

            #endregion

            #region "Tran"

            var x = queue.gettranmessage();
            var ms = x.getmsg();
            var qm = ms.Message;
         //   var resack = x.AckMessage(qm);

            var ress=  x.ModifyVisibility(qm, 100);
            Console.WriteLine($"{ress.IsError}");
            #endregion






        }
    }
}
