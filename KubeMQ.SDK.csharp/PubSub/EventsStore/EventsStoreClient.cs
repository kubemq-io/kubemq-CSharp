using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;

namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Represents a client for sending and subscribing to events in the Events Store.
    /// </summary>
    public class EventsStoreClient : BaseClient
    {

        /// <summary>
        /// Creates a new channel for events.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the operation.</returns>
        public async Task<Result> Create(string channelName)
        {
            return await CreateDeleteChannel(Cfg.ClientId, channelName, "events_store", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the delete operation.</returns>
        public async Task<Result> Delete(string channelName)
        {
            return await CreateDeleteChannel(Cfg.ClientId, channelName, "events_store", false);
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
            
            return await ListPubSubChannels(Cfg.ClientId, search, "events_store");
        }

        /// <summary>
        /// Sends an event store asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to be sent.</param>
        /// <returns>A <see cref="SendEventResult"/> object indicating whether the operation was successful or not.</returns>
        public async Task<Result> Send(EventStore eventToSend)
        {
            try
            {
                if (!IsConnected)
                {
                    return new Result("Client not connected") ;
                }
                eventToSend.Validate();
                var grpcEvent = eventToSend.Validate().Encode(Cfg.ClientId);
                var result = await KubemqClient.SendEventAsync(grpcEvent);

                if (!result.Sent)
                {
                    return new Result(result.Error);
                }
            }
            catch (Exception e)
            {
                return new Result(e);
            }
            return new Result() ;
        }

        /// <summary>
        /// Subscribes to events in the Events Store.
        /// </summary>
        /// <param name="subscription">The subscription details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of subscribing to events.</returns>
        public Result Subscribe(EventsStoreSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!IsConnected )
                {
                    return new Result("Client not connected");
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
                                var receivedEvent = new EventStoreReceived().Decode(stream.ResponseStream.Current);
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
                return new Result(e) ;
            }

            return new Result() ;
        }
     
    }
}