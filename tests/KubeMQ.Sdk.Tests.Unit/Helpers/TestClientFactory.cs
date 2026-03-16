using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Helpers;

internal static class TestClientFactory
{
    internal static KubeMQClientOptions DefaultOptions() => new()
    {
        Address = "localhost:50000",
        ClientId = "test-client",
        Retry = new() { Enabled = false },
    };

    internal static (KubeMQClient Client, Mock<ITransport> Transport) Create(
        KubeMQClientOptions? options = null)
    {
        var transport = new Mock<ITransport>();
        var opts = options ?? DefaultOptions();

        var client = new KubeMQClient(opts, transport.Object, NullLogger.Instance);
        return (client, transport);
    }
}
