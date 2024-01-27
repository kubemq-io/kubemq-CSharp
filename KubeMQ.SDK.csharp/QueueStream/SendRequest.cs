using System;
using System.Collections.Generic;
using KubeMQ.Grpc;
using static System.Guid;

namespace KubeMQ.SDK.csharp.QueueStream
{
    public class SendRequest
    {
        private List<Message> _messages;
        private string _requestId = NewGuid().ToString();

        public SendRequest(List<Message> messages)
        {
            _messages = messages;
        }
        internal string RequestId
        {
            get => _requestId;
        }
        internal QueuesUpstreamRequest ValidateAndComplete(string clientId)
        {
            if (_messages.Count == 0)
            {
                throw new AggregateException("request must contain at least one message");
            }
            QueuesUpstreamRequest pbReq = new QueuesUpstreamRequest();
            pbReq.RequestID = _requestId;
            foreach (var msg in _messages)
            {
                if (string.IsNullOrEmpty(msg.Queue))
                {
                    throw new ArgumentException("request queue cannot be empty");
                }
                if ((msg.Body == null || msg.Body.Length == 0) && (string.IsNullOrEmpty(msg.Metadata)))
                {
                    throw new ArgumentException("either body or metadata must be set");
                }
                pbReq.Messages.Add(msg.ToQueueMessage(clientId));
            }
            return pbReq;
        }
    }
}