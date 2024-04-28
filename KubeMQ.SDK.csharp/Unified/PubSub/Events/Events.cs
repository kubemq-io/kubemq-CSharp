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
    public class EventsClient
    {
        private readonly kubemqClient _kubemqClient;
        private readonly string _clientId;

        public EventsClient(kubemqClient kubemqClient, string clientId)
        {
            _kubemqClient = kubemqClient;
            _clientId = clientId;
        }

        public async Task SendAsync(Event eventToSend, CancellationToken cancellationToken)
        {
            var grpcEvent = eventToSend.ToKubemqEvent(_clientId);
            var result = await _kubemqClient.SendEventAsync(grpcEvent, cancellationToken: cancellationToken);

            if (!result.Sent)
            {
                throw new InvalidOperationException(result.Error);
            }
        }

        public async Task SubscribeAsync(EventsSubscription subscription, CancellationToken cancellationToken)
        {
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