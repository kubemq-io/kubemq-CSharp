using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.QueueStream
{
    /// <summary>
    /// PollResponse
    /// </summary>
    public class PollResponse
    {
        private List<Message> _messages;
        private string _transactionId;
        private PollRequest _request;
        private TaskCompletionSource<bool> _waitForResponseTask = new TaskCompletionSource<bool>();
        private DownstreamRequestHandler _requestHandler;
        private string _error = null;
        
        /// <summary>
        /// Indicate if the response has received messages
        /// </summary>
        
        public bool HasMessages => _messages.Count>0;
        /// <summary>
        /// Received messages list
        /// </summary>

        public List<Message> Messages => _messages;
        public TaskCompletionSource<bool> WaitForResponseTask => _waitForResponseTask;

        internal string RequestId
        {
            get => _request.RequestId;
        }
        internal string TransactionId
        {
            get => _transactionId;
            set => _transactionId = value;
        }
        /// <summary>
        /// Error message received for this poll request
        /// </summary>
        public string Error => _error;


        internal PollResponse(PollRequest request)
        {
            _request = request;
            _messages = new List<Message>() ;
        }

        internal PollResponse SetPollResponse(QueuesDownstreamResponse response, DownstreamRequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
            _transactionId = response.TransactionId;
            foreach (var message in response.Messages)
            {
              _messages.Add(new Message(message, requestHandler, _transactionId,_request.VisibilitySeconds,_request.AutoAck));
            }

            if (response.IsError)
            {
                _error = response.Error;
            }
            _waitForResponseTask.TrySetResult(true);
            return this;
        }

        private void CheckValidRequest()
        {
            if (_request.AutoAck)
            {
                throw new Exception("this operation cannot submitted with AutoAck requests");
            }

            if (_messages.Count == 0)
            {
                throw new Exception("this operation cannot submitted on empty response messages");
            }
        }
        /// <summary>
        /// Ack all messages in this response (accept all)
        /// </summary>
        public void AckAll()
        {
            CheckValidRequest();
            QueuesDownstreamRequest ackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.AckAll,
                RefTransactionId = _transactionId,
                
            };
            _requestHandler(ackRequest);
            MarkCompleted("AckAll");
        }
        /// <summary>
        /// NAck all messages in this response (reject all)
        /// </summary>
        public void NAckAll()
        {
            CheckValidRequest();
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackAll,
                RefTransactionId = _transactionId,

            };
            _requestHandler(nackRequest);
            MarkCompleted("NAckAll");
        }
        /// <summary>
        /// ReQueueAll all messages in this response to a new queue
        /// </summary>
        /// <param name="queue">requeue  queue name</param> 
        public void ReQueueAll(string queue)
        {
            if (string.IsNullOrEmpty(queue))
            {
                throw new Exception("requeue queue name must have a valid value");
            }
            CheckValidRequest();
            QueuesDownstreamRequest requeueRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.ReQueueAll,
                RefTransactionId = _transactionId,
                ReQueueChannel = queue
            };
            _requestHandler(requeueRequest);
            MarkCompleted("ReQueueAll");
        }

        internal void SendError(string err)
        {
            _error = err;
            _request.SendOnError(err);
        }
        internal void SendComplete()
        {
            _request.SendOnComplete();
        }
        
        private void MarkCompleted(string reason)
        {
            foreach (var message in _messages)
            {
                message.SetComplete(reason);
            }
        }
    }
}