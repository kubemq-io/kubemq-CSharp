using System;

namespace CommandQueryChannel
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting CommandQueryChannel...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            RunPubsubSenderTest();


            Console.ReadLine();
        }

        static void RunPubsubSenderTest()
        {
            CommandQueryChannel pubsubSubscriber = new CommandQueryChannel();
            Console.ReadKey();
        }
    }
}
