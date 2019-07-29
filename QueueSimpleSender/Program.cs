using System;

namespace QueueSimpleSender
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

            Console.WriteLine("[DemoSender]KubeMQ Queue pattern Simple Sender");
            Console.WriteLine($"[DemoSender] ClientID:{ClientID}");
            Console.WriteLine($"[DemoSender] QueueName:{QueueName}");
            Console.WriteLine($"[DemoSender] KubeMQServerAddress:{KubeMQServerAddress}");

            KubeMQ.SDK.csharp.Queue.Queue queue = null;
            try
            {
                queue = new KubeMQ.SDK.csharp.Queue.Queue(QueueName, ClientID, KubeMQServerAddress);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[DemoSender]Error while pinging to kubeMQ address:{ex.Message}");

                Console.WriteLine($"[DemoSender]Error while pinging to kubeMQ address:{KubeMQServerAddress}");
                Console.ReadLine();

            }

            Console.WriteLine($"Enter 'peak' to peak the {QueueName}_done");
            Console.WriteLine($"Enter 'ackall' to ack all in {QueueName}_done");
            Console.WriteLine($"Enter 'loopX' to loop X messages to {QueueName}");
            while (true)
            {
                Console.WriteLine($"Enter new message to queue {QueueName}, peak, ackall, loopx");
               
                var readline = Console.ReadLine();
                if (readline=="peak")
                {
                    peakmsgs( QueueName+ "_done");
                    continue;
                }
                if (readline == "ackall")
                {
                    acallkmsgs(QueueName + "_done");
                    continue;
                }
                if (readline.StartsWith("loop"))
                {
                    var splt = readline.Split("loop");
                    loopmsg(QueueName,splt[1]);
                    continue;
                }


                var res = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray(readline),
                    Metadata = readline
                });
                if (res.IsError)
                {
                    Console.WriteLine($"[DemoSender]Sent:{readline} error, error:{res.Error}");
                }
                else
                {
                    Console.WriteLine($"[DemoSender]Sent:{readline}");
                }
            }
        }

        private static void loopmsg(string queueName,string loop)
        {
            var q = new KubeMQ.SDK.csharp.Queue.Queue(queueName, ClientID, KubeMQServerAddress);

            for (int i = 0; i < int.Parse(loop); i++)
            {
                var res = q.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"loop_{i}"),
                    Metadata = $"loop_{ i }"
                });
                if (res.IsError)
                {
                    Console.WriteLine($"[DemoSender][loop]Sent:{$"loop_{ i }"} error, error:{res.Error}");
                }
                Console.WriteLine($"[DemoSender][loop]Sent:{$"loop_{ i }"}");
            }
        }

        private static void acallkmsgs(string queueName)
        {
            var q = new KubeMQ.SDK.csharp.Queue.Queue(queueName, ClientID, KubeMQServerAddress);
            var res = q.AckAllQueueMessagesResponse();
            if (res.IsError)
            {
                Console.WriteLine($"[DemoSender][acallkmsgs]message dequeue error, error:{res.Error}");
                return;
            }
            Console.WriteLine("[DemoSender][acallkmsgs]acked all messages");
          
        }

        private static void peakmsgs(string queueName)
        {
            var q = new KubeMQ.SDK.csharp.Queue.Queue(queueName, ClientID, KubeMQServerAddress);
            var res = q.PeakQueueMessage();
            if (res.IsError)
            {
                Console.WriteLine($"[DemoSender][peakmsgs]message dequeue error, error:{res.Error}");
                return;
            }
                        foreach (var item in res.Messages)
            {
                Console.WriteLine($"[DemoSender][peakmsgs]read:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
            }


        }
    }
}
