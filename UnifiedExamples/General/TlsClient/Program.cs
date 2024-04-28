using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;
using KubeMQ.SDK.csharp.Unified.Results;
namespace TlsClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            Connection conn = new Connection().
                SetAddress("localhost:50000").
                SetClientId("some-client-id").
                SetTls(new TlsConfig().
                    SetEnabled(true).
                    SetCertFile("./localhost.pem").
                    SetKeyFile("./localhost-key.pem").
                    SetCaFile("./rootCA.pem"));
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