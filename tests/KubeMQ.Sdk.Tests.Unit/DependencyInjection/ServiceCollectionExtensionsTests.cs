using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KubeMQ.Sdk.Tests.Unit.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKubeMQ_WithAction_RegistersIKubeMQClient()
    {
        var services = new ServiceCollection();

        services.AddKubeMQ(opts => opts.Address = "localhost:50000");
        var provider = services.BuildServiceProvider();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKubeMQClient));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddKubeMQ_WithAction_RegistersKubeMQClientAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KubeMQClient));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddKubeMQ_WithAction_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddKubeMQ_WithAction_ConfiguresOptions()
    {
        var services = new ServiceCollection();

        services.AddKubeMQ(opts =>
        {
            opts.Address = "kubemq:50000";
            opts.ClientId = "test-client";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KubeMQClientOptions>>();
        options.Value.Address.Should().Be("kubemq:50000");
        options.Value.ClientId.Should().Be("test-client");
    }

    [Fact]
    public void AddKubeMQ_WithConfiguration_RegistersServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KubeMQ:Address"] = "config-server:50000",
                ["KubeMQ:ClientId"] = "config-client",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddKubeMQ(config);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KubeMQClientOptions>>();
        options.Value.Address.Should().Be("config-server:50000");
        options.Value.ClientId.Should().Be("config-client");
    }

    [Fact]
    public void AddKubeMQ_WithConfiguration_RegistersKubeMQClientAsSingleton()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KubeMQ:Address"] = "localhost:50000",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddKubeMQ(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KubeMQClient));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddKubeMQ_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKubeMQ_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddKubeMQ((Action<KubeMQClientOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKubeMQ_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddKubeMQ((IConfiguration)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddKubeMQ_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddKubeMQ_WithConfiguration_ReturnsSameServiceCollection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KubeMQ:Address"] = "localhost:50000",
            })
            .Build();

        var services = new ServiceCollection();
        var result = services.AddKubeMQ(config);

        result.Should().BeSameAs(services);
    }
}
