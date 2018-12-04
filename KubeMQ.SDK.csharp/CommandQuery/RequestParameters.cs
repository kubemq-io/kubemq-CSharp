namespace KubeMQ.SDK.csharp.CommandQuery
{
    public class RequestParameters
    {
        #region Properties
        /// <summary>
        /// Represents the limit for waiting for response (Milliseconds)
        /// </summary>
        public int? Timeout { get; set; }
        /// <summary>
        /// Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.
        /// </summary>
        public string CacheKey { get; set; }
        /// <summary>
        /// Cache time to live : for how long does the request should be saved in Cache.
        /// </summary>
        public int? CacheTTL { get; set; }
        #endregion

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.RequestParameters .
        /// </summary>
        public RequestParameters() { }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.RequestParameters with a set of chosen parameters to create a RequestChannel .
        /// </summary>
        /// <param name="timeout">Represents the limit for waiting for response (Milliseconds)</param>
        /// <param name="cacheKey">Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.</param>
        /// <param name="cacheTTL">Cache time to live : for how long does the request should be saved in Cache.</param>
        public RequestParameters(int timeout, string cacheKey, int cacheTTL)
        {
            Timeout = timeout;
            CacheKey = cacheKey;
            CacheTTL = cacheTTL;
        }
    }
}
