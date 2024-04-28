using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Subscription;
using pb= KubeMQ.Grpc;
using static KubeMQ.Grpc.kubemq;
namespace KubeMQ.SDK.csharp.Unified.PubSub.Events
{
    /// Represents a client for sending events and subscribing to events.
    /// /
    internal class EventsClient
    {
        /// <summary>
        /// Represents an instance of the Kubemq client for sending and receiving events.
        /// </summary>
        private readonly kubemqClient _kubemqClient;

        /// <summary>
        /// Represents the unique identifier for the client in the KubeMQ system. </summary> <remarks>
        /// The _clientId variable is used to identify the client when sending events or subscribing to events. </remarks>
        /// /
        private readonly string _clientId;

        /// <summary>
        /// Provides methods for sending and subscribing to events.
        /// </summary>
        public EventsClient(kubemqClient kubemqClient, string clientId)
        {
            _kubemqClient = kubemqClient;
            _clientId = clientId;
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

            using (var stream = _kubemqClient.SubscribeToEvents(pbRequest, null, null, cancellationToken))
            {
                while (await stream.ResponseStream.MoveNext(cancellationToken))
                {
                    var receivedEvent = EventReceived.FromEvent(stream.ResponseStream.Current);
                    subscription.RaiseOnReceiveEvent(receivedEvent);
                }
            }
            
        }
    }
}