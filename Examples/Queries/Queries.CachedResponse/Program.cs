// KubeMQ .NET SDK — Queries: Cached Response
//
// This example demonstrates query caching. The server caches the response
// and returns it directly for subsequent queries with the same cache key,
// without forwarding to the handler.
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
    ClientId = "csharp-queries-cached-response-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

try
{
    // First query — will be forwarded to the handler
    var response1 = await client.SendQueryAsync(new QueryMessage
    {
        Channel = "csharp-queries.cached-response",
        Body = Encoding.UTF8.GetBytes("get-user:42"),
        TimeoutInSeconds = 10,
        CacheKey = "user-42",
        CacheTtlSeconds = 60
    });

    Console.WriteLine($"First query - CacheHit: {response1.CacheHit}");
    Console.WriteLine($"Response: {Encoding.UTF8.GetString(response1.Body.Span)}");

    // Second query with same cache key — served from cache
    var response2 = await client.SendQueryAsync(new QueryMessage
    {
        Channel = "csharp-queries.cached-response",
        Body = Encoding.UTF8.GetBytes("get-user:42"),
        TimeoutInSeconds = 10,
        CacheKey = "user-42",
        CacheTtlSeconds = 60
    });

    Console.WriteLine($"\nSecond query - CacheHit: {response2.CacheHit}");
    Console.WriteLine($"Response: {Encoding.UTF8.GetString(response2.Body.Span)}");
}
catch (KubeMQTimeoutException)
{
    Console.WriteLine("Query timed out — no handler responded within 10 seconds");
}
catch (KubeMQOperationException ex)
{
    Console.WriteLine($"Operation error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}

Console.WriteLine("Done.");
