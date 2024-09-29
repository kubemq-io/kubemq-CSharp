using System.Text;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.PubSub.EventsStore;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace EventsStore
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("Some-client-id");
            EventsStoreClient client = new EventsStoreClient();
            CancellationTokenSource cts = new CancellationTokenSource();
            Result connectResult = await client.Connect(conn, cts.Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                return;
            }
            
            
            Result result = await client.Create("es1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create event store channel, error:{result.ErrorMessage}");
                return;
            }
            
            ListPubSubAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list event store channels, error:{listResult.ErrorMessage}");
                return;
            }
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list event channels, error:{listResult.ErrorMessage}");
                return;
            }
            result = await client.Delete("es1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete event channel, error:{result.ErrorMessage}");
                return;
            }
            //
            await Task.Delay(2000); 
            listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list event channels, error:{listResult.ErrorMessage}");
                return;
            }
            
            foreach (var channel in listResult.Channels)
            {
                Console.WriteLine($"{channel}");
            }
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
            Result subscribeResult =  client.Subscribe(subscription, cts.Token);
              if (!subscribeResult.IsSuccess)
              {
                  Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                  return;
              }
              await Task.Delay(1000);
              EventStore msg = new EventStore().SetChannel("es1").SetBody("hello kubemq - sending an event store message"u8.ToArray());
              Result sendResult=  await client.Send(msg);
              if (!sendResult.IsSuccess)
              {
                  Console.WriteLine($"Could not send an event to KubeMQ Server, error:{sendResult.ErrorMessage}");
                  return;
              }
            
              Console.WriteLine("Press any key to exit...");
              Console.ReadKey();
              await  client.Close();
        }
    }
}