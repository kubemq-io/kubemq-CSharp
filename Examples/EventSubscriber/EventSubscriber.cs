using CommonExample;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Events;
using KubeMQ.SDK.csharp.Subscription;

namespace EventSubscriber
{
    public class EventSubscriber : BaseExample
    {
        private Subscriber subscriber;

        public EventSubscriber() :base("EventSubscriber")
        {
            try
            {
                SubcribeToEventsWithoutStore();
                SubcribeToEventsWithStore();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Responder SubscribeToEvents EXCEPTION:{ex.Message}");
            }
            Console.ReadKey();

        }

        private void SubcribeToEventsWithStore()
        {
            subscriber = new Subscriber(logger);
            SubscribeRequest subscribeRequest = CreateSubscribeRequest(SubscribeType.EventsStore,EventsStoreType.StartAtSequence,2);
            subscriber.SubscribeToEvents(subscribeRequest, HandleIncomingEvents, HandleIncomingError);
        }

        public void SubcribeToEventsWithoutStore()
        {
            subscriber = new Subscriber(logger);
            SubscribeRequest subscribeRequest = CreateSubscribeRequest(SubscribeType.Events);
            subscriber.SubscribeToEvents(subscribeRequest, HandleIncomingEvents, HandleIncomingError);

        }

        private void HandleIncomingEvents(EventReceive @event)
        {
            if (@event != null)
            {
                string strMsg = string.Empty;
                object body = KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(@event.Body);

                logger.LogInformation($"Subscriber Received Event: Metadata:'{@event.Metadata}', Channel:'{@event.Channel}', Body:'{strMsg}'");
            }
        }

        private void HandleIncomingError(Exception ex)
        {
            logger.LogWarning($"Received Exception :{ex}");
        }

    }
}
