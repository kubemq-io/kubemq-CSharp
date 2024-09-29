using System.Text;
using KubeMQ.SDK.csharp.CQ.Commands;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Commands
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("Some-client-id");
            CommandsClient client = new CommandsClient();
            CancellationTokenSource cts = new CancellationTokenSource();
            Result connectResult = await client.Connect(conn, cts.Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                return;
            }

            Result result = await client.Create("c1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create commands channel, error:{result.ErrorMessage}");
                return;
            }
            
            ListCqAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list commands channels, error:{listResult.ErrorMessage}");
                return;
            }
            result = await client.Delete("c1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete commands channel, error:{result.ErrorMessage}");
                return;
            }
            
            await Task.Delay(2000); 
            listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list commands channels, error:{listResult.ErrorMessage}");
                return;
            }
            
            foreach (var channel in listResult.Channels)
            {
                Console.WriteLine($"{channel}");
            }
            var subscription = new CommandsSubscription()
                    .SetChannel("c1")
                    .SetGroup("")
                    .SetOnReceivedCommand(async receivedCommand =>
                    {
                        Console.WriteLine($"Command Received: Id:{receivedCommand.Id}, Body:{Encoding.UTF8.GetString(receivedCommand.Body)}");
                        CommandResponse response = new CommandResponse()
                            .SetRequestId(receivedCommand.Id)
                            .SetCommandReceived(receivedCommand)
                            .SetIsExecuted(true);
                        Result responseResult = await client.Response(response);
                        if (!responseResult.IsSuccess)
                        {
                            Console.WriteLine($"Error sending response to KubeMQ, error:{responseResult.ErrorMessage}");
                        }
                        Console.WriteLine($"Command Executed: Id:{receivedCommand.Id}");

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
              Command msg = new Command()
                  .SetChannel("c1")
                  .SetBody("hello kubemq - sending a command message"u8.ToArray())
                  .SetTimeout(10);
              CommandResponse sendResult=  await client.Send(msg);
              Console.WriteLine($"Command Response: {sendResult}");
              Console.WriteLine("Press any key to exit...");
              Console.ReadKey();
              await  client.Close ();
        }
    }
}