using System;
using System.Threading;
using Grpc.Core;
using KubeMQ.SDK.csharp.Queue.KubemqQueueErrors;
using KubeMQ.SDK.csharp.Queue.Stream;

namespace QueueLongPolling
{
    class Program
    {
        /// <summary>
        /// KubeMQ ClientID for tracing and tracking.
        /// </summary>
        private static readonly string ClientID = Environment.GetEnvironmentVariable("CLIENT") ?? $"MSMQ_Demo_{Environment.MachineName}";
        /// <summary>
        /// KubeMQ Command Chanel subscriber for handling  command request.
        /// </summary>
        private static readonly string QueueName = Environment.GetEnvironmentVariable("QUEUENAME") ?? "QUEUE_DEMO";

        private static readonly string KubeMQServerAddress = Environment.GetEnvironmentVariable("KUBEMQSERVERADDRESS") ?? "localhost:50000";
        
        static void Main()
        {

            Console.WriteLine("[DemoPoll]KubeMQ Queue pattern long polling");
            Console.WriteLine($"[DemoPoll] ClientID:{ClientID}");
            Console.WriteLine($"[DemoPoll] QueueName:{QueueName}");
            Console.WriteLine($"[DemoPoll] KubeMQServerAddress:{KubeMQServerAddress}");

            KubeMQ.SDK.csharp.Queue.Queue queue = CreateQueue();
            if (queue == null)
            {             
                Console.ReadLine();

            }


        
           

            while (true)
            {
                if (queue==null)
                {
                    Thread.Sleep(1000);
                    queue = CreateQueue();

                    continue;
                }
                var transaction = queue.CreateTransaction();

                if (transaction.InTransaction==true)
                {
                    Console.WriteLine($"[DemoPoll][Tran]Transaction Context is still busy, loop again");
                    continue;
                }
                Console.WriteLine($"[DemoPoll][Tran]Transaction ready and listening");
                try
                {
                    TransactionMessagesResponse ms = transaction.Receive((int)new TimeSpan(1, 0, 0).TotalSeconds, (int)new TimeSpan(0, 0, 5).TotalSeconds);

                    if (ms.IsError)
                    {
                        Console.WriteLine(ms.QueueErrors == KubemqQueueErrors.ErrNoNewMessageQueue
                            ? $"DemoPoll][Tran]no new message found"
                            : $"[DemoPoll][Tran]message dequeue error, error:{ms.Error}");
                        continue;
                    }

                    HandleMSG(ms, transaction);
                }
                catch (RpcException ex)
                {
                    Console.WriteLine($"[DemoPoll][Tran]RPC error, error:{ex.Message}");

                    queue = CreateQueue();
                }
                transaction.Close();
                Thread.Sleep(1);
            }
     

        }

        private static void HandleMSG(TransactionMessagesResponse ms, Transaction transaction)
        {
            var body = KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(ms.Message.Body);
            Console.WriteLine($"[DemoPoll][HandleMSG]Handling message ID{ms.Message.MessageID}, body:{body}");

           var res=  transaction.Modify(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"{body}_done"),
                Metadata = "ok",
                Queue = QueueName + "_done"
            }) ;
            if (res.IsError)
            {
                Console.WriteLine($"[DemoPoll][HandleMSG]Message Modify error, error:{res.Error}");
                return;
            }
            Console.WriteLine($"[DemoPoll][HandleMSG]Modify message ID{ms.Message.MessageID}, body:{body}_done, queue:{QueueName + "_done"}");

        }

        private static KubeMQ.SDK.csharp.Queue.Queue CreateQueue()
        {
            try
            {
                return new KubeMQ.SDK.csharp.Queue.Queue(QueueName, ClientID, KubeMQServerAddress);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[DemoPoll]Error while pinging to kubeMQ address:{ex.Message}");

                Console.WriteLine($"[DemoPoll]Error while pinging to kubeMQ address:{KubeMQServerAddress}");
            }
            return null;
        }
    }
}
