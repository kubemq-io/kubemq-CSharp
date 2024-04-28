using KubeMQ.SDK.csharp.Unified.PubSub.Events;
using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;


namespace AuthClient
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").
                SetClientId("some-client-id").
                SetAuthToken("your-auth-token");
            Client client = new Client();
            try
            {
                await client.ConnectAsync(conn, CancellationToken.None);
                Event msg = new Event().SetChannel("e1").
                    SetBody("hello kubemq - sending an event message"u8.ToArray());
                await client.SendEventAsync(msg);
                await client.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
        }
    }
}