using System;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Queries
{
    public class QueryResponse
   {
        /// <summary>
        /// The received command message this response is associated with.
        /// </summary>
        public QueryReceived QueryReceived { get; set; }

        /// <summary>
        /// The client ID associated with this response.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// The request ID associated with this response.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Indicates if the command associated with this response was executed successfully.
        /// </summary>
        public bool IsExecuted { get; set; }


        /// <summary>
        /// Represents a query response message.
        /// </summary>
        public string Metadata { get; set; }
      
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the cache was hit.
        /// </summary>
        public bool CacheHit { get; set; }
         
        /// <summary>
        /// The timestamp of this response.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The error message associated with this response.
        /// </summary>
        public string Error { get; set; }

        public QueryResponse()
        {
            
        }
        public QueryResponse(QueryReceived commandReceived = null, bool isExecuted = false, string error = "", DateTime? timestamp = null)
        {
            QueryReceived = commandReceived;
            ClientId = string.Empty;
            RequestId = string.Empty;
            IsExecuted = isExecuted;
            Timestamp = timestamp ?? DateTime.Now;
            Error = error;
        }
        
        public QueryResponse SetQueryReceived(QueryReceived commandReceived)
        {
            QueryReceived = commandReceived;
            return this;
        }
        
        public QueryResponse SetClientId(string clientId)
        {
            ClientId = clientId;
            return this;
        }
        
        public QueryResponse SetRequestId(string requestId)
        {
            RequestId = requestId;
            return this;
        }
        
        public QueryResponse SetIsExecuted(bool isExecuted)
        {
            IsExecuted = isExecuted;
            return this;
        }
        
        public QueryResponse SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
            return this;
        }
        
        public QueryResponse SetError(string error)
        {
            Error = error;
            return this;
        }
        
        
        /// <summary>
        /// Validates the command response message. Throws an <see cref="ArgumentException"/> if the command response is invalid.
        /// </summary>
        /// <returns>The validated <see cref="QueryResponse"/> instance.</returns>
        internal QueryResponse Validate()
        {
            if (QueryReceived == null)
                throw new ArgumentException("Query response must have a command request.");
            else if (string.IsNullOrEmpty(QueryReceived.ReplyChannel))
                throw new ArgumentException("Query response must have a reply channel.");

            return this;
        }

        /// <summary>
        /// Decodes the protocol buffer response and populates the <see cref="QueryResponse"/> attributes.
        /// </summary>
        /// <param name="pbResponse">The protocol buffer response object.</param>
        /// <returns>The <see cref="QueryResponse"/> instance.</returns>
        internal QueryResponse Decode(pb.Response pbResponse)
        {
            ClientId = pbResponse.ClientID;
            RequestId = pbResponse.RequestID;
            IsExecuted = pbResponse.Executed;
            Error = pbResponse.Error;
            Timestamp = new DateTime((long)(pbResponse.Timestamp / 1e9));

            return this;
        }

        /// <summary>
        /// Encodes the <see cref="QueryResponse"/> into a protocol buffer response object.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The protocol buffer response object.</returns>
        internal pb.Response Encode(string clientId)
        {
            var pbResponse = new pb.Response
            {
                ClientID = clientId,
                RequestID = QueryReceived.Id,
                ReplyChannel = QueryReceived.ReplyChannel,
                Executed = IsExecuted,
                Error = Error,
                Timestamp = (long)(Timestamp.Ticks * 1e9)
            };

            return pbResponse;
        }

        public override string ToString()
        {
            return $"QueryResponseMessage: client_id={ClientId}, request_id={RequestId}, is_executed={IsExecuted}, error={Error}, timestamp={Timestamp}";
        }
    }
}