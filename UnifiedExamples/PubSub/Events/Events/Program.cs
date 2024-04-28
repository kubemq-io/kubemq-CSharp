using System.Text;
using KubeMQ.SDK.csharp.Unified.PubSub.Events;
using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;


namespace Events
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("some-client-id");
            Client client = new Client();
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                
                await client.ConnectAsync(conn, cts.Token);
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
                client.SubscribeToEvents(subscription, cts.Token);
                await Task.Delay(1000);
                Event msg = new Event().SetChannel("e1").
                    SetBody("hello kubemq - sending an event message"u8.ToArray());
                await client.SendEventAsync(msg);
 
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                
                cts.Cancel();
                await client.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            
        }
    }
}