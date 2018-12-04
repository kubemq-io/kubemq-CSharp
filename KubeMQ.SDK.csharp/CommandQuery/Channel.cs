using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// Represents a Initiator with predefined parameters.
    /// </summary>
    public class Channel
    {
        #region Properties
        private LowLevel.Initiator _initiator;

        private RequestType RequestType;

        private string ChannelName { get; set; }
        private string ClientID { get; set; }
        private int Timeout { get; set; }
        private string CacheKey { get; set; }
        private int CacheTTL { get; set; }

        //private string ReplyChannel { get; set; }
        #endregion

        #region C'tor
        /// <summary>
        /// Initializes a new instance of the RequestChannel class using RequestChannelParameters.
        /// </summary>
        /// <param name="parameters">RequestChannelParameters that present a predefined set of parameters</param>
        public Channel(ChannelParameters parameters) : this(parameters.RequestsType, parameters.ChannelName, parameters.ClientID,
            parameters.Timeout, parameters.CacheKey, parameters.CacheTTL, parameters.KubeMQAddress, parameters.Logger) { }

        /// <summary>
        /// Initializes a new instance of the RequestChannel class using a set of parameters.
        /// </summary>
        /// <param name="channelName">Represents The channel name to send to using the KubeMQ .</param>
        /// <param name="clientID">Represents the sender ID that the Request will be send under.</param>
        /// <param name="timeout">Represents the limit for waiting for response (Milliseconds)</param>
        /// <param name="cacheKey">Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.</param>
        /// <param name="cacheTTL">Cache time to live : for how long does the request should be saved in Cache</param>
        /// <param name="kubeMQAddress">KubeMQ server address.</param>
        /// <param name="logger">Microsoft.Extensions.Logging Ilogger.</param>
        public Channel(RequestType requestsType, string channelName, string clientID, int timeout, string cacheKey, int cacheTTL, string KubeMQAddress, ILogger logger)
        {
            RequestType = requestsType;
            ChannelName = channelName;
            ClientID = clientID;
            Timeout = timeout;
            CacheKey = cacheKey ?? string.Empty;
            CacheTTL = cacheTTL;

            if (!IsValide(out Exception ex))
            {
                throw ex;
            }

            _initiator = new LowLevel.Initiator(KubeMQAddress, logger);
        }
        #endregion

        /// <summary>
        /// Publish a single request using the KubeMQ , response will return in the passed handler.
        /// </summary>
        /// <param name="handler">Method that will be activated once receiving response .</param>
        /// <param name="request">The KubeMQ.SDK.csharp.RequestReply.LowLevel.request that will be sent to the kubeMQ . </param>
        /// <returns>A task that represents the request that was sent using the SendRequest .</returns>
        public async Task SendRequest(HandleResponseDelegate handler, Request request)
        {
            await _initiator.SendRequest(handler, CreateLowLevelRequest(request));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="request"></param>
        /// <param name="overrideParams">Allow overwriting "Timeout" "CacheKey" and "CacheTTL" for a single Request. </param>
        /// <returns></returns>
        public async Task SendRequest(HandleResponseDelegate handler, Request request, RequestParameters overrideParams)
        {
            await _initiator.SendRequest(handler, CreateLowLevelRequest(request, overrideParams));
        }

        public async Task<Response> SendRequestAsync(Request request)
        {
            Response response = await _initiator.SendRequestAsync(CreateLowLevelRequest(request));
            return response;
        }

        public async Task<Response> SendRequestAsync(Request request, RequestParameters overrideParams)
        {
            Response response = await _initiator.SendRequestAsync(CreateLowLevelRequest(request, overrideParams));
            return response;
        }

        public Response SendRequest(Request request)
        {
            return _initiator.SendRequest(CreateLowLevelRequest(request));
        }

        public Response SendRequest(Request request, RequestParameters overrideParams)
        {
            return _initiator.SendRequest(CreateLowLevelRequest(request, overrideParams));
        }

        private bool IsValide(out Exception ex)
        {
            if (string.IsNullOrWhiteSpace(ChannelName))
            {
                ex = new ArgumentException("Parameter is mandatory", "ChannelName");
                return false;
            }
            //if (string.IsNullOrWhiteSpace(ClientID))
            //{
            //    ex = new ArgumentException("Parameter is mandatory", "ClientID");
            //    return false;
            //}
            if (RequestType == RequestType.RequestTypeUnknown)
            {
                ex = new ArgumentException("Invalid Request Type", "RequestType");
                return false;
            }
            if (Timeout <= 0 || Timeout > int.MaxValue)
            {
                ex = new ArgumentException($"Parameter must be between 1 and {int.MaxValue}", "Timeout");
                return false;
            }
            ex = null;
            return true;
        }

        private LowLevel.Request CreateLowLevelRequest(Request request)
        {
            return new LowLevel.Request()
            {
                RequestType = this.RequestType,
                Channel = this.ChannelName,
                ClientID = this.ClientID,
                Timeout = this.Timeout,
                CacheKey = this.CacheKey,
                CacheTTL = this.CacheTTL,
                RequestID = request.RequestID,
                Body = request.Body,
                Metadata = request.Metadata
            };
        }

        private LowLevel.Request CreateLowLevelRequest(Request request, RequestParameters overrideParams)
        {
            LowLevel.Request req = CreateLowLevelRequest(request);

            if (overrideParams.Timeout.HasValue)
            {
                req.Timeout = overrideParams.Timeout.Value;
            }

            if (overrideParams.CacheKey != null)
            {
                req.CacheKey = overrideParams.CacheKey;
            }

            if (overrideParams.CacheTTL.HasValue)
            {
                req.CacheTTL = overrideParams.CacheTTL.Value;
            }

            return req;
        }
    }
}
