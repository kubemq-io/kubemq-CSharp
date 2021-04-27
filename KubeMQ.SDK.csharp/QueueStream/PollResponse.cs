using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.QueueStream
{
    public class PollResponse
    {
        private List<Message> _messages;
        private string _transactionId;
        private PollRequest _request;
        private string _getRequestError = null;
        private TaskCompletionSource<bool> _waitForResponseTask = new TaskCompletionSource<bool>();
        private DownstreamRequestHandler _requestHandler;
        
        public string GetRequestError => _getRequestError;
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
        

        internal PollResponse(PollRequest request)
        {
            _request = request;
        }

        internal PollResponse SetPollResponse(QueuesDownstreamResponse response, DownstreamRequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
            _transactionId = response.TransactionId;
            _messages = new List<Message>() ;
            foreach (var message in response.Messages)
            {
              _messages.Add(new Message(message, requestHandler, _transactionId));
            }

            if (response.IsError)
            {
                _getRequestError = response.Error;
            }
            _waitForResponseTask.SetResult(true);
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
        public void AckAll()
        {
            CheckValidRequest();
            QueuesDownstreamRequest ackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.AckAll,
                RefTransactionId = _transactionId,
                
            };
            _requestHandler(ackRequest);
        }
        public void NAckAll()
        {
            CheckValidRequest();
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackAll,
                RefTransactionId = _transactionId,

            };
            _requestHandler(nackRequest);
        }
        public void ReQueueAll(string queue)
        {
            if (string.IsNullOrEmpty(queue))
            {
                throw new Exception("requeu queue name must have a valid value");
            }
            CheckValidRequest();
            QueuesDownstreamRequest requeueRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.ReQueueAll,
                RefTransactionId = _transactionId,
                ReQueueChannel = queue
            };
            _requestHandler(requeueRequest);
        }

        public void SendError(string err)
        {
            _request.SendOnError(err);
        }
        public void SendComplete()
        {
            _request.SendOnComplete();
        }
    }
}