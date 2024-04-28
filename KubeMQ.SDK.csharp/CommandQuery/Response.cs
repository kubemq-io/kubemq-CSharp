using Google.Protobuf;
using System;
using InnerResponse = KubeMQ.Grpc.Response;

namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// The response that receiving from KubeMQ after sending a request.
    /// </summary>
    public class Response
    {
        #region public Properties
        /// <summary>
        /// Represents the sender ID that the Response will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents a Response identifier.
        /// </summary>
        public string RequestID { get; set; }
        /// <summary>
        /// Represents text as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.RequestReply.Response.
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        /// Represents if the response was received from Cache.
        /// </summary>
        public bool CacheHit { get; set; }
        /// <summary>
        /// Represents if the response Time.
        /// </summary>
        public DateTime Timestamp { get; set; }
        /// <summary>
        /// Represents if the response was executed.
        /// </summary>
        public bool Executed { get; set; }
        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Channel name for the Response.
        /// Set and used internally by KubeMQ server
        /// </summary>
        private string ReplyChannel { get; set; }

        #endregion

        #region C'Tors

        /// <summary>
        /// Create a KubeMQ.SDK.csharp.CommandQuery.Response from KubeMQ.SDK.csharp.CommandQuery.RequestReceive.
        /// </summary>
        /// <param name="request"></param>
        public Response(RequestReceive request)
        {
            RequestID = request.RequestID;
            ReplyChannel = request.ReplyChannel;
            Timestamp = DateTime.UtcNow; 
        }

        internal Response(InnerResponse inner)
        {
            ClientID = inner.ClientID;
            RequestID = inner.RequestID;
            ReplyChannel = inner.ReplyChannel;
            Metadata = inner.Metadata ?? string.Empty;
            Body = inner.Body==null ? null : inner.Body.ToByteArray();
            CacheHit = inner.CacheHit;
            Timestamp = Tools.Converter.FromUnixTime(inner.Timestamp);
            Executed = inner.Executed;
            Error = inner.Error;
        }

        #endregion

        internal InnerResponse Convert()
        {
            return new InnerResponse()
            {
                ClientID = string.IsNullOrEmpty(this.ClientID) ? Guid.NewGuid().ToString() : this.ClientID,
                RequestID = this.RequestID,
                ReplyChannel = this.ReplyChannel,
                Metadata = this.Metadata ?? string.Empty,
                Body = this.Body == null ? ByteString.Empty : ByteString.CopyFrom(this.Body),
                CacheHit = this.CacheHit,
                Timestamp = Tools.Converter.ToUnixTime(this.Timestamp),
                Executed = this.Executed,
                Error = string.IsNullOrEmpty(this.Error) ? string.Empty : this.Error
            };
        }
    }
}