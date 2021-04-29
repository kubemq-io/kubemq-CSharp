using System;
using KubeMQ.Grpc;
using static System.Guid;

namespace KubeMQ.SDK.csharp.QueueStream
{
    /// <summary>
   /// Delegate for calling request / responses error
    /// </summary>
    public delegate void OnPollRequestError ( string err);
    /// <summary>
   /// Delegate for calling when request completed
    /// </summary>
    public delegate void OnPollRequestComplete ();
    
    /// <summary>
    /// PollRequest
    /// </summary>
    public class PollRequest
    {
        /// <summary>
     /// Poll Request Queue name
        /// </summary>
        public string Queue
        {
            get => _queue;
            set => _queue = value;
        }
        /// <summary>
        /// Sets max items to poll in one request within wait timout 
        /// </summary>
        public int MaxItems
        {
            get => _maxItems;
            set => _maxItems = value;
        }
        /// <summary>
        /// Sets how long to wait in milliseconds for the request to complete
        /// </summary>
        public int WaitTimeout
        {
            get => _waitTimeout;
            set => _waitTimeout = value;
        }
        /// <summary>
        /// Sets automatic ack for receiveing messages
        /// </summary>
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
                throw new ArgumentException("request queue cannot be empty");
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