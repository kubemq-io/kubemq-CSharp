using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using KubeMQGrpc = KubeMQ.Grpc;
using InnerRecivedEvent = KubeMQ.Grpc.EventReceive;
using System.Threading;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Subscription;
using KubeMQ.SDK.csharp.Tools;

namespace KubeMQ.SDK.csharp.Events {
    public class Subscriber : GrpcClient {
        private static ILogger logger;

        private readonly BufferBlock<InnerRecivedEvent> _RecivedEvents = new BufferBlock<InnerRecivedEvent> ();

        /// <summary>
        /// Represents a delegate that receive KubeMQ.SDK.csharp.PubSub.EventReceive.
        /// </summary>
        /// <param name="eventReceive">Represents an instance of KubeMQ.SDK.csharp.PubSub.EventReceive</param>
        public delegate void HandleEventDelegate (EventReceive eventReceive);

        /// <summary>
        /// Represents a delegate that receive Exception and return to user.
        /// </summary>
        /// <param name="eventReceive">Represents an Exception that occurred during event receiving </param>
        public delegate void HandleEventErrorDelegate (Exception eventReceive);

        #region C'tor

        /// <summary>
        /// Initialize a new Subscriber to incoming events
        /// Logger will write to default output with suffix KubeMQSDK
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        public Subscriber(): this(null, null) {}

        /// <summary>    
        /// Initialize a new Subscriber to incoming events 
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Subscriber(ILogger plogger): this(null, plogger) {}

        /// <summary>
        /// Initialize a new Subscriber to incoming events 
        /// Logger will write to default output with suffix KubeMQSDK
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Subscriber(string KubeMQAddress, string authToken = null): this(KubeMQAddress, null, authToken) {}

        /// <summary>
        /// Initialize a new Subscriber to incoming events 
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Subscriber(string KubeMQAddress, ILogger plogger, string authToken = null) {
            logger = Logger.InitLogger(plogger, "Subscriber");

            _kubemqAddress = KubeMQAddress;
            this.addAuthToken(authToken);
        }

        #endregion

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest .
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <param name="handler">Method the perform when receiving KubeMQ.SDK.csharp.PubSub.EventReceive .</param>
        /// <param name="errorDelegate">Method the perform when receiving error from KubeMQ.SDK.csharp.PubSub.EventReceive .</param>
        /// <param name="cancellationToken">Optional param if needed to cancel the subscriber ,will receive RPC exception with status canceled through the error Delegate is called.</param>
        /// <returns>A task that represents the Subscribe Request. Possible Exception: fail on ping to kubemq.</returns>
        public void SubscribeToEvents(SubscribeRequest subscribeRequest, HandleEventDelegate handler, HandleEventErrorDelegate errorDelegate, CancellationToken cancellationToken = default(CancellationToken)) {
            ValidateSubscribeRequest(subscribeRequest); // throws ArgumentException
            try {
                this.Ping();
            } catch (Exception pingEx) {
                logger.LogWarning(pingEx, "An exception occurred while sending ping to kubemq");
                throw pingEx;
            }
            var grpcListnerTask = Task.Run((Func < Task > )(async() => {
                while (true) {
                    try {
                        await SubscribeToEvents(subscribeRequest, cancellationToken);
                    } catch (RpcException rpcx) {
                        if (rpcx.StatusCode == StatusCode.Cancelled) {
                            logger.LogWarning(rpcx, $"Cancellation was called ");

                            errorDelegate(rpcx);
                            break;
                        } else {
                            logger.LogWarning(rpcx, $"An RPC exception occurred while listening for events");

                            errorDelegate(rpcx);
                        }
                    } catch (Exception ex) {
                        logger.LogWarning(ex, $"An exception occurred while listening for events");

                        errorDelegate(ex);
                    }
                    await Task.Delay(1000);
                }
            }));

            // send events to end-user
            Task evenSenderTask = Task.Run((Func < Task > )(async() => {
                while (true) {
                    // await for event from queue
                    InnerRecivedEvent innerEvent = await _RecivedEvents.ReceiveAsync();

                    LogIncomingEvent(innerEvent);

                    // Convert KubeMQ.Grpc.Event to outer Event
                    EventReceive evnt = new EventReceive(innerEvent);

                    try {
                        // Activate end-user event handler Delegate
                        handler(evnt);
                    } catch (Exception ex) {
                        logger.LogError(ex, "An exception occurred while handling the event");
                        errorDelegate(ex);
                    }
                }
            }));
        }

        private async Task SubscribeToEvents(SubscribeRequest subscribeRequest, CancellationToken cancellationToken) {
            KubeMQGrpc.Subscribe innerSubscribeRequest = subscribeRequest.ToInnerSubscribeRequest();

            using(var call = GetKubeMQClient().SubscribeToEvents(innerSubscribeRequest, Metadata, null, cancellationToken)) {
                // Wait for event..
                while (await call.ResponseStream.MoveNext()) {
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
        public PingResult Ping() {
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            return rec;

        }

        private void ValidateSubscribeRequest(SubscribeRequest subscribeRequest) {
            if (string.IsNullOrWhiteSpace(subscribeRequest.Channel)) {
                throw new ArgumentException("Parameter is mandatory", "Channel");
            }
            if (!subscribeRequest.IsValideType("Events")) // SubscribeType
            {
                throw new ArgumentException("Invalid Subscribe Type for this Class.", "SubscribeType");
            }
            if (subscribeRequest.SubscribeType == SubscribeType.EventsStore) {
                if (string.IsNullOrWhiteSpace(subscribeRequest.ClientID)) {
                    throw new ArgumentException("Parameter is mandatory for this type.", "ClientID");
                }
                if (subscribeRequest.EventsStoreType == EventsStoreType.Undefined) {
                    throw new ArgumentException("Parameter is mandatory for this type.", "EventsStoreType");
                }
            }
        }

        private void LogIncomingEvent(InnerRecivedEvent eventReceive) {
            try {
                logger.LogInformation($"Subscriber Received Event: EventID:{eventReceive.EventID}, Channel:{eventReceive.Channel}");

            } catch (Exception ex) {
                logger.LogError(ex, "failed in  LogIncomingEvent."); //TODO: Check if this works 
            }
        }

    }
}