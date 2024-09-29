using System.Text;
using KubeMQ.SDK.csharp.QueueStream;


namespace HelloWorld
{
    class Program
    {
        static async Task Main(string[] args)
        {
            QueueStream client = new QueueStream("localhost:50000", "Some-client-id");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="hello_world",
                Body = "hello kubemq - sending an queue message"u8.ToArray(),
            };
            List<Message> messages = new List<Message> { msg };
            await client.Send(new SendRequest(messages));
            Thread.Sleep(1000);
            PollRequest pollRequest = new PollRequest()
            {
                Queue = "hello_world",
                WaitTimeout = 1000,
                MaxItems = 1,
            };
            PollResponse response = await client.Poll(pollRequest);
            foreach (var receiveMsg in response.Messages)
            {
                Console.WriteLine(Encoding.UTF8.GetString(receiveMsg.Body));
                receiveMsg.Ack();
            }
        }
    }
}