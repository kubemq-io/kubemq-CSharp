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
namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Represents a client for sending and subscribing to events in the Events Store.
    /// </summary>
    public class EventsStoreClient
    {
        private kubemqClient _kubemqClient;
        private Connection _cfg;
        private bool _isConnected ;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Transport.Transport _transport;


        /// <summary>
        /// Establishes a connection to the Events Store Client.
        /// </summary>
        /// <param name="cfg">The connection configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// An instance of ConnectAsyncResult indicating whether the connection was successful or not.
        /// </returns>
        public async Task<ConnectAsyncResult> Connect(Connection cfg, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_isConnected)
                {
                    throw new Exception("Client already connected");
                }

                if (cfg == null)
                {
                    throw new ArgumentNullException(nameof(cfg));
                }

                try
                {
                    cfg.Validate();
                    _cfg = cfg;
                    _transport = new Transport.Transport(cfg);
                    await _transport.InitializeAsync(cancellationToken);
                    _isConnected = _transport.IsConnected();
                    _kubemqClient = _transport.KubeMqClient();
                }
                catch (Exception ex)
                {
                    _transport = null;
                    _kubemqClient = null;
                    _isConnected = false;
                    return new ConnectAsyncResult() { IsSuccess = false, ErrorMessage = ex.Message };
                }

                return new ConnectAsyncResult() { IsSuccess = true };
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Sends an event store message to the Kubemq server asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains no value.</returns>
        private async Task SendAsync(EventStore eventToSend, CancellationToken cancellationToken)
        {
            var grpcEvent = eventToSend.Validate().ToKubemqEvent(_cfg.ClientId);
            var result = await _kubemqClient.SendEventAsync(grpcEvent, cancellationToken: cancellationToken);

            if (!result.Sent)
            {
                throw new InvalidOperationException(result.Error);
            }
        }

        private async Task SubscribeAsync(EventsStoreSubscription subscription, CancellationToken cancellationToken)
        {
            subscription.Validate();
            var pbRequest = new pb.Subscribe()
            {
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.EventsStore,
                ClientID = _cfg.ClientId,
                Channel = subscription.Channel,
                Group = subscription.Group,
            };

            switch (subscription.StartAt)
            {
                case StartAtType.StartAtTypeUndefined:
                    throw new ArgumentOutOfRangeException(nameof(subscription.StartAt), subscription.StartAt, null);
                case StartAtType.StartAtTypeFromNew:
                    pbRequest.EventsStoreTypeData = pb.Subscribe.Types.EventsStoreType.StartNewOnly;
                break;
                case StartAtType.StartAtTypeFromFirst:
                    pbRequest.EventsStoreTypeData = pb.Subscribe.Types.EventsStoreType.StartFromFirst;
                break;
                case StartAtType.StartAtTypeFromLast:
                    pbRequest.EventsStoreTypeData = pb.Subscribe.Types.EventsStoreType.StartFromLast;
                break;
                case StartAtType.StartAtTypeFromSequence:
                    pbRequest.EventsStoreTypeData = pb.Subscribe.Types.EventsStoreType.StartAtSequence;
                    pbRequest.EventsStoreTypeValue = subscription.StartAtSequenceValue;
                break;
                case StartAtType.StartAtTypeFromTime:
                    pbRequest.EventsStoreTypeData = pb.Subscribe.Types.EventsStoreType.StartAtTime;
                    pbRequest.EventsStoreTypeValue = (long)(subscription.StartAtTimeValue - new DateTime(1970, 1, 1)).TotalMilliseconds;
                break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subscription.StartAt), subscription.StartAt, null);
            }

            using var stream = _kubemqClient.SubscribeToEvents(pbRequest, null, null, cancellationToken);
            while (await stream.ResponseStream.MoveNext(cancellationToken))
            {
                var receivedEvent = EventStoreReceived.FromEvent(stream.ResponseStream.Current);
                subscription.RaiseOnReceiveEvent(receivedEvent);
            }
        }


        /// <summary>
        /// Subscribes to events in the Events Store.
        /// </summary>
        /// <param name="subscription">
        /// An instance of <see cref="EventsStoreSubscription"/> containing the information needed for the subscription.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the subscription.
        /// </param>
        /// <returns>
        /// An instance of <see cref="SubscribeToEventsStoreResult"/> representing the result of the subscription operation.
        /// </returns>
        public SubscribeToEventsStoreResult Subscribe(EventsStoreSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected )
                {
                    return new SubscribeToEventsStoreResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await SubscribeAsync(subscription, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            subscription.RaiseOnError(ex);
                            if (_cfg.DisableAutoReconnect)
                            {
                                break;
                            }

                            await Task.Delay(_cfg.GetReconnectIntervalDuration(), cancellationToken);
                        }
                    }

                }, cancellationToken);
            }
            catch (Exception e)
            {
                return new SubscribeToEventsStoreResult() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new SubscribeToEventsStoreResult() { IsSuccess = true };
        }

        /// <summary>
        /// Sends an event to the Events Store.
        /// </summary>
        /// <param name="eventStoreToSend">The event to send to the Events Store.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the send operation (optional).</param>
        /// <returns>A SendEventStoreAsyncResult object indicating the result of the send operation.</returns>
        public async Task<SendEventStoreAsyncResult> Send(EventStore eventStoreToSend,CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected)
                {
                    return new SendEventStoreAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                await SendAsync(eventStoreToSend, cancellationToken);
            }
            catch (Exception e)
            {
                return new SendEventStoreAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            return new SendEventStoreAsyncResult() { IsSuccess = true };
        }
        
        /// <summary>
        /// Creates a new events store channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>
        /// An instance of CommonAsyncResult indicating the result of the operation.
        /// </returns>
        public async Task<CommonAsyncResult> Create(string channelName)
        {
            return await CreateDeleteChannel(_kubemqClient,_cfg.ClientId, channelName, "events_store", true);
        }

        /// <summary>
        /// Deletes a channel from the Events Store.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A CommonAsyncResult indicating the result of the operation.</returns>
        public async Task<CommonAsyncResult> Delete(string channelName)
        {
            return await CreateDeleteChannel(_kubemqClient, _cfg.ClientId, channelName, "events_store", false);
        }

        /// <summary>
        /// A method that lists the channels in the Events Store.
        /// </summary>
        /// <param name="search">The search string to filter the channel names.</param>
        /// <returns>
        /// An instance of ListPubSubAsyncResult with the list of PubSubChannels.
        /// </returns>
        public async Task<ListPubSubAsyncResult> List (string search = "") {
            
            return await ListPubSubChannels(_kubemqClient, _cfg.ClientId, search, "events_store");
        }
        
        /// <summary>
        /// Closes the connection to the KubeMQ server.
        /// </summary>
        /// <returns>A <see cref="CloseAsyncResult"/> representing the result of closing the connection.</returns>
        public async Task<CloseAsyncResult> Close()
        {
            try
            {
                await _lock.WaitAsync();
                if (!_isConnected)
                {
                    return new CloseAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }

                if (_transport != null)
                {
                    await _transport.CloseAsync();
                    _transport = null;
                }
                _isConnected = false;
            }
            catch (Exception e)
            {
                return new CloseAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            finally
            {
                _lock.Release();
            }

            return new CloseAsyncResult() { IsSuccess = true };
        }
    }
}