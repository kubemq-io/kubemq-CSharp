using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Grpc;


namespace KubeMQ.SDK.csharp.QueueStream
{
  

    internal delegate void DownstreamRequestHandler(QueuesDownstreamRequest request);
    internal class Downstream
    {
        BlockingCollection<QueuesDownstreamRequest> _sendQueue = new BlockingCollection<QueuesDownstreamRequest>();
        private ConcurrentDictionary<string, PollResponse> _pendingResponses =
            new ConcurrentDictionary<string, PollResponse>();

        private ConcurrentDictionary<string, PollResponse> _activeResponses =
            new ConcurrentDictionary<string, PollResponse>();

        private AsyncDuplexStreamingCall<QueuesDownstreamRequest, QueuesDownstreamResponse> _downstreamConnection;
        private TaskCompletionSource<bool> _isConnectionDropped;
      
        public TaskCompletionSource<bool> IsConnectionDropped => _isConnectionDropped;
        private string _clientId;
        
        public Downstream(AsyncDuplexStreamingCall<QueuesDownstreamRequest, QueuesDownstreamResponse> downstreamConnectionConnection,string clientId)
        {
            _downstreamConnection = downstreamConnectionConnection;
            _clientId = clientId;
            _isConnectionDropped = new TaskCompletionSource<bool>();
        }

        public async Task StartResponseStream()
        {
            try
            {
                while (await  _downstreamConnection.ResponseStream.MoveNext())
                {
                    
                    QueuesDownstreamResponse response =
                        new QueuesDownstreamResponse(_downstreamConnection.ResponseStream.Current);
                    HandelResponse(response);
                }
            }
            catch (Exception e)
            {
               _isConnectionDropped.TrySetResult(true);
            }
        }

        public void SendRequest(QueuesDownstreamRequest request)
        {
            _sendQueue.Add(request);
        }
        public void StartHandelRequests()
        {
           
            Task.Run(async () =>
            {
                while (true)
                {
                    QueuesDownstreamRequest request= _sendQueue.Take();
                    try
                    {
                        request.ClientID = _clientId;
                        await _downstreamConnection.RequestStream.WriteAsync(request);    
                    }
                    catch (Exception e)
                    {
                        _isConnectionDropped.TrySetResult(true);
                        break;
                    }    
                } 
                
            });
        }
        
        private void HandelResponse(QueuesDownstreamResponse response)
        {
            Task.Run(() =>
            {
                if (response.RequestTypeData == QueuesDownstreamRequestType.Get)
                {
                    PollResponse pendingResponse;
                    if (_pendingResponses.TryRemove(response.RefRequestId, out pendingResponse))
                    {
                        if (response.Messages.Count > 0 && !response.IsError)
                        {
                            _activeResponses.TryAdd(response.TransactionId, pendingResponse);
                        }
                        else
                        {
                            pendingResponse.SendComplete();
                        }
                        pendingResponse.SetPollResponse(response, this.SendRequest);
                    }
                }
                else
                {
                    if (_activeResponses.TryGetValue(response.TransactionId, out PollResponse activeResponse))
                    {
                        if (response.TransactionComplete)
                        {
                            activeResponse.SendComplete();
                            _activeResponses.TryRemove(response.TransactionId, out activeResponse);
                            return;
                        }

                        if (response.IsError)
                        {
                            activeResponse.SendError(response.Error);
                        }
                    }
                } 
            });
        }

        internal async Task<PollResponse> Poll(PollRequest request,string clientId)
        {
            QueuesDownstreamRequest pbReq = request.ValidateAndComplete(_clientId);
            PollResponse response = new PollResponse(request);
            _pendingResponses.TryAdd(response.RequestId, response);
            SendRequest(pbReq);
            await response.WaitForResponseTask.Task;
            if (!string.IsNullOrEmpty(response.GetRequestError))
            {
                throw new Exception($"poll request error: {response.GetRequestError}");
            }
            return response;
        }
        internal void ClearResponses()
        {
            
            foreach (var  response  in _activeResponses)
            {
                response.Value.SendComplete();
            }
            _activeResponses.Clear();
            
            foreach (var  response  in _pendingResponses)
            {
            
                response.Value.WaitForResponseTask.TrySetResult(true);
                response.Value.SendComplete();
            }
            _pendingResponses.Clear();
        }
        private PollResponse MovePendingToActiveResponse(string transactionId, string requestId)
        {
            PollResponse currentResponse;
            if (_pendingResponses.TryRemove(requestId, out currentResponse))
            {
                currentResponse.TransactionId = transactionId;
                _activeResponses.TryAdd(transactionId, currentResponse);
            }

            return currentResponse;
        }

        public int ActiveTransactions ()
        {
            return _activeResponses.Count;
        }
        public int PendingTransactions ()
        {
            return _pendingResponses.Count;
        }
    }
}