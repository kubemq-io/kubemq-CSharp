using System.Collections.Generic;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    /// <summary>
    /// Queue response.
    /// </summary>
    public class ReceiveMessagesResponse
    {
        /// <summary>
        ///  Unique for Request
        /// </summary>
        public string RequestID { get; }
        /// <summary>
        /// Returned from KubeMQ, false if no error.
        /// </summary>
        public string Error { get; }
        /// <summary>
        /// Error message, valid only if IsError true.
        /// </summary>
        public bool IsError { get; }
        /// <summary>
        /// Indicate if the request was peek, true if peek.
        /// </summary>
        public bool IsPeek { get; }
        /// <summary>
        /// Collection of Messages.
        /// </summary>
        public IEnumerable<Message> Messages { get; }
        /// <summary>
        /// 
        /// </summary>
        public int MessagesExpired { get; }
        /// <summary>
        /// Count of received messages.
        /// </summary>
        public int MessagesReceived { get; }

        internal ReceiveMessagesResponse(ReceiveQueueMessagesResponse receiveQueueMessagesResponse)
        {
            Error = receiveQueueMessagesResponse.Error;
            IsError = receiveQueueMessagesResponse.IsError;
            IsPeek = receiveQueueMessagesResponse.IsPeak;
            Messages = Tools.Converter.FromQueueMessages(receiveQueueMessagesResponse.Messages);
            MessagesExpired = receiveQueueMessagesResponse.MessagesExpired;
            MessagesReceived = receiveQueueMessagesResponse.MessagesReceived;
            RequestID = receiveQueueMessagesResponse.RequestID;
        }


    }
}