using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    /// <summary>
    ///  Queue purge messages request execution result (will not delete data).
    /// </summary>
    public class AckAllMessagesResponse
    {
        /// <summary>
        ///  Unique for Request.
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
        /// Number of affected messages.
        /// </summary>
        public ulong AffectedMessages { get; }

        internal AckAllMessagesResponse(AckAllQueueMessagesResponse rec)
        {
            RequestID = rec.RequestID;
            IsError = rec.IsError;
            Error= rec.Error;
            AffectedMessages= rec.AffectedMessages;          
        }   
    }
}