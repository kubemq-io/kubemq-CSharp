using KubeMQ.SDK.csharp.Unified;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;

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