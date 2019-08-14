using System;

namespace Queue_Receive_a_Message
{
    class Program
    {
        static void Main(string[] args)
        {
            var QueueName = "hello-world-queue";
            var ClientID = "test-queue-client-id";
            var KubeMQServerAddress = "localhost:50000";


            KubeMQ.SDK.csharp.Queue.Queue queue = null;
            try
            {
                queue = new KubeMQ.SDK.csharp.Queue.Queue(QueueName, ClientID, KubeMQServerAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);      
            }

            try
            {
                var msg = queue.ReceiveQueueMessages();
                if (msg.IsError)
                {
                    Console.WriteLine($"message dequeue error, error:{msg.Error}");
                    return;
                }
                Console.WriteLine($"Received {msg.MessagesReceived} Messages:");

                foreach (var item in msg.Messages)
                {
                    Console.WriteLine($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
