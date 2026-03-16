using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.DependencyInjection;

public class HostedServiceTests
{
    [Fact]
    public void StartAsync_HostedService_IsResolvable()
    {
        var services = new ServiceCollection();
        services.AddKubeMQ(opts => opts.Address = "localhost:50000");
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        var kubeMqHostedService = hostedServices
            .FirstOrDefault(s => s.GetType().Name == "KubeMQConnectionHostedService");

        kubeMqHostedService.Should().NotBeNull("the hosted service should be registered");
    }

    [Fact]
    public void HostedService_IsRegisteredViaAddKubeMQ()
    {
        var services = new ServiceCollection();
        services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType?.Name == "KubeMQConnectionHostedService");

        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void HostedService_IsTransient()
    {
        var services = new ServiceCollection();
        services.AddKubeMQ(opts => opts.Address = "localhost:50000");

        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType?.Name == "KubeMQConnectionHostedService");

        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void StartAsync_WithTestableClient_ClientIsResolvable()
    {
        var services = new ServiceCollection();
        services.AddKubeMQ(opts => opts.Address = "localhost:50000");
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<KubeMQClient>();
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_CallsConnectAsyncOnClient()
    {
        var mockClient = new MockKubeMQClient();
        var hostedService = new KubeMQConnectionHostedService(mockClient);

        await hostedService.StartAsync(CancellationToken.None);

        mockClient.ConnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CallsDisposeAsyncOnClient()
    {
        var mockClient = new MockKubeMQClient();
        var hostedService = new KubeMQConnectionHostedService(mockClient);

        await hostedService.StopAsync(CancellationToken.None);

        mockClient.DisposeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithCancellation_PassesCancellationToken()
    {
        var mockClient = new MockKubeMQClient();
        var hostedService = new KubeMQConnectionHostedService(mockClient);
        using var cts = new CancellationTokenSource();

        await hostedService.StartAsync(cts.Token);

        mockClient.ConnectCalled.Should().BeTrue();
        mockClient.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task StartAsync_ThenStopAsync_BothComplete()
    {
        var mockClient = new MockKubeMQClient();
        var hostedService = new KubeMQConnectionHostedService(mockClient);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        mockClient.ConnectCalled.Should().BeTrue();
        mockClient.DisposeCalled.Should().BeTrue();
    }

    private class MockKubeMQClient : KubeMQClient
    {
        public bool ConnectCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public override Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            LastCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        protected override ValueTask DisposeAsyncCore()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
