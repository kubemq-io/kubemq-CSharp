// KubeMQ .NET SDK — Queries: Handle Query
//
// This example demonstrates subscribing to incoming queries and responding with data.
// Run this before Queries.SendQuery to handle the request.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queries-handle-query-client",
});
await client.ConnectAsync();

Console.WriteLine("Waiting for queries on 'csharp-demo.queries'...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var query in client.SubscribeToQueriesAsync(
    new QueriesSubscription { Channel = "csharp-queries.handle-query" }, cts.Token))
{
    var body = Encoding.UTF8.GetString(query.Body.Span);
    Console.WriteLine($"Received query: {body}");

    // Process query and respond with data
    var responseBody = Encoding.UTF8.GetBytes("{\"id\":42,\"name\":\"Alice\",\"email\":\"alice@example.com\"}");

    await client.SendQueryResponseAsync(new QueryResponse
    {
        RequestId = query.RequestId,
        ReplyChannel = query.ReplyChannel!,
        Body = responseBody,
        Executed = true,
    });

    Console.WriteLine("  -> Responded with user data");
}

Console.WriteLine("Done.");
