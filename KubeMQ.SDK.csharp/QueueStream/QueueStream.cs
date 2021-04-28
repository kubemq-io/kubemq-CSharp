using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;

namespace KubeMQ.SDK.csharp.QueueStream
{
   
    public class QueueStream : GrpcClient
    {
        private static object syncLock = new object();
        private string _clientId = Guid.NewGuid().ToString();
        private kubemq.kubemqClient _client ;
        private Downstream _downstream = null;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken ctx;
        private bool _connected = false;
        public int ActivePollRequests
        {
            get
            {
                Downstream downstream;
                lock (syncLock)
                {
                    downstream = _downstream;
                }

                if (downstream != null)
                {
                    return downstream.ActiveTransactions();
                }
                else
                {
                    return 0;
                }
            }
        }
        public int WaitingPollRequests
        {
            get
            {
                Downstream downstream;
                lock (syncLock)
                {
                    downstream = _downstream;
                }

                if (downstream != null)
                {
                    return downstream.PendingTransactions();
                }
                else
                {
                    return 0;
                }
            }
        }
        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        public QueueStream(string address) :this (address,null,null)
        {
        }
        public QueueStream(string address,string clientId) :this (address,clientId,null)
        {
        }
        public QueueStream(string address,string clientId, string authToken) 
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                _clientId = clientId;
            }

            if (!string.IsNullOrEmpty(address))
            {
                _kubemqAddress = address;
            }
            this.addAuthToken(authToken);
            _client = GetKubeMQClient();
            ctx = _tokenSource.Token;
           
            Task.Run(async () =>
            {
                await RunDownstream();
            });
            Thread.Sleep(1000);
        }
        
        private async  Task RunDownstream()
        {
            while (!ctx.IsCancellationRequested)
            {
                
                lock (syncLock)
                {
                        try
                        {
                            _connected = false;
                            Ping();
                            var downstreamConnection = _client.QueuesDownstream(null, null, ctx);
                            _downstream = new Downstream(downstreamConnection, _clientId);
                            Task.Run(async () =>
                            {
                                _downstream.StartHandelRequests();
                                await _downstream.StartResponseStream();
                            });
                            _connected = true;
                        }
                        catch (Exception e)
                        {
                            _connected = false;
                        } 
                }

                if (_connected && _downstream!=null)
                {
                    await _downstream.IsConnectionDropped.Task;
                    lock (syncLock)
                    {
                        _connected = false;
                        _downstream.ClearResponses();
                    }    
                }

                await Task.Delay(1000);

            }
        }
        
        public async Task<PollResponse> Poll(PollRequest request)
        {
            Downstream downstream;
            bool connected;
            lock (syncLock)
            {
                downstream = _downstream;
                connected = _connected;
            }
           
            if (connected && downstream != null)
            {
                return await downstream.Poll(request, _clientId);
            }
            else
            {
                PollResponse  response= new PollResponse(request);
                response.SendError("queue client connection is not ready");
                return response;
            }
            
               
        }
        public void Close()
        {
            Downstream downstream;
            lock (syncLock)
            {
                downstream = _downstream;
            }
            downstream.ClearResponses();
            
            _tokenSource.Cancel();
        }

        
        public PingResult Ping()
        {
           return  _client.Ping(new Empty());
        }
    }
}