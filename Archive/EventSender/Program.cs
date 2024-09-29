using System;

namespace EventSender
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting EventSender...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            RunPubsubSenderTest();


            Console.ReadLine();
        }

        static void RunPubsubSenderTest()
        {
            EventSender pubsubSubscriber = new EventSender();
            Console.ReadKey();
        }
    }
}
