// KubeMQ .NET SDK — Queries: Send Query
//
// This example demonstrates sending a query and receiving a data response.
// Queries are request/reply: the sender waits for the handler to return data.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Run Queries.HandleQuery in a separate terminal first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queries-send-query-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

try
{
    var response = await client.SendQueryAsync(new QueryMessage
    {
        Channel = "csharp-queries.send-query",
        Body = Encoding.UTF8.GetBytes("get-user:42"),
        TimeoutInSeconds = 10
    });

    Console.WriteLine($"Query executed: {response.Executed}");
    Console.WriteLine($"Response: {Encoding.UTF8.GetString(response.Body.Span)}");
}
catch (KubeMQTimeoutException)
{
    Console.WriteLine("Query timed out — no handler responded within 10 seconds");
}
catch (KubeMQOperationException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Query timed out — server reported: {ex.Message}");
}

Console.WriteLine("Done.");

// Expected output:
// Connected to KubeMQ server
// Query executed: True
// Response: <handler-response-body>
// Done.
