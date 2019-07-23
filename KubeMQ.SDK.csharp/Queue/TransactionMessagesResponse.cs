using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue.Stream
{
    public class TransactionMessagesResponse
    {
        public bool IsError { get; }
        public string Error { get; }
        public Message Message { get; }
        public string RequestID { get; }
        public StreamRequestType StreamRequestTypeData { get; }

        public TransactionMessagesResponse(StreamQueueMessagesResponse streamQueueMessagesResponse)
        {
            IsError = streamQueueMessagesResponse.IsError;
            Error = streamQueueMessagesResponse.Error;
            Message = streamQueueMessagesResponse.Message!=null? new Message(streamQueueMessagesResponse.Message): null;
            RequestID  = streamQueueMessagesResponse.RequestID;
            StreamRequestTypeData = streamQueueMessagesResponse.StreamRequestTypeData;
        }
        public TransactionMessagesResponse(string errorMessage, Message msg=null, string requestID=null)
        {
            IsError = true;
            Error = errorMessage;
            Message = msg;
            RequestID = requestID;
        }

    }
}