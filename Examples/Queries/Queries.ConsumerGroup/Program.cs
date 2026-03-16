// KubeMQ .NET SDK — Queries: Consumer Group Subscription
//
// This example demonstrates subscribing to queries with a consumer group.
// When multiple handlers join the same group, queries are load-balanced across them
// so that only one handler in the group processes each query.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Run Queries.SendQuery in a separate terminal to send queries
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queries-consumer-group-client",
});
await client.ConnectAsync();

Console.WriteLine("Subscribed to queries with consumer group 'handler-group'...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var query in client.SubscribeToQueriesAsync(
    new QueriesSubscription { Channel = "csharp-queries.consumer-group", Group = "handler-group" },
    cts.Token))
{
    var body = Encoding.UTF8.GetString(query.Body.Span);
    Console.WriteLine($"Query: {query.RequestId} — {body}");

    var responseBody = Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"handler\":\"group-member\"}");

    await client.SendQueryResponseAsync(
        requestId: query.RequestId,
        replyChannel: query.ReplyChannel!,
        body: responseBody,
        executed: true);

    Console.WriteLine("  -> Responded with data");
}

Console.WriteLine("Done.");
