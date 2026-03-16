// KubeMQ .NET SDK — Config: Explicit Close
//
// This example demonstrates manually disposing a client without 'await using'.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-config-explicit-close-client",
};

var client = new KubeMQClient(options);
await client.ConnectAsync();
Console.WriteLine("Connected. Press Enter to disconnect...");
Console.ReadLine();
await client.DisposeAsync();
Console.WriteLine("Disconnected.");
