using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// Represents the Request used in RequestReply KubeMQ.SDK.csharp.RequestReply.channel.
    /// </summary>
    public class CommandRequest
    {
        #region Properties
        /// <summary>
        /// Represents a Request identifier.
        /// </summary>
        public string RequestID { get; set; }
        /// <summary>
        /// Represents metadata as System.String.
        /// </summary>
        
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ
        /// </summary>
        public string Channel { get; set; }
        
        /// <summary>
        /// Represents the timeout for waiting for response (Milliseconds)
        /// </summary>
        public int Timeout { get; set; } 
        
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
        public CommandRequest() { }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.Request for KubeMQ.SDK.csharp.RequestReply.channel use with a set of parameters.
        /// </summary>
        /// <param name="id">Represents a Request identifier.</param>
        /// <param name="channel">Represents The channel name to send to using the KubeMQ</param>
        /// <param name="timeout">Represents the limit for waiting for response (Milliseconds)</param>
        /// <param name="metadata">Represents text as System.String.</param>
        /// <param name="body">Represents The content of the KubeMQ.SDK.csharp.RequestReply.Request.</param>
        public CommandRequest(string id, string channel, string metadata, byte[] body, int timeout)
        {
            Channel = channel;
            RequestID = id;
            Metadata = metadata;
            Body = body;
            Timeout = timeout;
        }
        
        internal LowLevel.Request CreateLowLevelRequest()
        {
            return new LowLevel.Request()
            {
                RequestType = RequestType.Command,
                RequestID = RequestID,
                Metadata = Metadata,
                Body = Body,
                Tags = Tags,
                Timeout = Timeout,
                Channel = Channel
            };
        }
        #endregion
    }
}
    