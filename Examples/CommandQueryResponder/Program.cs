using System;

namespace CommandQueryResponder
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Starting CommandQueryResponder...");
            Console.WriteLine();
            Console.WriteLine("Press 'Enter' to stop the application...");
            Console.WriteLine();

            InitiatorExample();


            Console.ReadLine();
        }

        static void InitiatorExample()
        {
            CommandQueryResponder reqRepResponder = new CommandQueryResponder();
            Console.ReadKey();
        }
    }
}
