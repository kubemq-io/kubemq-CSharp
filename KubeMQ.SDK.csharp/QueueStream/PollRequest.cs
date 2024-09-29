using System;
using KubeMQ.Grpc;
using static System.Guid;

namespace KubeMQ.SDK.csharp.QueueStream
{
    /// <summary>
    /// Delegate for handling request/response errors
    /// </summary>
    public delegate void OnPollRequestError(string err);

    /// <summary>
    /// Delegate for calling when request is completed
    /// </summary>
    public delegate void OnPollRequestComplete();

    /// <summary>
    /// PollRequest class for configuring poll requests
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
        /// Sets max items to poll in one request within wait timeout
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
        /// Sets automatic ack for receiving messages
        /// </summary>
        public bool AutoAck
        {
            get => _autoAck;
            set => _autoAck = value;
        }

        /// <summary>
        /// Sets the visibility timeout in seconds
        /// </summary>
        public int VisibilitySeconds
        {
            get => _visibilitySeconds;
            set => _visibilitySeconds = value;
        }

        /// <summary>
        /// Delegate for handling errors
        /// </summary>
        public OnPollRequestError OnError
        {
            set => _onError = value;
        }

        /// <summary>
        /// Delegate for handling completion
        /// </summary>
        public OnPollRequestComplete OnComplete
        {
            set => _onComplete = value;
        }

        internal string RequestId => _requestId;

        private string _requestId = NewGuid().ToString();
        private string _queue = "";
        private int _maxItems = 0;
        private int _waitTimeout = 0;
        private bool _autoAck = false;
        private int _visibilitySeconds = 0;
        private OnPollRequestError _onError = null;
        private OnPollRequestComplete _onComplete = null;

        /// <summary>
        /// PollRequest constructor
        /// </summary>
        public PollRequest()
        {
        }

        internal void SendOnError(string err)
        {
            _onError?.Invoke(err);
        }

        internal void SendOnComplete()
        {
            _onComplete?.Invoke();
        }

        internal QueuesDownstreamRequest ValidateAndComplete(string clientId)
        {
            if (string.IsNullOrEmpty(Queue))
            {
                throw new ArgumentException("Request queue cannot be empty");
            }
            if (MaxItems < 0)
            {
                throw new ArgumentException("Request max items cannot be negative");
            }
            if (WaitTimeout < 0)
            {
                throw new ArgumentException("Request wait timeout cannot be negative");
            }
            if (VisibilitySeconds < 0)
            {
                throw new ArgumentException("Request visibility seconds cannot be negative");
            }
            if (AutoAck && VisibilitySeconds > 0)
            {
                throw new ArgumentException("Request visibility seconds cannot be set with auto ack");
            }

            return new QueuesDownstreamRequest()
            {
                RequestID = _requestId,
                Channel = _queue,
                RequestTypeData = QueuesDownstreamRequestType.Get,
                ClientID = clientId,
                MaxItems = _maxItems,
                WaitTimeout = _waitTimeout,
                AutoAck = _autoAck
            };
        }
    }
}
