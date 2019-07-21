using System.Collections.Generic;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class ReceiveMessages
    {
        public ReceiveMessages(ReceiveQueueMessagesResponse qr)
        {
            Error = qr.Error;
            IsError = qr.IsError;
            IsPeak = qr.IsPeak;
            Messages = Tools.Converter.QueueMessages(qr.Messages);
            MessagesExpired = qr.MessagesExpired;
            MessagesReceived= qr.MessagesReceived;
            RequestID= qr.RequestID;
           
        }

        public string Error { get; }
        public bool IsError { get; }
        public bool IsPeak { get; }
        public IEnumerable<TransactionMessage> Messages { get; }
        public int MessagesExpired { get; }
        public int MessagesReceived { get; }
        public string RequestID { get; }     
    }
}