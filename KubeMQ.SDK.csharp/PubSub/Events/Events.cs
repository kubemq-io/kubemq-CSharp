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
    public class EventsClient
    {
        /// <summary>
        /// Represents an instance of the Kubemq client for sending and receiving events.
        /// </summary>
        private kubemqClient _kubemqClient;
        private Connection _cfg;
        private bool _isConnected ;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Transport.Transport _transport;


        /// <summary>
        /// Connects the client to the KubeMQ server using the specified connection configuration.
        /// </summary>
        /// <param name="cfg">The connection configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the connect operation.</returns>
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
        /// Sends a ping request to the server to check the connectivity.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken to cancel the request.</param>
        /// <returns>A PingAsyncResult containing the result of the operation.</returns>
        public async Task<PingAsyncResult> Ping(CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected )
                {
                    return new PingAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                ServerInfo result = await _transport.PingAsync(cancellationToken);
                return new PingAsyncResult() { IsSuccess = true, ServerInfo = result };
            }
            catch (Exception ex)
            {
                return new PingAsyncResult() { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }
        /// <summary>
        /// Sends an event asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendAsync(Event eventToSend, CancellationToken cancellationToken)
        {
            var grpcEvent = eventToSend.Validate().ToKubemqEvent(_cfg.ClientId);
            var result = await _kubemqClient.SendEventAsync(grpcEvent, cancellationToken: cancellationToken);

            if (!result.Sent)
            {
                throw new InvalidOperationException(result.Error);
            }
        }


        /// <summary>
        /// Creates a new channel for events.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the operation.</returns>
        public async Task<CommonAsyncResult> Create(string channelName)
        {
            return await CreateDeleteChannel(_kubemqClient,_cfg.ClientId, channelName, "events", true);
        }


        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the delete operation.</returns>
        public async Task<CommonAsyncResult> Delete(string channelName)
        {
            return await CreateDeleteChannel(_kubemqClient, _cfg.ClientId, channelName, "events", false);
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
            
            return await ListPubSubChannels(_kubemqClient, _cfg.ClientId, search, "events");
        }


        private async Task SubscribeAsync(EventsSubscription subscription, CancellationToken cancellationToken)
        {
            subscription.Validate();
            var pbRequest = new pb.Subscribe()
            {
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.Events,
                ClientID = _cfg.ClientId,
                Channel = subscription.Channel,
                Group = subscription.Group
            };

            using var stream = _kubemqClient.SubscribeToEvents(pbRequest, null, null, cancellationToken);
            while (await stream.ResponseStream.MoveNext(cancellationToken))
            {
                var receivedEvent = EventReceived.FromEvent(stream.ResponseStream.Current);
                subscription.RaiseOnReceiveEvent(receivedEvent);
            }
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
                if (!_isConnected )
                {
                    return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
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
                return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new SubscribeToEventsResult() { IsSuccess = true };
        }

        /// <summary>
        /// Sends an event asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to be sent.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>A <see cref="SendEventAsyncResult"/> object indicating whether the operation was successful or not.</returns>
        public async Task<SendEventAsyncResult> Send(Event eventToSend,CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected)
                {
                    return new SendEventAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                await SendAsync(eventToSend, cancellationToken);
            }
            catch (Exception e)
            {
                return new SendEventAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            return new SendEventAsyncResult() { IsSuccess = true };
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