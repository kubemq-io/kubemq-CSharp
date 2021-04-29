using System;
using KubeMQ.Grpc;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Grpc.Core;
namespace KubeMQ.SDK.csharp.QueueStream
{
    internal class Upstream
    {
        private readonly BlockingCollection<QueuesUpstreamRequest> _sendQueue =
            new BlockingCollection<QueuesUpstreamRequest>();
        private readonly ConcurrentDictionary<string, SendResponse> _pendingRequests =
            new ConcurrentDictionary<string, SendResponse>();
        private readonly AsyncDuplexStreamingCall<QueuesUpstreamRequest, QueuesUpstreamResponse>
            _upstreamConnection;
        private readonly string _clientId;
        public TaskCompletionSource<bool> IsConnectionDropped { get; }
        
        public Upstream(
            AsyncDuplexStreamingCall<QueuesUpstreamRequest, QueuesUpstreamResponse> upstreamConnection,
            string clientId)
        {
            _upstreamConnection = upstreamConnection;
            _clientId = clientId;
            IsConnectionDropped = new TaskCompletionSource<bool>();
        }
        
        public async Task StartResponseStream()
        {
            try
            {
                while (await _upstreamConnection.ResponseStream.MoveNext())
                {
                    var response =
                        new QueuesUpstreamResponse(_upstreamConnection.ResponseStream.Current);
                    HandelResponse(response);
                }
            }
            catch (Exception )
            {
                IsConnectionDropped.TrySetResult(true);
                throw;
            }
        }
        
        public void SendRequest(QueuesUpstreamRequest request)
        {
            _sendQueue.Add(request);
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
                        
                        await _upstreamConnection.RequestStream.WriteAsync(request);
                    }
                    catch (Exception )
                    {
                        IsConnectionDropped.TrySetResult(true);
                        break;
                    }
                }
            });
        }
        private void HandelResponse(QueuesUpstreamResponse response)
        {
            Task.Run(() =>
            {
                SendResponse pendingResponse;
                if (_pendingRequests.TryRemove(response.RefRequestID, out pendingResponse))
                {
                    pendingResponse.setSendResponse(response);
                }
            });
        }
        internal async Task<SendResponse> Send(SendRequest request, string clientId)
        {
            var pbReq = request.ValidateAndComplete(_clientId);
            var response = new SendResponse(request);
            _pendingRequests.TryAdd(response.RequestId, response);
            SendRequest(pbReq);
            await response.WaitForResponseTask.Task;
            if (!string.IsNullOrEmpty(response.Error)) throw new Exception($"send request error: {response.Error}");
            return response;
        }
        
        internal void ClearResponses()
        {
            foreach (var response in _pendingRequests)
            {
                response.Value.WaitForResponseTask.TrySetResult(true);
            }
            _pendingRequests.Clear();
        }
        public int PendingTransactions()
        {
            return _pendingRequests.Count;
        }
    }
}