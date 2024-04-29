using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;
using static KubeMQ.SDK.csharp.Common.Common;
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
    /// Get Queue information
        /// </summary>
        public QueuesInfo QueuesInfo(string filter)
        {
                QueuesInfoRequest req = new QueuesInfoRequest();
                QueuesInfoResponse resp = _client.QueuesInfo(new QueuesInfoRequest()
                {
                    RequestID = Guid.NewGuid().ToString(),
                    QueueName = filter,
                });
                return new QueuesInfo(resp);
        }
        /// <summary>
        /// Creates a channel with the given name.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task representing the asynchronous channel creation operation.</returns>
        public async Task<CommonAsyncResult> CreateChannel (string channelName) {
            return await CreateDeleteChannel (GetKubeMQClient (), _clientId, channelName, "queues", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A task that represents the asynchronous delete operation. The task result is of type CommonAsyncResult.</returns>
        public async Task<CommonAsyncResult> DeleteChannel (string channelName) {
            return await CreateDeleteChannel (GetKubeMQClient (), _clientId, channelName, "queues", false);
        }

        /// <summary>
        /// Lists all the channels within the KubeMQ server that match the search criteria.
        /// </summary>
        /// <param name="search">The search criteria to filter the channels. If left empty, all channels will be returned.</param>
        /// <returns>A task result containing a ListQueuesAsyncResult object with the channels that match the search criteria.</returns>
        public async Task<ListQueuesAsyncResult> ListChannels (string search = "") {
            return await ListQueuesChannels(GetKubeMQClient(), _clientId, search, "queues");
        }
        /// <summary>
        /// Close Queue Client - all pending transactions will be cancelled 
        /// </summary>
        public bool EmptyRequestsQueue()
        {
            return _downstream.EmptyRequestsQueue();
        }
        public void Close()
        {
            do
            {
                Thread.Sleep(1);
            }
            while(!_downstream.EmptyRequestsQueue());
            Thread.Sleep(100);
            _tokenSource.Cancel();
        }

        internal PingResult Ping()
        {
           return  _client.Ping(new Empty());
        }
    }
}