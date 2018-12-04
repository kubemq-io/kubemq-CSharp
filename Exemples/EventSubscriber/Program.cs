using System;

namespace EventSubscriber
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting EventSubscriber...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            RunPubsubSenderTest();


            Console.ReadLine();
        }

        static void RunPubsubSenderTest()
        {
            EventSubscriber pubsubSubscriber = new EventSubscriber();
            Console.ReadKey();
        }
    }
}
