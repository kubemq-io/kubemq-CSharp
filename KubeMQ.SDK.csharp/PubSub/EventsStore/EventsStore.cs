using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Subscription;
using pb= KubeMQ.Grpc;
using static KubeMQ.Grpc.kubemq;
namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Represents a client for sending and subscribing to events in the Events Store.
    /// </summary>
    internal class EventsStoreClient
    {
        /// <summary>
        /// Represents the client for interacting with the Kubemq gRPC service.
        /// </summary>
        private readonly kubemqClient _kubemqClient;

        /// <summary>
        /// Represents the client ID used for identification in the KubeMQ SDK EventsStoreClient.
        /// </summary>
        private readonly string _clientId;

        /// <summary>
        /// Represents a client for the EventsStore service.
        /// </summary>
        public EventsStoreClient(kubemqClient kubemqClient, string clientId)
        {
            _kubemqClient = kubemqClient;
            _clientId = clientId;
        }

        /// <summary>
        /// Sends an event message to the Kubemq server asynchronously.
        /// </summary>
        /// <param name="eventToSend">The event message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains no value.</returns>
        public async Task SendAsync(EventStore eventToSend, CancellationToken cancellationToken)
        {
            var grpcEvent = eventToSend.Validate().ToKubemqEvent(_clientId);
            var result = await _kubemqClient.SendEventAsync(grpcEvent, cancellationToken: cancellationToken);

            if (!result.Sent)
            {
                throw new InvalidOperationException(result.Error);
            }
        }

        /// <summary>
        /// Subscribes to events from the EventsStore.
        /// </summary>
        /// <param name="subscription">The subscription details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeAsync(EventsStoreSubscription subscription, CancellationToken cancellationToken)
        {
            subscription.Validate();
            var pbRequest = new pb.Subscribe()
            {
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.EventsStore,
                ClientID = _clientId,
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

            using (var stream = _kubemqClient.SubscribeToEvents(pbRequest, null, null, cancellationToken))
            {
                while (await stream.ResponseStream.MoveNext(cancellationToken))
                {
                    var receivedEvent = EventStoreReceived.FromEvent(stream.ResponseStream.Current);
                    subscription.RaiseOnReceiveEvent(receivedEvent);
                }
            }
            
        }
    }
}