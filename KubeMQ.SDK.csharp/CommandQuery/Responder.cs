using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using KubeMQ.SDK.csharp.Basic;
using KubeMQGrpc = KubeMQ.Grpc;
using InnerRequest = KubeMQ.Grpc.Request;
using InnerResponse = KubeMQ.Grpc.Response;
using KubeMQ.SDK.csharp.Tools;
using KubeMQ.SDK.csharp.Subscription;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// An instance that responsible on receiving request from the kubeMQ.
    /// </summary>
    public class Responder : GrpcClient
    {
        private static ILogger logger;
        private readonly BufferBlock<InnerRequest> _RecivedRequests = new BufferBlock<InnerRequest>();
        //private readonly BufferBlock<InnerResponse> _ResponsesToSend = new BufferBlock<InnerResponse>();

        /// <summary>
        /// Represents a delegate that receive KubeMQ.SDK.csharp.RequestReply.RequestReceive.
        /// </summary>
        /// <param name="request">Represents an instance of KubeMQ.SDK.csharp.RequestReply.Responder .</param>
        /// <returns></returns>
        public delegate Response RespondDelegate(RequestReceive request);

        #region C'tor
        /// <summary>
        /// Initialize a new Responder to subscribe to Response
        /// Logger will write to default output with suffix KubeMQSDK
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        public Responder() : this(null, null) { }

        /// <summary>    
        /// Initialize a new Responder to subscribe to Response    
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Responder(ILogger plogger) : this(null, plogger) { }

        /// <summary>
        /// Initialize a new Responder to subscribe to Response  
        /// Logger will write to default output with suffix KubeMQSDK
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        public Responder(string KubeMQAddress) : this(KubeMQAddress, null) { }

        /// <summary>
        /// Initialize a new Responder to subscribe to Response  
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Responder(string KubeMQAddress, ILogger plogger)
        {
            _kubemqAddress = KubeMQAddress;

            logger = Logger.InitLogger(plogger, "Responder");
        }
        #endregion

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest with RespondDelegate .
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <param name="handler">Method the perform when receiving KubeMQ.SDK.csharp.RequestReplay.RequestReceive </param>
        /// <returns>A task that represents the Subscribe Request.</returns>
        public void SubscribeToRequests(SubscribeRequest subscribeRequest, RespondDelegate handler)
        {
            ValidateSubscribeRequest(subscribeRequest);// throws ArgumentException

            Task grpcListnerTask = Task.Run((Func<Task>)(async () =>
            {
                while (true)
                {
                    try
                    {
                        await SubscribeToRequests(subscribeRequest);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"An exception occurred while listening for request");
                    }
                    await Task.Delay(1000);
                }
            }));


            // send requests to end-user and send his response via GRPC
            Task handelAndRespondTask = Task.Run((Func<Task>)(async () =>
            {
                while (true)
                {
                    // await for Request from queue
                    InnerRequest innerRequest = await _RecivedRequests.ReceiveAsync();

                    // Convert KubeMQ.Grpc.Request to RequestReceive
                    RequestReceive request = new RequestReceive(innerRequest);
                    
                    try
                    {
                        // Activate end-user request handler and receive the response
                        Response response = handler(request);
                        
                        // Convert
                        InnerResponse innerResponse = response.Convert();

                        LogResponse(innerResponse);

                        // Send Response via GRPC
                        GetKubeMQClient().SendResponse(innerResponse, _metadata);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An exception occurred while handling the response");
                    }
                }
            }));
        }

        /// <summary>
        /// Register to kubeMQ Channel using KubeMQ.SDK.csharp.Subscription.SubscribeRequest.
        /// </summary>
        /// <param name="subscribeRequest">Parameters list represent by KubeMQ.SDK.csharp.Subscription.SubscribeRequest that will determine the subscription configuration.</param>
        /// <returns>A task that represents the Subscribe Request.</returns>
        private async Task SubscribeToRequests(SubscribeRequest subscribeRequest)
        {
            KubeMQGrpc.Subscribe innerSubscribeRequest = subscribeRequest.ToInnerSubscribeRequest();

            using (var call = GetKubeMQClient().SubscribeToRequests(innerSubscribeRequest, _metadata))
            {
                // await for requests form GRPC stream.
                while (await call.ResponseStream.MoveNext())
                {
                    // Received requests form GRPC stream.
                    InnerRequest request = call.ResponseStream.Current;

                    LogRequest(request);

                    // Add (Post) request to queue
                    _RecivedRequests.Post(request);
                }
            }
        }

        private void ValidateSubscribeRequest(SubscribeRequest subscribeRequest)
        {
            if (string.IsNullOrWhiteSpace(subscribeRequest.Channel))
            {
                throw new ArgumentException("Parameter is mandatory", "Channel");
            }
            
            if (!subscribeRequest.IsValideType("CommandQuery"))// SubscribeType
            {
                throw new ArgumentException("Invalid Subscribe Type for this Class.", "SubscribeType");
            }
        }

        private void LogRequest(InnerRequest request)
        {
            logger.LogTrace($"Responder InnerRequest. ID:'{request.RequestID}', Channel:'{request.Channel}', ReplyChannel:'{request.ReplyChannel}'");
        }

        private void LogResponse(InnerResponse response)
        {
            logger.LogTrace($"Responder InnerResponse. ID:'{response.RequestID}', ReplyChannel:'{response.ReplyChannel}'");
        }
    }

}
