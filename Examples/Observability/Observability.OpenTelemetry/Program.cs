// KubeMQ .NET SDK — Observability: OpenTelemetry Integration
//
// This example demonstrates how the SDK exposes tracing and metrics via
// System.Diagnostics.ActivitySource and System.Diagnostics.Metrics.Meter.
//
// The SDK uses "KubeMQ.Sdk" as the ActivitySource and Meter name.
// No OpenTelemetry NuGet dependency is required in the core SDK —
// the OTel collector picks up traces/metrics automatically when configured.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run
//
// To export to an OTel collector, add OpenTelemetry packages to your app:
//   dotnet add package OpenTelemetry.Extensions.Hosting
//   dotnet add package OpenTelemetry.Exporter.Console
//   dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Diagnostics;
using System.Text;

// Listen for KubeMQ SDK activities (traces)
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "KubeMQ.Sdk",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity =>
    {
        Console.WriteLine($"[Trace] Started: {activity.OperationName}");
        foreach (var tag in activity.Tags)
        {
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
        }
    },
    ActivityStopped = activity =>
    {
        Console.WriteLine($"[Trace] Stopped: {activity.OperationName} ({activity.Duration.TotalMilliseconds:F1}ms)");
    }
};
ActivitySource.AddActivityListener(listener);

// Create and use the client — traces are emitted automatically
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-observability-open-telemetry-client",
});
await client.ConnectAsync();

Console.WriteLine("\nPublishing events (traces will be logged above)...\n");

for (var i = 1; i <= 3; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "csharp-observability.open-telemetry",
        Body = Encoding.UTF8.GetBytes($"Traced event #{i}")
    });
}

Console.WriteLine("\nDone. In production, configure OpenTelemetry SDK to export to your collector.");
