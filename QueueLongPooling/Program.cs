using System;
using System.Threading;
using KubeMQ.SDK.csharp.Queue.Stream;

namespace QueueLongPooling
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

        private static string KubeMQServerAddress = Environment.GetEnvironmentVariable("KubeMQServerAddress") ?? "localhost:50000";

        private static string testGui = DateTime.UtcNow.ToBinary().ToString();


        static void Main(string[] args)
        {

            Console.WriteLine("[DemoPoll]KubeMQ Queue pattern long polling");
            Console.WriteLine($"[DemoPoll] ClientID:{ClientID}");
            Console.WriteLine($"[DemoPoll] QueueName:{QueueName}");
            Console.WriteLine($"[DemoPoll] KubeMQServerAddress:{KubeMQServerAddress}");

            KubeMQ.SDK.csharp.Queue.Queue queue = null;
            try
            {
                queue = new KubeMQ.SDK.csharp.Queue.Queue(QueueName, ClientID, KubeMQServerAddress);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[DemoPoll]Error while ping:{ex.Message}, kubeMQ address{KubeMQServerAddress}");
                Console.ReadLine();

            }
           

            while (true)
            {
                var transaction = queue.CreateTransaction();

                if (transaction.InTransaction==true)
                {
                    Console.WriteLine($"[DemoPoll][Tran]Transaction Context is still busy, loop again");
                    continue;
                }
                Console.WriteLine($"[DemoPoll][Tran]Transaction ready and listening");

                TransactionMessagesResponse ms = transaction.Receive((int)new TimeSpan(1, 0, 0).TotalSeconds, (int)new TimeSpan(1, 0, 0).TotalSeconds);
                if (ms.IsError)
                {
                    Console.WriteLine($"[DemoPoll][Tran]message dequeue error, error:{ms.Error}");
                    continue;
                }

                HandleMSG(ms,transaction);

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
    }
}
