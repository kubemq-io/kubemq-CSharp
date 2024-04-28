using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;

namespace Unified.General
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("some-client-id")
                .SetTls(new TlsConfig().SetEnabled(true).SetCertFile("./localhost.pem")
                    .SetKeyFile("./localhost-key.pem").SetCaFile("./rootCA.pem"));
            Transport transport = new Transport(conn);
            try
            {
                await transport.InitializeAsync(CancellationToken.None);
                Console.WriteLine("Connected");
                await transport.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


        }
    }
}