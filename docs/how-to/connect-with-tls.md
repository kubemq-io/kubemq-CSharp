# How To: Connect with TLS and mTLS

Configure encrypted connections to KubeMQ using TLS (server verification) or mTLS (mutual certificate authentication).

## TLS — Server Certificate Verification

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    ClientId = "my-service",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem"
    }
});

await client.ConnectAsync();
var info = await client.PingAsync();
Console.WriteLine($"Connected with TLS — server: {info.Version}");
```

## mTLS — Mutual Certificate Authentication

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    ClientId = "my-service",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem",
        CertFile = "/path/to/client.pem",
        KeyFile = "/path/to/client.key"
    }
});

await client.ConnectAsync();
Console.WriteLine($"Connected with mTLS — host: {(await client.PingAsync()).Host}");
```

## TLS with Connection State Monitoring

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

var options = new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    ClientId = "tls-monitored",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem"
    },
    Reconnect = new ReconnectOptions
    {
        Enabled = true,
        MaxAttempts = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(15),
        BackoffMultiplier = 2.0,
    }
};

await using var client = new KubeMQClient(options);

client.StateChanged += (_, args) =>
{
    Console.WriteLine($"[{args.Timestamp:HH:mm:ss}] {args.PreviousState} → {args.CurrentState}");
    if (args.Error is not null)
        Console.WriteLine($"  Error: {args.Error.Message}");
};

try
{
    await client.ConnectAsync();
    Console.WriteLine("TLS connection established");
}
catch (Exception ex)
{
    Console.WriteLine($"TLS connection failed: {ex.Message}");
}
```

## TLS with Auth Token

Combine TLS encryption with token-based authentication:

```csharp
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    AuthToken = "your-jwt-token-here",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem"
    }
});

await client.ConnectAsync();
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `KubeMQConnectionException` with SSL error | CA cert doesn't match server | Use the CA that signed the server certificate |
| `The remote certificate is invalid` | Certificate expired or hostname mismatch | Check cert expiry and server address |
| Connection hangs | TLS disabled for a TLS-required server | Set `Tls.Enabled = true` |
| `FileNotFoundException` | Wrong certificate file path | Verify paths exist and are readable |
| mTLS rejected | Client cert not trusted by server | Ensure server CA includes client cert issuer |
