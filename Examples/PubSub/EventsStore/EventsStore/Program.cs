using System.Text;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.PubSub.EventsStore;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace EventsStore
{
    class Program
    {
        static async Task<EventsStoreClient> CreateEventsStoresClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id");
            EventsStoreClient client = new EventsStoreClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
        static async Task CreateEventsStoresChannel()
        {
            EventsStoreClient client =await CreateEventsStoresClient();
            Result result = await client.Create("events_store_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create events-store channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("EventsStores Channel Created");
            await client.Close();
        }
        
        static async Task DeleteEventsStoresChannel()
        {
            EventsStoreClient client =await CreateEventsStoresClient();
            Result result = await client.Delete("events_store_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete events-store channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("EventsStores Channel Deleted");
            await client.Close();
        }
        
        static async Task ListEventsStoresChannels()
        {
            EventsStoreClient client =await CreateEventsStoresClient();
            ListPubSubAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list events-store channels, error:{listResult.ErrorMessage}");
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
            EventsStoreClient client =await CreateEventsStoresClient();
            var subscription = new EventsStoreSubscription()
                .SetChannel("es1")
                .SetGroup("")
                .SetStartAtType(StartAtType.StartAtTypeFromSequence)
                .SetStartAtSequence(1)
                .SetOnReceiveEvent(receivedEvent =>
                {
                    Console.WriteLine($"Event Store Received: Id:{receivedEvent.Id}, Body:{Encoding.UTF8.GetString(receivedEvent.Body)}");
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
            EventStore msg = new EventStore().SetChannel("es1").SetBody("hello kubemq - sending an event store message"u8.ToArray());
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
            
            await CreateEventsStoresChannel();
            await DeleteEventsStoresChannel();
            await ListEventsStoresChannels();
            await SendSubscribe();
        }
    }
}