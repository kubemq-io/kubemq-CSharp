using System;
using KubeMQ.Grpc;
using static System.Guid;

namespace KubeMQ.SDK.csharp.QueueStream
{
    public delegate void OnPollRequestError ( string err);
    public delegate void OnPollRequestComplete ();
    
    public class PollRequest
    {
        public string Queue
        {
            get => _queue;
            set => _queue = value;
        }

        public int MaxItems
        {
            get => _maxItems;
            set => _maxItems = value;
        }

        public int WaitTimeout
        {
            get => _waitTimeout;
            set => _waitTimeout = value;
        }

        public bool AutoAck
        {
            get => _autoAck;
            set => _autoAck = value;
        }

        public OnPollRequestError OnError
        {
            set => _onError = value;
        }

        public OnPollRequestComplete OnComplete
        {
            set => _onComplete = value;
        }

        internal string RequestId
        {
            get => _requestId;
        }
        private string _requestId = NewGuid().ToString();
        private string _queue = "";
        private int _maxItems = 0;
        private int _waitTimeout = 0;
        private bool _autoAck = false;
        private OnPollRequestError _onError = null;
        private OnPollRequestComplete _onComplete = null;

        

        public PollRequest()
        {
        }

        internal void SendOnError(string err)
        {
            if (_onError != null)
            {
                _onError(err);
            }
        }
        internal void SendOnComplete()
        {
            if (_onComplete != null)
            {
                _onComplete();
            }
        }
        internal QueuesDownstreamRequest ValidateAndComplete(string clientId)
        {
            if (string.IsNullOrEmpty(Queue))
            {
                throw new ArgumentException("request channel cannot be empty");
            }
            if (MaxItems<0)
            {
                throw new ArgumentException("request max items cannot be negative");
            }
            if (WaitTimeout<0)
            {
                throw new ArgumentException("request wait timeout cannot be negative");
            }

            return new QueuesDownstreamRequest()
            {
                RequestID = _requestId,
                Channel = _queue,
                RequestTypeData = QueuesDownstreamRequestType.Get,
                ClientID = clientId,
                MaxItems = _maxItems,
                WaitTimeout = _waitTimeout,
                AutoAck = _autoAck,
            };
        }
    }
}