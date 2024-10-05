using System.Text;
using KubeMQ.SDK.csharp.CQ.Queries;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Queries
{
    class Program
    {
        
        static async Task<QueriesClient> CreateQueriesClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id");
            QueriesClient client = new QueriesClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
        static async Task CreateQueriesChannel()
        {
            QueriesClient client =await CreateQueriesClient();
            Result result = await client.Create("query_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create queries channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Queries Channel Created");
            await client.Close();
        }
        
        static async Task DeleteQueriesChannel()
        {
            QueriesClient client =await CreateQueriesClient();
            Result result = await client.Delete("query_1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete queries channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Queries Channel Deleted");
            await client.Close();
        }
        
        static async Task ListQueriesChannels()
        {
            QueriesClient client =await CreateQueriesClient();
            ListCqAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list queries channels, error:{listResult.ErrorMessage}");
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
            QueriesClient client =await CreateQueriesClient();
            var subscription = new QueriesSubscription()
                .SetChannel("q1")
                .SetGroup("")
                .SetOnReceivedQuery(async receivedQuery =>
                {
                    Console.WriteLine($"Query Received: Id:{receivedQuery.Id}, Body:{Encoding.UTF8.GetString(receivedQuery.Body)}");
                    QueryResponse response = new QueryResponse()
                        .SetRequestId(receivedQuery.Id)
                        .SetQueryReceived(receivedQuery)
                        .SetIsExecuted(true)
                        .SetBody(Encoding.UTF8.GetBytes("query response"));
                    Result responseResult = await client.Response(response);
                    if (!responseResult.IsSuccess)
                    {
                        Console.WriteLine($"Error sending response to KubeMQ, error:{responseResult.ErrorMessage}");
                    }
                    Console.WriteLine($"Query Executed: Id:{receivedQuery.Id}");
        
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
              Query msg = new Query()
                  .SetChannel("q1")
                  .SetBody("hello kubemq - sending a query message"u8.ToArray())
                  .SetTimeout(10);
              QueryResponse sendResult=  await client.Send(msg);
              Console.WriteLine($"Query Response: {sendResult}");
              await  client.Close ();
        }
        static async Task Main(string[] args)
        {
            await CreateQueriesChannel();
            await DeleteQueriesChannel();
            await ListQueriesChannels();
            await SendReceiveResponse();
        }
    }
}