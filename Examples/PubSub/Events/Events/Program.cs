using System.Text;
using KubeMQ.SDK.csharp.PubSub.Events;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Events
{
    class Program
    {
        static async Task<EventsClient> CreateEventsClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id");
            EventsClient client = new EventsClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
        static async Task CreateEventsChannel()
        {
            EventsClient client =await CreateEventsClient();
            Result result = await client.Create("events_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create events channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Eventss Channel Created");
            await client.Close();
        }
        
        static async Task DeleteEventsChannel()
        {
            EventsClient client =await CreateEventsClient();
            Result result = await client.Delete("events_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete events channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Eventss Channel Deleted");
            await client.Close();
        }
        
        static async Task ListEventsChannels()
        {
            EventsClient client =await CreateEventsClient();
            ListPubSubAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list events channels, error:{listResult.ErrorMessage}");
                return;
            }
            
            foreach (var channel in listResult.Channels)
            {
                Console.WriteLine($"{channel}");
            }
            await client.Close();
        }
        
        static async Task SendSubscribe()
        {
            EventsClient client =await CreateEventsClient();
            var subscription = new EventsSubscription()
                .SetChannel("e1")
                .SetGroup("")
                .SetOnReceiveEvent(receivedEvent =>
                {
                    Console.WriteLine($"Event Received: Id:{receivedEvent.Id}, Body:{Encoding.UTF8.GetString(receivedEvent.Body)}");
                })
                .SetOnError(exception =>
                {
                    Console.WriteLine($"Error: {exception.Message}");
                });
            Result subscribeResult =  client.Subscribe(subscription);
            if (!subscribeResult.IsSuccess)
            {
                Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                return;
            }
            Thread.Sleep(1000);
            Event msg = new Event().SetChannel("e1").SetBody("hello kubemq - sending an event message"u8.ToArray());
            Result sendResult=  await client.Send(msg);
            if (!sendResult.IsSuccess)
            {
                Console.WriteLine($"Could not send an event to KubeMQ Server, error:{sendResult.ErrorMessage}");
                return;
            }
            await  client.Close ();
        }
        static async Task Main(string[] args)
        {

            await CreateEventsChannel();
            await DeleteEventsChannel();
            await ListEventsChannels();
            await SendSubscribe();
        }
    }
}