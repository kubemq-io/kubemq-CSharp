using KubeMQ.Sdk.Client;

namespace KubeMQ.Sdk.Tests.Integration.Helpers;

public abstract class IntegrationTestBase
{
    protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    protected static KubeMQClient CreateClient(string clientId = "integration-test")
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = clientId,
        };
        return new KubeMQClient(options);
    }

    protected static string UniqueChannel(string testName) =>
        $"test-{testName}-{Guid.NewGuid():N}";
}
