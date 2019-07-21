using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class TransactionMessagesResponse
    {

        public bool IsError { get; }
        public string Error { get; }
        public Message Message { get; }
        public object RequestID { get; }
        public StreamRequestType StreamRequestTypeData { get; }

        public TransactionMessagesResponse(StreamQueueMessagesResponse streamQueueMessagesResponse)
        {
            IsError = streamQueueMessagesResponse.IsError;
            Error = streamQueueMessagesResponse.Error;
            Message = streamQueueMessagesResponse.Message!=null? new Message(streamQueueMessagesResponse.Message): null;
            RequestID  = streamQueueMessagesResponse.RequestID;
            StreamRequestTypeData = streamQueueMessagesResponse.StreamRequestTypeData;
        }

    }
}