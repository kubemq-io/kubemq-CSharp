using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Basic;
using Microsoft.Extensions.Logging;
using KubeMQGrpc = KubeMQ.Grpc;
using InnerRequest = KubeMQ.Grpc.Request;
using InnerResponse = KubeMQ.Grpc.Response;
using System.Threading;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Subscription;
using KubeMQ.SDK.csharp.Tools;

namespace KubeMQ.SDK.csharp.CommandQuery {
    /// <summary>
    /// An instance that responsible on receiving request from the kubeMQ.
    /// </summary>
    public class Responder : GrpcClient {
        private static ILogger logger;
        private string clientId;
        private readonly Queue<InnerRequest> _receivedRequests = new Queue<InnerRequest>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Represents a delegate that receive KubeMQ.SDK.csharp.RequestReply.RequestReceive.
        /// </summary>
        /// <param name="request">Represents an instance of KubeMQ.SDK.csharp.RequestReply.Responder .</param>
        /// <returns></returns>
        public delegate Response RespondDelegate (RequestReceive request);

        #region C'tor
        /// <summary>
        /// Represents a delegate that receive Exception and return to user.
        /// </summary>
        /// <param name="eventReceive">Represents an Exception that occurred during CommandQuery receiving </param>
        public delegate void HandleCommandQueryErrorDelegate(Exception eventReceive);

        /// <summary>
        /// Initialize a new Responder to subscribe to Response  
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="clientId">Represents the responder ID that a responder will subscribe and send responses.</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Responder(string KubeMQAddress, string clientId="", ILogger plogger=null, string authToken=null) {
            _kubemqAddress = KubeMQAddress;
            this.clientId = clientId;
            logger = Logger.InitLogger(plogger, "Responder");
            this.addAuthToken(authToken);
        }
        #endregion

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest with RespondDelegate .
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <param name="handler">Method the perform when receiving KubeMQ.SDK.csharp.RequestReplay.RequestReceive </param>
        /// <param name="errorDelegate">Method the perform when receiving error from KubeMQ.SDK.csharp.CommandQuery .</param>
        /// <param name="cancellationToken">Optional param if needed to cancel the subscriber ,will receive RPC exception with status canceled through the error Delegate is called.</param>
        /// <returns>A task that represents the Subscribe Request. Possible Exception: fail on ping to kubemq.</returns>
        public void SubscribeToRequests(SubscribeRequest subscribeRequest, RespondDelegate handler, HandleCommandQueryErrorDelegate errorDelegate, CancellationToken cancellationToken = default(CancellationToken)) {
            ValidateSubscribeRequest(subscribeRequest); // throws ArgumentException
            Task grpcListnerTask = Task.Run((Func < Task > )(async() => {
                while (true) {
                    try {
                        await SubscribeToRequests(subscribeRequest, cancellationToken);
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
                        logger.LogWarning(ex, $"An exception occurred while receiving request");
                        errorDelegate(ex);
                    }
                    await Task.Delay(1000);
                }
            }));

            // send requests to end-user and send his response via GRPC
            Task handelAndRespondTask = Task.Run((Func < Task > )(async() => {
                while (true) {
                    InnerRequest innerRequest;
                    lock (_lockObject)
                    {
                        while (_receivedRequests.Count == 0)
                        {
                            Monitor.Wait(_lockObject);
                        }
                        innerRequest = _receivedRequests.Dequeue();
                    }

                    // Convert KubeMQ.Grpc.Request to RequestReceive
                    RequestReceive request = new RequestReceive(innerRequest);

                    try {
                        // Activate end-user request handler and receive the response
                        Response response = handler(request);
                        
                        response.ClientID= this.clientId;
                        // Convert
                        InnerResponse innerResponse = response.Convert();
                        // Send Response via GRPC
                        GetKubeMQClient().SendResponse(innerResponse, Metadata);
                    } catch (Exception ex) {
                        logger.LogError(ex, "An exception occurred while handling the response");
                        errorDelegate(ex);
                    }
                }
            }));
        }

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest.
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <param name="cancellationToken">Optional param if needed to cancel the subscriber ,will receive RPC exception with status canceled through the error Delegate is called.</param>
        /// <returns>A task that represents the Subscribe Request.</returns>
        private async Task SubscribeToRequests(SubscribeRequest subscribeRequest, CancellationToken cancellationToken) {
            KubeMQGrpc.Subscribe innerSubscribeRequest = subscribeRequest.ToInnerSubscribeRequest();

            using(var call = GetKubeMQClient().SubscribeToRequests(innerSubscribeRequest, Metadata, null, cancellationToken)) {
                // await for requests form GRPC stream.
                while (await call.ResponseStream.MoveNext()) {
                    // Received requests form GRPC stream.
                    InnerRequest request = call.ResponseStream.Current;

                    // Add request to queue
                    lock (_lockObject)
                    {
                        _receivedRequests.Enqueue(request);
                        Monitor.PulseAll(_lockObject);
                    }
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

            if (!subscribeRequest.IsValideType("CommandQuery")) // SubscribeType
            {
                throw new ArgumentException("Invalid Subscribe Type for this Class.", "SubscribeType");
            }
        }
    }
}