using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue.Stream
{
    /// <summary>
    /// Transaction response
    /// </summary>
    public class TransactionMessagesResponse
    {
        /// <summary>
        /// Unique for Request
        /// </summary>
        public string RequestID { get; }
        /// <summary>
        /// Returned from KubeMQ, false if no error.
        /// </summary>
        public bool IsError { get; }
        /// <summary>
        /// Error message, valid only if IsError true.
        /// </summary>
        public string Error { get; }
        /// <summary>
        /// The received Message
        /// </summary>
        public Message Message { get; }
        
        /// <summary>
        /// Request action: ReceiveMessage, AckMessage, RejectMessage, ModifyVisibility, ResendMessage,  SendModifiedMessage, Unknown
        /// </summary>
        public StreamRequestType StreamRequestTypeData { get; }
              
        internal TransactionMessagesResponse(StreamQueueMessagesResponse streamQueueMessagesResponse)
        {
            IsError = streamQueueMessagesResponse.IsError;
            Error = streamQueueMessagesResponse.Error;
            Message = streamQueueMessagesResponse.Message!=null? new Message(streamQueueMessagesResponse.Message): null;
            RequestID  = streamQueueMessagesResponse.RequestID;
            StreamRequestTypeData = streamQueueMessagesResponse.StreamRequestTypeData;
        }
        internal TransactionMessagesResponse(string errorMessage, Message msg=null, string requestID=null)
        {
            IsError = true;
            Error = errorMessage;
            Message = msg;
            RequestID = requestID;
        }

    }
}