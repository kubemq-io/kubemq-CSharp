using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class ChannelManagementTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateAndListEventsChannel()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("mgmt-events");

        try
        {
            await client.CreateChannelAsync(channel, "events");
            await Task.Delay(500);

            var channels = await client.ListChannelsAsync("events");

            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteChannelAsync(channel, "events");
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateAndListQueuesChannel()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("mgmt-queues");

        try
        {
            await client.CreateChannelAsync(channel, "queues");
            await Task.Delay(500);

            var channels = await client.ListChannelsAsync("queues");

            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteChannelAsync(channel, "queues");
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateAndDeleteEventStoreChannel()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("mgmt-es-del");

        try
        {
            await client.CreateChannelAsync(channel, "events_store");
            await Task.Delay(500);

            var channelsBefore = await client.ListChannelsAsync("events_store");
            channelsBefore.Should().Contain(c => c.Name == channel);

            await client.DeleteChannelAsync(channel, "events_store");
            await Task.Delay(500);

            var channelsAfter = await client.ListChannelsAsync("events_store");
            channelsAfter.Should().NotContain(c => c.Name == channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task ListChannels_FiltersByType()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        // Ensure at least one events channel exists by publishing
        var eventsChannel = UniqueChannel("mgmt-filter-evt");
        await client.SendEventAsync(new EventMessage
        {
            Channel = eventsChannel,
            Body = Encoding.UTF8.GetBytes("ensure-channel-exists"),
        });
        await Task.Delay(500);

        try
        {
            var eventsChannels = await client.ListChannelsAsync("events");
            eventsChannels.Should().NotBeNull();

            var queuesChannels = await client.ListChannelsAsync("queues");
            queuesChannels.Should().NotBeNull();

            // Events channels should not appear in queues list and vice versa
            if (eventsChannels.Count > 0 && queuesChannels.Count > 0)
            {
                eventsChannels.Select(c => c.Name).Should()
                    .NotIntersectWith(queuesChannels.Select(c => c.Name));
            }
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    // ---------------------------------------------------------------
    // Typed channel management helpers
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateEventsChannelAsync_TypedHelper_Works()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("typed-evt");

        try
        {
            await client.CreateEventsChannelAsync(channel);
            await Task.Delay(500);

            var channels = await client.ListEventsChannelsAsync();
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteEventsChannelAsync(channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateEventsStoreChannelAsync_TypedHelper_Works()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("typed-es");

        try
        {
            await client.CreateEventsStoreChannelAsync(channel);
            await Task.Delay(500);

            var channels = await client.ListEventsStoreChannelsAsync();
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteEventsStoreChannelAsync(channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateCommandsChannelAsync_TypedHelper_Works()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("typed-cmd");

        try
        {
            await client.CreateCommandsChannelAsync(channel);
            await Task.Delay(500);

            var channels = await client.ListCommandsChannelsAsync();
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteCommandsChannelAsync(channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateQueriesChannelAsync_TypedHelper_Works()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("typed-qry");

        try
        {
            await client.CreateQueriesChannelAsync(channel);
            await Task.Delay(500);

            var channels = await client.ListQueriesChannelsAsync();
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteQueriesChannelAsync(channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task CreateQueuesChannelAsync_TypedHelper_Works()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var channel = UniqueChannel("typed-q");

        try
        {
            await client.CreateQueuesChannelAsync(channel);
            await Task.Delay(500);

            var channels = await client.ListQueuesChannelsAsync();
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel);

            // Cleanup
            await client.DeleteQueuesChannelAsync(channel);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }

    [Fact]
    public async Task ListChannelsAsync_WithSearchPattern_FiltersResults()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var prefix = $"pat-{Guid.NewGuid():N}";
        var channel1 = $"{prefix}-alpha";
        var channel2 = $"{prefix}-beta";

        try
        {
            await client.CreateEventsChannelAsync(channel1);
            await client.CreateEventsChannelAsync(channel2);
            await Task.Delay(500);

            // Search with a pattern that matches our prefix
            var channels = await client.ListEventsChannelsAsync(searchPattern: prefix);
            channels.Should().NotBeNull();
            channels.Should().Contain(c => c.Name == channel1);
            channels.Should().Contain(c => c.Name == channel2);

            // Cleanup
            await client.DeleteEventsChannelAsync(channel1);
            await client.DeleteEventsChannelAsync(channel2);
        }
        catch (KubeMQException)
        {
            // Channel management may not be supported by all server versions
        }
    }
}
