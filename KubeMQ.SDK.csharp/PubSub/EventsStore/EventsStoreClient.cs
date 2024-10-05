using System;
using System.Collections.Generic;
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
        private readonly List<CancellationTokenSource> _subscriptionTokens = new List<CancellationTokenSource>();
        /// <summary>
        /// Creates a new channel for events.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A <see cref="Result"/> representing the result of the operation.</returns>
        public Task<Result> Create(string channelName)
        {
            return CreateDeleteChannel(Cfg.ClientId, channelName, "events_store", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="Result"/> representing the result of the delete operation.</returns>
        public Task<Result> Delete(string channelName)
        {
            return CreateDeleteChannel(Cfg.ClientId, channelName, "events_store", false);
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
        public Task<ListPubSubAsyncResult> List (string search = "") {
            
            return ListPubSubChannels(Cfg.ClientId, search, "events_store");
        }

        /// <summary>
        /// Sends an event store asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to be sent.</param>
        /// <returns>A <see cref="Result"/> object indicating whether the operation was successful or not.</returns>
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
        /// Subscribes to incoming queries.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <returns>The result of subscribing to queries. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>
        public Result Subscribe(EventsStoreSubscription subscription)
        {
            CancellationTokenSource token = new CancellationTokenSource();
            return _Subscribe(subscription,token);
        }
        /// <summary>
        /// Subscribes to incoming queries.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <param name="cancellationToken">Cancellation token to stop the subscription.</param>
        /// <returns>The result of subscribing to queries. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>

        public Result Subscribe(EventsStoreSubscription subscription,CancellationTokenSource cancellationToken)
        {
            return _Subscribe(subscription,cancellationToken);
        }
        /// <summary>
        /// Subscribes to events in the Events Store.
        /// </summary>
        /// <param name="subscription">The subscription details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of subscribing to events.</returns>
        private Result _Subscribe(EventsStoreSubscription subscription, CancellationTokenSource cancellationToken)
        {
            try
            {
                if (!IsConnected )
                {
                    return new Result("Client not connected");
                }
                subscription.Validate();
                lock (_subscriptionTokens)
                {
                    _subscriptionTokens.Add(cancellationToken);
                }
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            using var stream = KubemqClient.SubscribeToEvents(subscription.Encode(Cfg.ClientId), null, null, cancellationToken.Token);
                            while (await stream.ResponseStream.MoveNext(cancellationToken.Token))
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

                            await Task.Delay(Cfg.GetReconnectIntervalDuration(), cancellationToken.Token);
                        }
                        finally
                        {
                            lock (_subscriptionTokens)
                            {
                                _subscriptionTokens.Remove(cancellationToken);
                            } 
                        }
                    }

                }, cancellationToken.Token);
            }
            catch (Exception e)
            {
                return new Result(e) ;
            }

            return new Result() ;
        }
     
        public async Task<Result> Close()
        {
            // Cancel all active subscriptions
            lock (_subscriptionTokens)
            {
                foreach (var cts in _subscriptionTokens)
                {
                    cts.Cancel();
                }
                _subscriptionTokens.Clear();
            }

            // Call the base class Close method
            return await base.CloseClient();
        }
        
    }
    
}