using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Sdk.Client;
using Microsoft.Extensions.Hosting;

namespace KubeMQ.Sdk.DependencyInjection;

/// <summary>
/// Hosted service that connects the <see cref="KubeMQClient"/> on application startup
/// and disposes it on shutdown.
/// </summary>
internal sealed class KubeMQConnectionHostedService : IHostedService
{
    private readonly KubeMQClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="KubeMQConnectionHostedService"/> class.
    /// </summary>
    /// <param name="client">The KubeMQ client to manage.</param>
    public KubeMQConnectionHostedService(KubeMQClient client) => _client = client;

    /// <summary>Connects the client on application startup.</summary>
    /// <param name="cancellationToken">Token to cancel startup.</param>
    /// <returns>A task representing the asynchronous connect operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken) =>
        _client.ConnectAsync(cancellationToken);

    /// <summary>Disposes the client on application shutdown.</summary>
    /// <param name="cancellationToken">Token to cancel shutdown.</param>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken) =>
        await _client.DisposeAsync().ConfigureAwait(false);
}
