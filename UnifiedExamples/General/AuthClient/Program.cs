using KubeMQ.SDK.csharp.Unified.PubSub.Events;
using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Results;

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
            ConnectAsyncResult result = await client.ConnectAsync(conn, CancellationToken.None);
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{result.ErrorMessage}");
                return;
            }    
            Console.WriteLine("Connected");
            await client.CloseAsync();
            
        }
    }
}