using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class SendMessageResult
    {
        public bool IsError { get; }
        public string Error { get; }
        public long ExpirationAt { get; }
        public string MessageID { get; }
        public long SentAt { get; }
        public long DelayedTo { get; }
       

        public SendMessageResult(SendQueueMessageResult sendQueueMessageResult)
        {
            this.IsError = sendQueueMessageResult.IsError;
            this.ExpirationAt = sendQueueMessageResult.ExpirationAt;
            this.MessageID = sendQueueMessageResult.MessageID;
            this.SentAt = sendQueueMessageResult.SentAt;
            this.DelayedTo = sendQueueMessageResult.DelayedTo;
            this.Error = sendQueueMessageResult.Error;          
        }        
    }
}