using System;

namespace EventChannel
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting EventChannel...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            RunPubsubSenderTest();


            Console.ReadLine();
        }

        static void RunPubsubSenderTest()
        {
            EventChannel pubsubSubscriber = new EventChannel();
            Console.ReadKey();
        }
    }
}
