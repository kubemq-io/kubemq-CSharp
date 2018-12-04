using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Tools;
using InnerRequest = KubeMQ.Grpc.Request;
using InnerResponse = KubeMQ.Grpc.Response;

namespace KubeMQ.SDK.csharp.CommandQuery.LowLevel
{

    /// <summary>
    ///  Represents the instance that is responsible to send requests to the kubemq. 
    /// </summary>
    public class Initiator : GrpcClient
    {
        private static ILogger logger;

        #region C'tor
        /// <summary>
        /// Initialize a new Initiator to send requests and handle response
        /// Logger will write to default output with suffix KubeMQSDK
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        public Initiator() : this(null, null) { }

        /// <summary>    
        /// Initialize a new Initiator to send requests and handle response 
        /// KubeMQAddress will be parsed from Config or environment parameter
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Initiator(ILogger plogger) : this(null, plogger) { }

        /// <summary>
        /// Initialize a new Initiator to send requests and handle response 
        /// Logger will write to default output with suffix KubeMQSDK
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        public Initiator(string KubeMQAddress) : this(KubeMQAddress, null) { }

        /// <summary>
        /// Initialize a new Initiator to send requests and handle response 
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger</param>
        public Initiator(string KubeMQAddress, ILogger plogger )
        {
            _kubemqAddress = KubeMQAddress;
            logger = Logger.InitLogger(plogger, "Initiator");
        }
        #endregion

        /// <summary>
        /// Publish a single request using the KubeMQ , response will return in the passed handler.
        /// </summary>
        /// <param name="handler">Method that will be activated once receiving response.</param>
        /// <param name="request">The KubeMQ.SDK.csharp.RequestReply.LowLevel.request that will be sent to the kubeMQ.</param>
        /// <returns>A task that represents the request that was sent using the SendRequest.</returns>
        public async Task SendRequest(HandleResponseDelegate handler, Request request)
        {
            try
            {
                //LogRequest(request);

                InnerRequest innerRequest = request.Convert();

                // Send request and wait for response
                InnerResponse innerResponse = await GetKubeMQClient().SendRequestAsync(innerRequest, _metadata);

                // convert InnerResponse to Response and return response to end user
                Response response = new Response(innerResponse);

                // send the response to the end-user response handler
                handler(response);
            }
            catch (RpcException ex)
            {
                logger.LogError($"Grpc Exception in SendRequest. Status: {ex.Status}");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in Initiator.SendRequest");

                throw ex;
            }
        }

        /// <summary>
        ///  Publish a single request using the KubeMQ Async .
        /// </summary>
        /// <param name="request">The KubeMQ.SDK.csharp.RequestReply.LowLevel.request that will be sent to the kubeMQ.</param>
        /// <returns> A task that represents the request that was sent using the SendRequestAsync.</returns>
        public async Task<Response> SendRequestAsync(Request request)
        {
            try
            {
                //LogRequest(request);

                InnerRequest innerRequest = request.Convert();
                
                // Send request and wait for response
                InnerResponse innerResponse = await GetKubeMQClient().SendRequestAsync(innerRequest, _metadata);

                // convert InnerResponse to Response and return response to end user
                return new Response(innerResponse);
            }
            catch (RpcException ex)
            {
                logger.LogError($"Grpc Exception in SendRequestAsync. Status: {ex.Status}");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in Initiator.SendRequestAsync");
                return null;
            }
        }

        /// <summary>
        /// Publish a single request using the KubeMQ.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>KubeMQ KubeMQ.SDK.csharp.RequestReply.Response.</returns>
        public Response SendRequest(Request request)
        {
            try
            {
                //LogRequest(request);

                InnerRequest innerRequest = request.Convert();

                // Send request and wait for response
                InnerResponse innerResponse = GetKubeMQClient().SendRequest(innerRequest, _metadata);

                // convert InnerResponse to Response and return response to end user
                return new Response(innerResponse);
            }
            catch (RpcException ex)
            {
                logger.LogError($"Grpc Exception in SendRequestAsync. Status: {ex.Status}");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in Initiator.SendRequestAsync");
                return null;
            }
        }

        private void LogRequest(Request request)
        {
            logger.LogTrace($"Initiator->SendRequest. ID:'{request.RequestID}', Channel:'{request.Channel}', ReplyChannel:'{request.ReplyChannel}'");
        }
    }
}
