using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;

namespace BasicClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            Connection conn = new Connection().
                SetAddress("localhost:50001").
                SetClientId("some-client-id");
            Client client = new Client();
            try
            {
                await client.ConnectAsync(conn, CancellationToken.None);
                Console.WriteLine("Connected");
                await client.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            
        }
    }
}