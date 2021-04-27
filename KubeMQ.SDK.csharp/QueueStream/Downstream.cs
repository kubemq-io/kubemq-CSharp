using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Grpc;


namespace KubeMQ.SDK.csharp.QueueStream
{
    internal delegate void DownstreamResponseHandler(QueuesDownstreamResponse response);

    internal delegate Task DownstreamRequestHandler(QueuesDownstreamRequest request);

    internal class Downstream
    {
        private ConcurrentDictionary<string, PollResponse> _pendingResponses =
            new ConcurrentDictionary<string, PollResponse>();

        private ConcurrentDictionary<string, PollResponse> _activeResponses =
            new ConcurrentDictionary<string, PollResponse>();

        private AsyncDuplexStreamingCall<QueuesDownstreamRequest, QueuesDownstreamResponse> _downstream;    
        private kubemq.kubemqClient _client;
        private DownstreamResponseHandler _responseHandler;
        private TaskCompletionSource<bool> _waitForConnectionTask = new TaskCompletionSource<bool>();

        public TaskCompletionSource<bool> WaitForConnectionTask => _waitForConnectionTask;

        private string _clientId;

        public Downstream(kubemq.kubemqClient client, DownstreamResponseHandler responseHandler, string clientId)
        {
            _client = client;
            _clientId = clientId;
            _responseHandler = responseHandler;
        }

        private async Task StartResponseStream(IAsyncStreamReader<QueuesDownstreamResponse> responseStream)
        {
            _waitForConnectionTask.SetResult(true);
            while (await responseStream.MoveNext())
            {
                QueuesDownstreamResponse response =
                    new QueuesDownstreamResponse(responseStream.Current);
                Console.WriteLine($"Getting Response: {response}");
                HandelResponse(response);
            }
        }

        public async Task SendRequest(QueuesDownstreamRequest request)
        {
            request.ClientID = _clientId;
            Console.WriteLine($"Sending Request: {request}");
            await _downstream.RequestStream.WriteAsync(request);
        }

        public void Connect(CancellationToken cancellationToken = new CancellationToken())
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine("token cancelled... return ");
                            return;
                        }

                        Console.WriteLine("Connecting...");
                        _downstream = _client.QueuesDownstream(null, null, cancellationToken);
                        var responseTask = Task.Run(async () =>
                        {
                            try
                            {
                                await StartResponseStream(_downstream.ResponseStream);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"exception on response - {e.Message}");
                                throw;
                            }
                        });
                        await responseTask;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"exception on main loop");
                    }

                    await Task.Delay(1000);
                }
            });
        }


        private void HandelResponse(QueuesDownstreamResponse response)
        {
            Task.Run(() =>
            {
                foreach (var  kv  in _activeResponses)
                {
                    Console.WriteLine(kv.Key);
                }
                if (response.RequestTypeData == QueuesDownstreamRequestType.Get)
                {
                    
                    var pollResponse = MovePendingToActiveResponse(response.TransactionId, response.RefRequestId);
                    if (pollResponse != null)
                    {
                        pollResponse.SetPollResponse(response, this.SendRequest);
                    }
                    else
                    {
                        return;
                    }
                  
                    if (response.IsError)
                    {
                      
                        _activeResponses.TryRemove(response.TransactionId, out pollResponse);
                        return;
                    }
                    
                    // if (pollResponse.Messages.Count == 0)
                    // {
                    //     _activeResponses.TryRemove(response.TransactionId, out pollResponse);
                    //     pollResponse.SendComplete();
                    // }
                    
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
                    else
                    {
                        Console.WriteLine("transaction not found");
                      
                    }
                } 
            });
        }

        internal async Task<PollResponse> Poll(PollRequest request,string clientId)
        {
            QueuesDownstreamRequest pbReq = request.ValidateAndComplete(_clientId);
            PollResponse response = new PollResponse(request);
            _pendingResponses.TryAdd(response.RequestId, response);
            await SendRequest(pbReq);
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
    }
}