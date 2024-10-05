using System.Text;
using KubeMQ.SDK.csharp.CQ.Commands;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Commands
{
    class Program
    {
        static async Task<CommandsClient> CreateCommandsClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id");
            CommandsClient client = new CommandsClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
        
        static async Task CreateCommandsChannel()
        {
            CommandsClient client =await CreateCommandsClient();
            Result result = await client.Create("command_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create commands channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Commands Channel Created");
            await client.Close();
        }
        
        static async Task DeleteCommandsChannel()
        {
            CommandsClient client =await CreateCommandsClient();
            Result result = await client.Delete("command_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete commands channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Commands Channel Deleted");
            await client.Close();
        }
        
        static async Task ListCommandsChannels()
        {
            CommandsClient client =await CreateCommandsClient();
            ListCqAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list commands channels, error:{listResult.ErrorMessage}");
                return;
            }
            
            foreach (var channel in listResult.Channels)
            {
                Console.WriteLine($"{channel}");
            }
            await client.Close();
        }
        
        static async Task SendReceiveResponse()
        {
            CommandsClient client =await CreateCommandsClient();
            var subscription = new CommandsSubscription()
                .SetChannel("q1")
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
            Result subscribeResult =  client.Subscribe(subscription);
            if (!subscribeResult.IsSuccess)
            {
                Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                return;
            }
            Thread.Sleep(1000);
            Command msg = new Command()
                .SetChannel("q1")
                .SetBody("hello kubemq - sending a command message"u8.ToArray())
                .SetTimeout(10);
            CommandResponse sendResult=  await client.Send(msg);
            Console.WriteLine($"Command Response: {sendResult}");
            await  client.Close ();
        }
        static async Task Main(string[] args)
        {

            await CreateCommandsChannel();
            await DeleteCommandsChannel();
            await ListCommandsChannels();
            await SendReceiveResponse();
        }
    }
}