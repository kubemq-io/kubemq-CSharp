using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// Contain a set of parameters that can be passed to KubeMQ.SDK.csharp.RequestReply.RequestChannelParameters CTOR .
    /// </summary>
    public class ChannelParameters
    {
        #region Properties
        /// <summary>
        ///  Represents the type of request operation using KubeMQ.SDK.csharp.CommandQuery.RequestType.
        /// </summary>
        public RequestType RequestsType { get; set; }       
        /// <summary>
        /// Represents the sender ID that the messages will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ .
        /// </summary>
        public string ChannelName { get; set; }
        /// <summary>
        /// Represents the limit for waiting for response (Milliseconds).
        /// </summary>
        public int Timeout { get; set; }
        /// <summary>
        /// Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.
        /// </summary>
        public string CacheKey { get; set; }
        /// <summary>
        /// Cache time to live : for how long does the request should be saved in Cache.
        /// </summary>
        public int CacheTTL { get; set; }
        /// <summary>
        /// Represents The address of the KubeMQ server.
        /// </summary>
        public string KubeMQAddress { get; set; }
        /// <summary>
        /// Represents a type used to perform logging.
        /// </summary>
        public ILogger Logger { get; set; }
        #endregion

        #region C'tor
        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.RequestChannelParameters class.
        /// </summary>
        public ChannelParameters() { }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.RequestChannelParameters class with set parameters.
        /// </summary>
        /// <param name="channelName">Represents The channel name to send to using the KubeMQ .</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under</param>
        /// <param name="timeout">Represents the limit for waiting for response (Milliseconds).</param>
        /// <param name="cacheKey">Cache time to live : for how long does the request should be saved in Cache.</param>
        /// <param name="cacheTTL">Represents The address of the KubeMQ server.</param>
        /// <param name="kubeMQAddress">Represents The address of the KubeMQ server.</param>
        /// <param name="logger">Represents a type used to perform logging.</param>
        public ChannelParameters(RequestType requestsType, string channelName,string clientID,int timeout,string cacheKey,int cacheTTL,string kubeMQAddress,ILogger logger)
        {
            RequestsType = requestsType;
            ChannelName = channelName;
            ClientID = clientID;
            Timeout = timeout;
            CacheKey = cacheKey;
            CacheTTL = cacheTTL;
            KubeMQAddress = kubeMQAddress;
            Logger = logger;
        }
        #endregion
    }
}
