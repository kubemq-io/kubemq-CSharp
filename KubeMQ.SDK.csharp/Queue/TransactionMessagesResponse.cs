using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class TransactionMessagesResponse
    { 

        public TransactionMessagesResponse(StreamQueueMessagesResponse streamQueueMessagesResponse)
        {
            IsError = streamQueueMessagesResponse.IsError;
            Error = streamQueueMessagesResponse.Error;
            Message = Tools.Converter.QueueMessage(streamQueueMessagesResponse.Message);
            RequestID  = streamQueueMessagesResponse.RequestID;
            StreamRequestTypeData = streamQueueMessagesResponse.StreamRequestTypeData;
        }

        public bool IsError { get; }
        public string Error { get; }
        public TransactionMessage Message { get; }
        public object RequestID { get; }
        public StreamRequestType StreamRequestTypeData { get; }
    }
}