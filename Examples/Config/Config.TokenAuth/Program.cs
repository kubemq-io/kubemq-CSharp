// KubeMQ .NET SDK — Config: Token Authentication
//
// This example demonstrates connecting with JWT token authentication.
// Shows both static token and dynamic token provider approaches.
//
// Prerequisites:
//   - KubeMQ server running with authentication enabled
//   - Valid JWT token
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Auth;

// Option 1: Static token
await using var client1 = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    AuthToken = "your-jwt-token-here"
});

Console.WriteLine("Option 1: Static auth token");

// Option 2: Dynamic token provider (for token refresh)
await using var client2 = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    CredentialProvider = new StaticTokenProvider("your-jwt-token-here")
});

Console.WriteLine("Option 2: StaticTokenProvider");

// Option 3: Custom token provider for dynamic refresh
// Implement ICredentialProvider for rotating/refreshing tokens:
//
// public class MyTokenProvider : ICredentialProvider
// {
//     public Task<CredentialResult> GetCredentialsAsync(CancellationToken ct)
//     {
//         var token = FetchTokenFromVault();
//         return Task.FromResult(new CredentialResult(token, DateTimeOffset.UtcNow.AddHours(1)));
//     }
// }

Console.WriteLine("Done.");
