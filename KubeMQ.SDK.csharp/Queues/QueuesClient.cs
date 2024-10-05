using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;
using PingResult = KubeMQ.Grpc.PingResult;
using Result = KubeMQ.SDK.csharp.Results.Result;

namespace KubeMQ.SDK.csharp.Queues
{
   
    public class QueuesClient : BaseClient
    {
        private static object _downstreamSyncLock = new object();
        private static object _upstreamSyncLock = new object();
        
        private Downstream _downstream = null;
        private Upstream _upstream = null;
        private bool _connected = false;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        public async Task<Result>  Connect(Configuration cfg, CancellationToken cancellationToken)
        {
            Result result = await base.Connect(cfg, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }
            
            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            RunStreams();
            await Task.Delay(1000, cancellationToken);
            return result;
        }
        private async  Task RunStreams()
        {
            while (!_tokenSource.IsCancellationRequested)
            {

                lock (_downstreamSyncLock)
                {
                    lock (_upstreamSyncLock)
                    {


                        try
                        {
                            _connected = false;
                            Ping();
                            var downstreamConnection = KubemqClient.QueuesDownstream(null,null, _tokenSource.Token);
                            var upstreamConnection = KubemqClient.QueuesUpstream(null, null, _tokenSource.Token);
                            _downstream = new Downstream(downstreamConnection, Cfg.ClientId);
                            _upstream = new Upstream(upstreamConnection,  Cfg.ClientId);
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
                                
                            }, _tokenSource.Token);
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
                            }, _tokenSource.Token);
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

                await Task.Delay(1000, _tokenSource.Token);

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
                return await downstream.Poll(request, Cfg.ClientId);
            }
            else
            {
                if (_tokenSource.IsCancellationRequested)
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
                return await upstream.Send(request, Cfg.ClientId);
            }
            else
            {
                if (_tokenSource.IsCancellationRequested)
                {
                    throw new Exception("queue client closed");
                }
                throw new Exception("queue client connection is not ready");
            }
        }
        /// <summary>
        /// Send A single Queue Message
        /// </summary>
        /// <param name="message">QueueMessage</param>
        public async Task<SendResponse> Send(Message message)
        {
            List<Message> messages = new List<Message> { message };
            SendRequest request = new SendRequest(messages);
            return await Send(request);
        }
        
    
        /// <summary>
        /// Creates a channel with the given name.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task representing the asynchronous channel creation operation.</returns>
        public  Task<Result> Create (string channelName) {
            return CreateDeleteChannel (Cfg.ClientId, channelName, "queues", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A task that represents the asynchronous delete operation. The task result is of type CommonAsyncResult.</returns>
        public  Task<Result> Delete (string channelName) {
            return  CreateDeleteChannel (Cfg.ClientId, channelName,"queues", false);
        }

        /// <summary>
        /// Lists all the channels within the KubeMQ server that match the search criteria.
        /// </summary>
        /// <param name="search">The search criteria to filter the channels. If left empty, all channels will be returned.</param>
        /// <returns>A task result containing a ListQueuesAsyncResult object with the channels that match the search criteria.</returns>
        public  Task<ListQueuesAsyncResult> List (string search = "") {
            return  ListQueuesChannels(Cfg.ClientId, search, "queues");
        }
        /// <summary>
        /// Close Queue Client - all pending transactions will be cancelled 
        /// </summary>
        public bool EmptyRequestsQueue()
        {
            return _downstream.EmptyRequestsQueue();
        }
        public async Task<Result> Close()
        {
            while(!_downstream.EmptyRequestsQueue());
            _tokenSource.Cancel();
            // _tokenSource.Dispose();
            return await base.CloseClient();
        }

        internal PingResult Ping()
        {
           return  KubemqClient.Ping(new Empty());
        }
        
       
    }
}