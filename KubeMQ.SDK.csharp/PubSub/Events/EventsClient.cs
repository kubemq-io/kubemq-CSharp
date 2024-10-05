using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Common;
using System.Collections.Concurrent;
using System.Collections.Generic;
using KubeMQ.SDK.csharp.Results;
namespace KubeMQ.SDK.csharp.PubSub.Events
{
    /// Represents a client for sending events and subscribing to events.
    /// /
    public class EventsClient : BaseClient
    {
        private readonly BlockingCollection<Event> _sendQueue =
            new BlockingCollection<Event>();
        private readonly List<CancellationTokenSource> _subscriptionTokens = new List<CancellationTokenSource>();
        
        /// <summary>
        /// Creates a new channel for events.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A <see cref="Result"/> representing the result of the operation.</returns>
        public Task<Result> Create(string channelName)
        {
            return CreateDeleteChannel(Cfg.ClientId, channelName, "events", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="Result"/> representing the result of the delete operation.</returns>
        public Task<Result> Delete(string channelName)
        {
            return CreateDeleteChannel(Cfg.ClientId, channelName, "events", false);
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
        /// Subscribes to incoming events.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <returns>The result of subscribing to events. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>
        public Result Subscribe(EventsSubscription subscription)
        {
            CancellationTokenSource token = new CancellationTokenSource();
            return _Subscribe(subscription,token);
        }
        /// <summary>
        /// Subscribes to incoming events.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <param name="cancellationToken">Cancellation token to stop the subscription.</param>
        /// <returns>The result of subscribing to events. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>

        public Result Subscribe(EventsSubscription subscription,CancellationTokenSource cancellationToken)
        {
            return _Subscribe(subscription,cancellationToken);
        }
        private Result _Subscribe(EventsSubscription subscription, CancellationTokenSource cancellationToken)
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
                return new Result() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new Result() { IsSuccess = true };
        }

        /// <summary>
        /// Sends an event asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to be sent.</param>
        /// <returns>A <see cref="Result"/> object indicating whether the operation was successful or not.</returns>
        public async Task<Result> Send(Event eventToSend)
        {
            try
            {
                if (!IsConnected)
                {
                    return new Result("Client not connected");
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
                return new Result(e) ;
            }

            return new Result();
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