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

        private bool _isConnected ;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Transport.Transport _transport;
        private string _clientId;

        public async Task<ConnectAsyncResult> ConnectAsync(Connection cfg, CancellationToken cancellationToken)
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
                    _transport = new Transport.Transport(cfg);
                    await _transport.InitializeAsync(cancellationToken);
                    _isConnected = _transport.IsConnected();
                    _kubemqClient = _transport.KubeMqClient();
                    _clientId = cfg.ClientId;
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

        
        public async Task<PingAsyncResult> PingAsync(CancellationToken cancellationToken)
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
        public async Task SendAsync(Event eventToSend, CancellationToken cancellationToken)
        {
            var grpcEvent = eventToSend.Validate().ToKubemqEvent(_clientId);
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
            return await CreateDeleteChannel(_kubemqClient,_clientId, channelName, "events", true);
        }


        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> representing the result of the delete operation.</returns>
        public async Task<CommonAsyncResult> Delete(string channelName)
        {
            return await CreateDeleteChannel(_kubemqClient, _clientId, channelName, "events", false);
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
            
            return await ListPubSubChannels(_kubemqClient, _clientId, search, "events");
        }

        /// <summary>
        /// Subscribes to events based on the provided <see cref="EventsSubscription"/>.
        /// </summary>
        /// <param name="subscription">The <see cref="EventsSubscription"/> object containing the channel and group to subscribe to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SubscribeAsync(EventsSubscription subscription, CancellationToken cancellationToken)
        {
            subscription.Validate();
            var pbRequest = new pb.Subscribe()
            {
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.Events,
                ClientID = _clientId,
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
    }
}