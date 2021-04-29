using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;

namespace KubeMQ.SDK.csharp.QueueStream
{
   
    public class QueueStream : GrpcClient
    {
        private static object _downstreamSyncLock = new object();
        private static object _upstreamSyncLock = new object();
        private string _clientId = Guid.NewGuid().ToString();
        private Downstream _downstream = null;
        private Upstream _upstream = null;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken ctx;
        private bool _connected = false;
        public int WaitingSendRequests
        {
            get
            {
                Upstream upstream ;
                lock (_upstreamSyncLock)
                {
                    upstream = _upstream;
                }

                if (upstream != null)
                {
                    return upstream.PendingTransactions();
                }
                else
                {
                    return 0;
                }
            }
        }
        public int ActivePollRequests
        {
            get
            {
                Downstream downstream;
                lock (_downstreamSyncLock)
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
                lock (_downstreamSyncLock)
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
                await RunStreams();
            });
            Thread.Sleep(1000);
        }
        
        private async  Task RunStreams()
        {
            while (!ctx.IsCancellationRequested)
            {

                lock (_downstreamSyncLock)
                {
                    lock (_upstreamSyncLock)
                    {


                        try
                        {
                            _connected = false;
                            Ping();
                            var downstreamConnection = _client.QueuesDownstream(Metadata, null, ctx);
                            var upstreamConnection = _client.QueuesUpstream(Metadata, null, ctx);
                            _downstream = new Downstream(downstreamConnection, _clientId);
                            _upstream = new Upstream(upstreamConnection, _clientId);
                            Task.Run(async () =>
                            {
                                _downstream.StartHandelRequests();
                                try
                                {
                                    await _downstream.StartResponseStream();
                                }
                                catch (Exception e)
                                {
                                  Console.WriteLine(e);  
                                }
                                
                            });
                            Task.Run(async () =>
                            {
                                _upstream.StartHandelRequests();
                              
                                try
                                {
                                    await _upstream.StartResponseStream();
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);  
                                }
                            });
                            _connected = true;
                        }
                        catch (Exception )
                        {
                            _connected = false;
                        }
                    }
                }
                
                if (_connected)
                {
                    if (_downstream != null && _upstream !=null)
                    {
                       
                        await Task.WhenAny(_downstream.IsConnectionDropped.Task,_upstream.IsConnectionDropped.Task);
                        
                        lock (_downstreamSyncLock)
                        {
                            _connected = false;
                            _downstream.ClearResponses();
                        }
                        lock (_upstreamSyncLock)
                        {
                            _upstream.ClearResponses();
                        }    
                    }
                }

                await Task.Delay(1000);

            }
        }
        /// <summary>
        /// Poll a list of messages from a queue
        /// </summary>
        /// <param name="request">Poll request object</param>
        public async Task<PollResponse> Poll(PollRequest request)
        {
            Downstream downstream;
            bool connected;
            lock (_downstreamSyncLock)
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
                if (ctx.IsCancellationRequested)
                {
                    throw new Exception("queue client closed");
                }
                throw new Exception("queue client connection is not ready");
            }
        }
        /// <summary>
        /// Send List of Queue Messages
        /// </summary>
        /// <param name="request">Send request object</param>
        public async Task<SendResponse> Send(SendRequest request)
        {
            Upstream upstream ;
            bool connected;
            lock (_upstream)
            {
                upstream = _upstream;
                connected = _connected;
            }
           
            if (connected && upstream != null)
            {
                return await upstream.Send(request, _clientId);
            }
            else
            {
                if (ctx.IsCancellationRequested)
                {
                    throw new Exception("queue client closed");
                }
                throw new Exception("queue client connection is not ready");
            }
        }
        /// <summary>
        /// Close Queue Client - all pending transactions will be cancelled 
        /// </summary>
        public void Close()
        {
            // Downstream downstream;
            // lock (_downstreamSyncLock)
            // {
            //     downstream = _downstream;
            // }
            // downstream.ClearResponses();
            
            _tokenSource.Cancel();
        }

        internal PingResult Ping()
        {
           return  _client.Ping(new Empty());
        }
    }
}