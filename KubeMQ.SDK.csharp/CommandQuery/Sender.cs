using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using KubeMQ.Grpc;
using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.CommandQuery {
    /// <summary>
    /// Represents a Initiator with predefined parameters.
    /// </summary>
    public class Sender {
        #region Properties
        private LowLevel.Initiator _initiator;

        private string ClientID { get; set; }
        //private string ReplyChannel { get; set; }
        #endregion

        #region C'tor
        /// <summary>
        /// Initializes a new instance of the RequestChannel class using a set of parameters.
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address.</param>
        /// <param name="clientID">Represents the sender ID that the Request will be send under.</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Sender (string KubeMQAddress, string clientId="", ILogger logger = null, string authToken = null) {
            ClientID = clientId;
            _initiator = new LowLevel.Initiator (KubeMQAddress, logger, authToken);
        }
        #endregion

        /// <summary>
        /// Send a request using the KubeMQ
        /// </summary>
        /// <param name="request">The command request that will be sent to the kubeMQ.</param>
        /// <returns>Response</returns>
        public Response SendRequest (CommandRequest request) {
            Response response = _initiator.SendRequest (request.CreateLowLevelRequest());
            return response;            
        }
        /// <summary>
        /// Send a request using the KubeMQ
        /// </summary>
        /// <param name="request">The command request that will be sent to the kubeMQ.</param>
        /// <returns>Response</returns>
        public Response SendRequest (QueryRequest request) {
            Response response = _initiator.SendRequest (request.CreateLowLevelRequest());
            return response;            
        }
        /// <summary>
        /// Send a async request using the KubeMQ 
        /// </summary>
        /// <param name="request">The request that will be sent to the kubeMQ.</param>
        /// <returns>Response</returns>
        public async Task<Response> SendRequestAsync (CommandRequest request) {
            Response response = await _initiator.SendRequestAsync (request.CreateLowLevelRequest());
            return response;
        }

        /// <summary>
        /// Send a async request using the KubeMQ 
        /// </summary>
        /// <param name="request">The query request that will be sent to the kubeMQ.</param>
        /// <returns>Response</returns>
        public async Task<Response> SendRequestAsync (QueryRequest request) {
            Response response = await _initiator.SendRequestAsync (request.CreateLowLevelRequest());
            return response;
        }
        
        /// <summary>
        /// Send a request using the KubeMQ, response will be handled by the provided delegate.
        /// </summary>
        /// <param name="request">The command request that will be sent to the kubeMQ.</param>
        /// <param name="handler">Method that will be activated once receiving response.</param>
        /// <returns>A task that represents the request that was sent using the SendRequest .</returns>
        public async Task SendRequest (CommandRequest request, HandleResponseDelegate handler) {
            await _initiator.SendRequest (request.CreateLowLevelRequest(), handler);
        }

        /// <summary>
        /// Send a request using the KubeMQ, response will be handled by the provided delegate.
        /// </summary>
        /// <param name="request">The query request that will be sent to the kubeMQ.</param>
        /// <param name="handler">Method that will be activated once receiving response.</param>
        /// <returns>A task that represents the request that was sent using the SendRequest .</returns>
        public async Task SendRequest (QueryRequest request, HandleResponseDelegate handler) {
            await _initiator.SendRequest (request.CreateLowLevelRequest(), handler);
        }
        
        /// <summary>
        /// Ping check Kubemq response using Channel.
        /// </summary>
        /// <returns>ping status of kubemq.</returns>
        public PingResult Ping () {
            return _initiator.Ping ();

        }
    }
}