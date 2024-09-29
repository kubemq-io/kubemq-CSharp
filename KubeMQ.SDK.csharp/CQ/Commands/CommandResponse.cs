using System;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Commands
{
    public class CommandResponse
   {
        /// <summary>
        /// The received command message this response is associated with.
        /// </summary>
        public CommandReceived CommandReceived { get; set; }

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
        /// The timestamp of this response.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The error message associated with this response.
        /// </summary>
        public string Error { get; set; }

        public CommandResponse()
        {
            
        }
        public CommandResponse(CommandReceived commandReceived = null, bool isExecuted = false, string error = "", DateTime? timestamp = null)
        {
            CommandReceived = commandReceived;
            ClientId = string.Empty;
            RequestId = string.Empty;
            IsExecuted = isExecuted;
            Timestamp = timestamp ?? DateTime.Now;
            Error = error;
        }
        
        public CommandResponse SetCommandReceived(CommandReceived commandReceived)
        {
            CommandReceived = commandReceived;
            return this;
        }
        
        public CommandResponse SetClientId(string clientId)
        {
            ClientId = clientId;
            return this;
        }
        
        public CommandResponse SetRequestId(string requestId)
        {
            RequestId = requestId;
            return this;
        }
        
        public CommandResponse SetIsExecuted(bool isExecuted)
        {
            IsExecuted = isExecuted;
            return this;
        }
        
        public CommandResponse SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
            return this;
        }
        
        public CommandResponse SetError(string error)
        {
            Error = error;
            return this;
        }
        
        
        /// <summary>
        /// Validates the command response message. Throws an <see cref="ArgumentException"/> if the command response is invalid.
        /// </summary>
        /// <returns>The validated <see cref="CommandResponse"/> instance.</returns>
        internal CommandResponse Validate()
        {
            if (CommandReceived == null)
                throw new ArgumentException("Command response must have a command request.");
            
            if (string.IsNullOrEmpty(CommandReceived.ReplyChannel))
                throw new ArgumentException("Command response must have a reply channel.");

            return this;
        }

        /// <summary>
        /// Decodes the protocol buffer response and populates the <see cref="CommandResponse"/> attributes.
        /// </summary>
        /// <param name="pbResponse">The protocol buffer response object.</param>
        /// <returns>The <see cref="CommandResponse"/> instance.</returns>
        internal CommandResponse Decode(pb.Response pbResponse)
        {
            ClientId = pbResponse.ClientID;
            RequestId = pbResponse.RequestID;
            IsExecuted = pbResponse.Executed;
            Error = pbResponse.Error;
            Timestamp = new DateTime((long)(pbResponse.Timestamp / 1e9));

            return this;
        }

        /// <summary>
        /// Encodes the <see cref="CommandResponse"/> into a protocol buffer response object.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The protocol buffer response object.</returns>
        internal pb.Response Encode(string clientId)
        {
            var pbResponse = new pb.Response
            {
                ClientID = clientId,
                RequestID = CommandReceived.Id,
                ReplyChannel = CommandReceived.ReplyChannel,
                Executed = IsExecuted,
                Error = Error ?? string.Empty,
                Timestamp = (long)(Timestamp.Ticks * 1e9),
            };
            return pbResponse;
        }

        public override string ToString()
        {
            return $"CommandResponseMessage: client_id={ClientId}, request_id={RequestId}, is_executed={IsExecuted}, error={Error}, timestamp={Timestamp}";
        }
    }
}