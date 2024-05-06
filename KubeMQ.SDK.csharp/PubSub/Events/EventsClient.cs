using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Transport;
using pb= KubeMQ.Grpc;
using static KubeMQ.Grpc.kubemq;
using static KubeMQ.SDK.csharp.Common.Common;
namespace KubeMQ.SDK.csharp.PubSub.Events
{
    /// Represents a client for sending events and subscribing to events.
    /// /
    public class EventsClient : BaseClient
    {
        /// <summary>
        /// Creates a new channel for events.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the operation.</returns>
        public async Task<CommonAsyncResult> Create(string channelName)
        {
            return await CreateDeleteChannel(Cfg.ClientId, channelName, "events", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the delete operation.</returns>
        public async Task<CommonAsyncResult> Delete(string channelName)
        {
            return await CreateDeleteChannel(Cfg.ClientId, channelName, "events", false);
        }

        /// <summary>
        /// Retrieves the list of PubSub channels filtered by the given search string.
        /// </summary>
        /// <param name="search">The search string used to filter the PubSub channels. (optional)</param>
        /// <returns>The result of the list operation, containing the PubSub channels.</returns>
        /// <remarks>
        /// The list operation retrieves the PubSub channels available on the Kubemq server.
        /// The search string parameter can be used to filter the channels by name.
        /// </remarks>
        public async Task<ListPubSubAsyncResult> List (string search = "") {
            
            return await ListPubSubChannels(Cfg.ClientId, search, "events");
        }

        /// <summary>
        /// Subscribes to events based on the provided subscription information.
        /// </summary>
        /// <param name="subscription">The subscription information specifying the channel and group to subscribe to.</param>
        /// <param name="cancellationToken">The cancellation token to stop the subscription.</param>
        /// <returns>The result of the subscription.</returns>
        public SubscribeToEventsResult Subscribe(EventsSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!IsConnected )
                {
                    return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                subscription.Validate();
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            using var stream = KubemqClient.SubscribeToEvents(subscription.Encode(Cfg.ClientId), null, null, cancellationToken);
                            while (await stream.ResponseStream.MoveNext(cancellationToken))
                            {
                                var receivedEvent = EventReceived.Decode(stream.ResponseStream.Current);
                                subscription.RaiseOnReceiveEvent(receivedEvent);
                            }
                        }
                        catch (Exception ex)
                        {
                            subscription.RaiseOnError(ex);
                            if (Cfg.DisableAutoReconnect)
                            {
                                break;
                            }

                            await Task.Delay(Cfg.GetReconnectIntervalDuration(), cancellationToken);
                        }
                    }

                }, cancellationToken);
            }
            catch (Exception e)
            {
                return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new SubscribeToEventsResult() { IsSuccess = true };
        }

        /// <summary>
        /// Sends an event asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to be sent.</param>
        /// <returns>A <see cref="SendEventResult"/> object indicating whether the operation was successful or not.</returns>
        public async Task<SendEventResult> Send(Event eventToSend)
        {
            try
            {
                if (!IsConnected)
                {
                    return new SendEventResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                eventToSend.Validate();
                var grpcEvent = eventToSend.Validate().Encode(Cfg.ClientId);
                var result = await KubemqClient.SendEventAsync(grpcEvent);

                if (!result.Sent)
                {
                    return new SendEventResult()
                    {
                        ErrorMessage = result.Error,
                        IsSuccess = false
                    };
                }
            }
            catch (Exception e)
            {
                return new SendEventResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            return new SendEventResult() { IsSuccess = true };
        }

        
    }
    
}