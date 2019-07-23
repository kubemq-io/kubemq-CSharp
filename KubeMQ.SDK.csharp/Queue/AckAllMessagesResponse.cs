using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class AckAllMessagesResponse
    {
        public string RequestID { get; }
        public bool IsError { get; }
        public string Error { get; }
        public ulong AffectedMessages { get; }

        public AckAllMessagesResponse(AckAllQueueMessagesResponse rec)
        {
            RequestID = rec.RequestID;
            IsError = rec.IsError;
            Error= rec.Error;
            AffectedMessages= rec.AffectedMessages;          
        }   
    }
}