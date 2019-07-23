using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// Represents the Request used in RequestReply KubeMQ.SDK.csharp.RequestReply.channel.
    /// </summary>
    public class Request
    {
        #region Properties
        /// <summary>
        /// Represents a Request identifier.
        /// </summary>
        public string RequestID { get; set; }
        /// <summary>
        /// Represents metadata as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.RequestReply.Request.
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        /// Represents a set of Key value pair that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }
        #endregion

        #region C'tor
        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.Request for KubeMQ.SDK.csharp.RequestReply.channel use.
        /// </summary>
        public Request() { }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.Request for KubeMQ.SDK.csharp.RequestReply.channel use with a set of parameters.
        /// </summary>
        /// <param name="id">Represents a Request identifier.</param>
        /// <param name="metadata">Represents text as System.String.</param>
        /// <param name="body">Represents The content of the KubeMQ.SDK.csharp.RequestReply.Request.</param>
        public Request(string id, string metadata, byte[] body)
        {
            RequestID = id;
            Metadata = metadata;
            Body = body;
        }
        #endregion
    }
}
    