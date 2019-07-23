using KubeMQ.SDK.csharp.Tools;
using System.Collections.Generic;
using InnerRequest = KubeMQ.Grpc.Request;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    public class RequestReceive
    {
        #region Properties
        /// <summary>
        /// Represents a Request identifier.
        /// </summary>
        public string RequestID { get; set; }
        /// <summary>
        ///  Represents the type of request operation using KubeMQ.SDK.csharp.CommandQuery.RequestType.
        /// </summary>
        public RequestType RequestType { get; set; }
        /// <summary>
        /// Represents the sender ID that the Request return from.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ .
        /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// Represents The channel name that the response returned from.
        /// </summary>
        public string ReplyChannel { get; private set; }
        /// <summary>
        /// Represents text as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.RequestReply.RequestReceive .
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        ///  Represents the limit for waiting for response (Milliseconds) .
        /// </summary>
        public int Timeout { get; set; }
        /// <summary>
        /// Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.
        /// </summary>
        public string CacheKey { get; set; }
        /// <summary>
        /// Cache time to live : for how long does the request should be saved in Cache
        /// </summary>
        public int CacheTTL { get; set; }
        /// <summary>
        /// Represents a set of Key value pair that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }
        #endregion
        internal RequestReceive(InnerRequest innerRequest)
        {
            RequestID = innerRequest.RequestID;
            RequestType = (RequestType)innerRequest.RequestTypeData;
            ClientID = innerRequest.ClientID;
            Channel = innerRequest.Channel;
            Metadata = innerRequest.Metadata;
            Body = innerRequest.Body.ToByteArray();
            ReplyChannel = innerRequest.ReplyChannel;
            Timeout = innerRequest.Timeout;
            CacheKey = innerRequest.CacheKey;
            CacheTTL = innerRequest.CacheTTL;
            Tags = Converter.ReadTags(innerRequest.Tags);
        }
    }
}
