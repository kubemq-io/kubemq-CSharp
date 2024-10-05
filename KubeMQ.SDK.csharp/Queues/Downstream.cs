using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queues
{
    internal delegate void DownstreamRequestHandler(QueuesDownstreamRequest request);

    internal class Downstream
    {
        private readonly BlockingCollection<QueuesDownstreamRequest> _sendQueue =
            new BlockingCollection<QueuesDownstreamRequest>();

        private readonly ConcurrentDictionary<string, PollResponse> _pendingRequests =
            new ConcurrentDictionary<string, PollResponse>();

        private readonly ConcurrentDictionary<string, PollResponse> _activeResponses =
            new ConcurrentDictionary<string, PollResponse>();

        private readonly AsyncDuplexStreamingCall<QueuesDownstreamRequest, QueuesDownstreamResponse>
            _downstreamConnection;

        public TaskCompletionSource<bool> IsConnectionDropped { get; }
       
        private readonly string _clientId;

        public Downstream(
            AsyncDuplexStreamingCall<QueuesDownstreamRequest, QueuesDownstreamResponse> downstreamConnectionConnection,
            string clientId)
        {
            _downstreamConnection = downstreamConnectionConnection;
            _clientId = clientId;
            IsConnectionDropped = new TaskCompletionSource<bool>();
        }

        public async Task StartResponseStream()
        {
           
            try
            {
                while (await _downstreamConnection.ResponseStream.MoveNext())
                {
                    var response =
                        new QueuesDownstreamResponse(_downstreamConnection.ResponseStream.Current);
                    HandelResponse(response);
                }

                
            }
            catch (Exception e)
            {
                if (e is RpcException { StatusCode: StatusCode.Cancelled })
                {
                    return;
                }
                IsConnectionDropped.TrySetResult(true);
                throw;

            }
        }

        public void SendRequest(QueuesDownstreamRequest request)
        {
            _sendQueue.Add(request);
        }

        public bool EmptyRequestsQueue()
        {
            return _sendQueue.Count == 0;
        }
        public void StartHandelRequests()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                  
                    var request = _sendQueue.Take();
                    try
                    {
                        request.ClientID = _clientId;
                        await _downstreamConnection.RequestStream.WriteAsync(request);
                    }
                    catch (Exception )
                    {
                        IsConnectionDropped.TrySetResult(true);
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
                    if (_pendingRequests.TryRemove(response.RefRequestId, out pendingResponse))
                    {
                        pendingResponse= pendingResponse.SetPollResponse(response, SendRequest);
                        if (response.Messages.Count > 0 && !response.IsError)
                            _activeResponses.TryAdd(response.TransactionId, pendingResponse);
                        else
                            pendingResponse.SendComplete();
                        
                    }
                }
                else
                {
                    if (_activeResponses.TryGetValue(response.TransactionId, out var activeResponse))
                    {
                        if (response.TransactionComplete)
                        {
                            activeResponse.SendComplete();
                            _activeResponses.TryRemove(response.TransactionId, out activeResponse);
                            return;
                        }

                        if (response.IsError) activeResponse.SendError(response.Error);
                    }
                }
            });
        }

        internal async Task<PollResponse> Poll(PollRequest request, string clientId)
        {
            var pbReq = request.ValidateAndComplete(_clientId);
            var response = new PollResponse(request);
            _pendingRequests.TryAdd(response.RequestId, response);
            SendRequest(pbReq);
            await response.WaitForResponseTask.Task;
            if (!string.IsNullOrEmpty(response.Error)) throw new Exception($"poll request error: {response.Error}");
            return response;
        }

        internal void ClearResponses()
        {
            foreach (var response in _activeResponses) response.Value.SendComplete();
            _activeResponses.Clear();

            foreach (var response in _pendingRequests)
            {
                response.Value.WaitForResponseTask.TrySetResult(true);
                response.Value.SendComplete();
            }

            _pendingRequests.Clear();
        }

        private PollResponse MovePendingToActiveResponse(string transactionId, string requestId)
        {
            PollResponse currentResponse;
            if (_pendingRequests.TryRemove(requestId, out currentResponse))
            {
                currentResponse.TransactionId = transactionId;
                _activeResponses.TryAdd(transactionId, currentResponse);
            }

            return currentResponse;
        }

        public int ActiveTransactions()
        {
            return _activeResponses.Count;
        }

        public int PendingTransactions()
        {
            return _pendingRequests.Count;
        }
    }
}