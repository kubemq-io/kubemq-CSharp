using System.Text;
using KubeMQ.SDK.csharp.Unified.PubSub.Events;
using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Results;


namespace Events
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("Some-client-id");
            Client client = new Client();
            CancellationTokenSource cts = new CancellationTokenSource();
            ConnectAsyncResult connectResult = await client.ConnectAsync(conn, cts.Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                return;
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
              SubscribeToEventsResult subscribeResult =  client.SubscribeToEvents(subscription, cts.Token);
              if (!subscribeResult.IsSuccess)
              {
                  Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                  return;
              }
              await Task.Delay(1000);
              Event msg = new Event().SetChannel("e1").SetBody("hello kubemq - sending an event message"u8.ToArray());
              SendEventAsyncResult sendResult=  await client.SendEventAsync(msg, cts.Token);
              if (!sendResult.IsSuccess)
              {
                  Console.WriteLine($"Could not send to KubeMQ Server, error:{sendResult.ErrorMessage}");
                  return;
              }

              Console.WriteLine("Press any key to exit...");
              Console.ReadKey();
              await  client.CloseAsync();
        }
    }
}