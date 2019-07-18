using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using KubeMQGrpc = KubeMQ.Grpc;
using InnerRecivedEvent = KubeMQ.Grpc.EventReceive;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Tools;
using KubeMQ.SDK.csharp.Subscription;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Events
{
    public class Subscriber : GrpcClient
    {
        private static ILogger logger;

        private readonly BufferBlock<InnerRecivedEvent> _RecivedEvents = new BufferBlock<InnerRecivedEvent>();

        /// <summary>
        /// Represents a delegate that receive KubeMQ.SDK.csharp.PubSub.EventReceive.
        /// </summary>
        /// <param name="eventReceive">Represents an instance of KubeMQ.SDK.csharp.PubSub.EventReceive</param>
        public delegate void HandleEventDelegate(EventReceive eventReceive);

        #region C'tor

        /// <summary>
        /// Initialize a new Subscriber to incoming events
        /// Logger will write to default output with suffix KubeMQSDK
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        public Subscriber() : this(null, null) { }

        /// <summary>    
        /// Initialize a new Subscriber to incoming events 
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Subscriber(ILogger plogger) : this(null, plogger) { }

        /// <summary>
        /// Initialize a new Subscriber to incoming events 
        /// Logger will write to default output with suffix KubeMQSDK
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        public Subscriber(string KubeMQAddress) : this(KubeMQAddress, null) { }

        /// <summary>
        /// Initialize a new Subscriber to incoming events 
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Subscriber(string KubeMQAddress, ILogger plogger)
        {
            logger = Logger.InitLogger(plogger, "Subscriber");

            _kubemqAddress = KubeMQAddress;
        }

        #endregion

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest .
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <param name="handler">Method the perform when receiving KubeMQ.SDK.csharp.PubSub.EventReceive .</param>
        /// <returns>A task that represents the Subscribe Request. Possible Exception: fail on ping to kubemq.</returns>
        public void SubscribeToEvents(SubscribeRequest subscribeRequest, HandleEventDelegate handler)
        {
            ValidateSubscribeRequest(subscribeRequest);// throws ArgumentException
            try
            {
                this.Ping();
            }
            catch (Exception pingEx)
            {
                logger.LogWarning(pingEx, "n exception occurred while sending ping to kubemq");
                throw pingEx;
            }


            var grpcListnerTask = Task.Run((Func<Task>)(async () =>
            {
                while (true)
                {
                    try
                    {
                        await SubscribeToEvents(subscribeRequest);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"An exception occurred while listening for events");
                    }
                    await Task.Delay(1000);
                }
            }));

            // send events to end-user
            Task evenSenderTask = Task.Run((Func<Task>)(async () =>
            {
                while (true)
                {
                    // await for event from queue
                    InnerRecivedEvent innerEvent = await _RecivedEvents.ReceiveAsync();

                    LogIncomingEvent(innerEvent);

                    // Convert KubeMQ.Grpc.Event to outer Event
                    EventReceive evnt = new EventReceive(innerEvent);

                    try
                    {
                        // Activate end-user event handler Delegate
                        handler(evnt);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An exception occurred while handling the event");
                    }
                }
            }));
        }

        private async Task SubscribeToEvents(SubscribeRequest subscribeRequest)
        {
            KubeMQGrpc.Subscribe innerSubscribeRequest = subscribeRequest.ToInnerSubscribeRequest();
            using (var call = GetKubeMQClient().SubscribeToEvents(innerSubscribeRequest, _metadata))
            {
                // Wait for event..
                while (await call.ResponseStream.MoveNext())
                {
                    // Received a event
                    InnerRecivedEvent eventReceive = call.ResponseStream.Current;

                    // add event to queue
                    _RecivedEvents.Post(eventReceive);
                    LogIncomingEvent(eventReceive);
                }
            }
        }

        /// <summary>
        /// Ping check Kubemq response.
        /// </summary>
        /// <returns>ping status of kubemq.</returns>
        public PingResult Ping()
        {
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            return rec;

        }

        private void ValidateSubscribeRequest(SubscribeRequest subscribeRequest)
        {
            if (string.IsNullOrWhiteSpace(subscribeRequest.Channel))
            {
                throw new ArgumentException("Parameter is mandatory", "Channel");
            }
            if (!subscribeRequest.IsValideType("Events"))// SubscribeType
            {
                throw new ArgumentException("Invalid Subscribe Type for this Class.", "SubscribeType");
            }
            if (subscribeRequest.SubscribeType == SubscribeType.EventsStore)
            {
                if (string.IsNullOrWhiteSpace(subscribeRequest.ClientID))
                {
                    throw new ArgumentException("Parameter is mandatory for this type.", "ClientID");
                }
                if (subscribeRequest.EventsStoreType == EventsStoreType.Undefined)
                {
                    throw new ArgumentException("Parameter is mandatory for this type.", "EventsStoreType");
                }
            }
        }

        private void LogIncomingEvent(InnerRecivedEvent eventReceive)
        {
            try
            {
                object objBody = Converter.FromByteArray(eventReceive.Body.ToByteArray());//TODO: convert message, Check if this works
                //string objBody = "";
                logger.LogInformation($"Subscriber Received Event: EventID:'{eventReceive.EventID}', Channel:'{eventReceive.Channel}', Body:'{objBody}'");

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "failed in  LogIncomingEvent.");//TODO: Check if this works 
            }
        }


    }
}
