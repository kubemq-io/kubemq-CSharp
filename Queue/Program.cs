using System;

namespace Queue
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("ido", "test");
           var res = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                MessageID = "123",
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hi my name is dodo"),
                Metadata = "MeaAleha",
                //Tags = 



            });
        }
    }
}
