using System.Collections.Generic;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class ReceiveMessagesResponse
    {
        public string Error { get; }
        public bool IsError { get; }
        public bool IsPeak { get; }
        public IEnumerable<Message> Messages { get; }
        public int MessagesExpired { get; }
        public int MessagesReceived { get; }
        public string RequestID { get; }
        public ReceiveMessagesResponse(ReceiveQueueMessagesResponse receiveQueueMessagesResponse)
        {
            Error = receiveQueueMessagesResponse.Error;
            IsError = receiveQueueMessagesResponse.IsError;
            IsPeak = receiveQueueMessagesResponse.IsPeak;
            Messages = Tools.Converter.FromQueueMessages(receiveQueueMessagesResponse.Messages);
            MessagesExpired= receiveQueueMessagesResponse.MessagesExpired;
            MessagesReceived = receiveQueueMessagesResponse.MessagesReceived;
            RequestID = receiveQueueMessagesResponse.RequestID;
        }

       
    }
}