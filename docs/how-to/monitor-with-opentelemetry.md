# How To: Monitor with OpenTelemetry

Instrument KubeMQ operations with distributed tracing and metrics using `System.Diagnostics` and OpenTelemetry.

## How It Works

The SDK uses `System.Diagnostics.ActivitySource` (named `"KubeMQ.Sdk"`) for tracing and `System.Diagnostics.Metrics.Meter` for metrics. No OpenTelemetry NuGet dependency is required in the SDK — the OTel collector picks up traces and metrics automatically when configured in your application.

## Dependencies

Add to your project:

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## Tracing Setup

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Text;

// 1. Configure OTel tracing to listen for KubeMQ.Sdk activities
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("order-service"))
    .AddSource("KubeMQ.Sdk")
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
    .AddConsoleExporter()
    .Build();

// 2. Create the KubeMQ client — tracing is automatic
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "traced-service",
});
await client.ConnectAsync();

// 3. Operations produce spans automatically
await client.PublishEventAsync(new EventMessage
{
    Channel = "orders.created",
    Body = Encoding.UTF8.GetBytes("{\"orderId\":\"ORD-001\"}"),
});

Console.WriteLine("Event published — check your OTel collector for traces");
```

## Minimal Tracing with ActivityListener

For quick debugging without the full OTel SDK:

```csharp
using System.Diagnostics;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "KubeMQ.Sdk",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity =>
    {
        Console.WriteLine($"[Trace] {activity.OperationName}");
        foreach (var tag in activity.Tags)
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
    },
    ActivityStopped = activity =>
    {
        Console.WriteLine($"[Trace] {activity.OperationName} completed in {activity.Duration.TotalMilliseconds:F1}ms");
    },
};
ActivitySource.AddActivityListener(listener);

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "debug-traced",
});
await client.ConnectAsync();

for (int i = 1; i <= 3; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "demo.traced",
        Body = Encoding.UTF8.GetBytes($"Traced event #{i}"),
    });
}
```

## Metrics Setup

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("KubeMQ.Sdk")
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
    .AddConsoleExporter()
    .Build();
```

## Span Attributes

Every SDK span includes these semantic attributes:

| Attribute | Example |
|---|---|
| `messaging.system` | `kubemq` |
| `messaging.operation.name` | `publish`, `receive`, `send` |
| `messaging.destination.name` | `orders.created` |
| `messaging.client.id` | `traced-service` |
| `server.address` | `localhost` |
| `server.port` | `50000` |

## Metrics Emitted

| Metric | Type | Description |
|---|---|---|
| `kubemq.messages.sent` | Counter | Messages published/sent |
| `kubemq.messages.received` | Counter | Messages consumed |
| `kubemq.operation.duration` | Histogram | Operation latency (seconds) |
| `kubemq.connection.state_changes` | Counter | Connection state transitions |

## ASP.NET Core Integration

For hosted services, wire up OTel in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("KubeMQ.Sdk")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("KubeMQ.Sdk")
        .AddOtlpExporter());

var app = builder.Build();
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No spans in collector | `AddSource("KubeMQ.Sdk")` missing | Add the source name to your tracer provider |
| Spans appear locally but not exported | Exporter not configured | Add `AddOtlpExporter()` or `AddConsoleExporter()` |
| Metrics not recorded | Meter not registered | Add `AddMeter("KubeMQ.Sdk")` to meter provider |
| High-cardinality warnings | Too many unique channel names | Use structured channel naming (e.g., `orders.{type}`) |
