using System;

namespace CommandQueryInitiator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting CommandQueryInitiator...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            InitiatorExample();


            Console.ReadLine();
        }

        static void InitiatorExample()
        {
            CommandQueryInitiator pubsubSubscriber = new CommandQueryInitiator();
            Console.ReadKey();
        }
    }
}
