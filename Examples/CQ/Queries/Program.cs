using System.Text;
using KubeMQ.SDK.csharp.CQ.Queries;

using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;


namespace Queries
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Connection conn = new Connection().SetAddress("localhost:50000").SetClientId("Some-client-id");
            QueriesClient client = new QueriesClient();
            CancellationTokenSource cts = new CancellationTokenSource();
            Result connectResult = await client.Connect(conn, cts.Token);
            // if (!connectResult.IsSuccess)
            // {
            //     Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            //     return;
            // }
            //
            // Result result = await client.Create("q1");
            // if (!result.IsSuccess)
            // {
            //     Console.WriteLine($"Could not create queries channel, error:{result.ErrorMessage}");
            //     return;
            // }
            //
            // ListCqAsyncResult listResult = await client.List();
            // if (!listResult.IsSuccess)
            // {
            //     Console.WriteLine($"Could not list queries channels, error:{listResult.ErrorMessage}");
            //     return;
            // }
            // result = await client.Delete("q1");
            // if (!result.IsSuccess)
            // {
            //     Console.WriteLine($"Could not delete queries channel, error:{result.ErrorMessage}");
            //     return;
            // }
            //
            // await Task.Delay(2000); 
            // listResult = await client.List();
            // if (!listResult.IsSuccess)
            // {
            //     Console.WriteLine($"Could not list queries channels, error:{listResult.ErrorMessage}");
            //     return;
            // }
            //
            // foreach (var channel in listResult.Channels)
            // {
            //     Console.WriteLine($"{channel}");
            // }
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
              Result subscribeResult =  client.Subscribe(subscription, cts.Token);
              if (!subscribeResult.IsSuccess)
              {
                  Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
                  return;
              }
              await Task.Delay(1000);
              Query msg = new Query()
                  .SetChannel("q1")
                  .SetBody("hello kubemq - sending a query message"u8.ToArray())
                  .SetTimeout(10);
              QueryResponse sendResult=  await client.Send(msg);
              Console.WriteLine($"Query Response: {sendResult}");
              Console.WriteLine("Press any key to exit...");
              Console.ReadKey();
              await  client.Close ();
        }
    }
}