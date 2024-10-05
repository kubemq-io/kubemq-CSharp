using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queues
{
    /// <summary>
    /// Queue request execution result.
    /// </summary>
    public class SendMessageResult
    {
        /// <summary>
        /// Unique for message
        /// </summary>
        public string MessageID { get; }
        /// <summary>
        /// Returned from KubeMQ, false if no error.
        /// </summary>
        public bool IsError { get; }
        /// <summary>
        /// Error message, valid only if IsError true.
        /// </summary>
        public string Error { get; }
        /// <summary>
        /// Message expiration time.
        /// </summary>
        public long ExpirationAt { get; }
        /// <summary>
        /// Message sent time.
        /// </summary>
        public long SentAt { get; }
        /// <summary>
        /// Message delayed delivery by KubeMQ.
        /// </summary>
        public long DelayedTo { get; }
       

        internal SendMessageResult(SendQueueMessageResult sendQueueMessageResult)
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