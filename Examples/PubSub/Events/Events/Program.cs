using System.Text;
using KubeMQ.SDK.csharp.PubSub.Events;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Events
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("Some-client-id");
            EventsClient client = new EventsClient();
            CancellationTokenSource cts = new CancellationTokenSource();
            Result connectResult = await client.Connect(conn, cts.Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                return;
            }

            Result result = await client.Create("e1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create event channel, error:{result.ErrorMessage}");
                return;
            }
            
            ListPubSubAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list event channels, error:{listResult.ErrorMessage}");
                return;
            }
            result = await client.Delete("e1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete event channel, error:{result.ErrorMessage}");
                return;
            }
            
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
              Result subscribeResult =  client.Subscribe(subscription, cts.Token);
              if (!subscribeResult.IsSuccess)
              {
                  Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                  return;
              }
              await Task.Delay(1000);
              Event msg = new Event().SetChannel("e1").SetBody("hello kubemq - sending an event message"u8.ToArray());
              Result sendResult=  await client.Send(msg);
              if (!sendResult.IsSuccess)
              {
                  Console.WriteLine($"Could not send to KubeMQ Server, error:{sendResult.ErrorMessage}");
                  return;
              }

              Console.WriteLine("Press any key to exit...");
              Console.ReadKey();
              await  client.Close ();
        }
    }
}