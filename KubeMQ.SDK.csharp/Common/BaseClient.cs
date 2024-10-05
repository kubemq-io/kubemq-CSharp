using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Transport;
using static KubeMQ.Grpc.kubemq;
using PingResult = KubeMQ.SDK.csharp.Results.PingResult;
using Result = KubeMQ.SDK.csharp.Results.Result;

namespace KubeMQ.SDK.csharp.Common
{
    public class BaseClient
{
        internal kubemqClient KubemqClient;
        internal Configuration Cfg;
        internal bool IsConnected;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Transport.Transport _transport;
        private static readonly string RequestChannel = "kubemq.cluster.internal.requests";
        
       
        /// <summary>
        /// Connects to the KubeMQ server using the provided connection configuration.
        /// </summary>
        /// <param name="cfg">The connection configuration.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the connect operation.</param>
        /// <returns>A task that represents the asynchronous connect operation. The task result is a <see cref="Result"/> object.</returns>
        public async Task<Result> Connect(Configuration cfg, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (IsConnected)
                {
                    throw new Exception("Client already connected");
                }

                if (cfg == null)
                {
                    throw new ArgumentNullException(nameof(cfg));
                }

                try
                {
                    cfg.Validate();
                    Cfg = cfg;
                    _transport = new Transport.Transport(cfg);
                    await _transport.InitializeAsync(cancellationToken);
                    IsConnected = _transport.IsConnected();
                    KubemqClient = _transport.KubeMqClient();
                }
                catch (Exception ex)
                {
                    _transport = null;
                    KubemqClient = null;
                    IsConnected = false;
                    return new Result(ex);
                }

                return new Result();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Sends a Ping request to the KubeMQ server to verify the connection status.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken to observe while waiting for the response.</param>
        /// <returns>A PingResult object containing the result of the Ping operation.</returns>
        public async Task<PingResult> Ping(CancellationToken cancellationToken)
        {
            try
            {
                if (!IsConnected)
                {
                    return new PingResult( "Client not connected");
                }

                ServerInfo result = await _transport.PingAsync(cancellationToken);
                return new PingResult(result) ;
            }
            catch (Exception ex)
            {
                return new PingResult(ex);
            }
        }

        /// <summary>
        /// Closes the connection to the KubeMQ server.
        /// </summary>
        /// <returns>A <see cref="Result"/> object indicating the result of closing the connection.</returns>
        public async Task<Result> CloseClient()
        {
            try
            {
                await _lock.WaitAsync();
                if (!IsConnected)
                {
                    return new Result("Client not connected");
                }

                if (_transport != null)
                {
                    await _transport.CloseAsync();
                    _transport = null;
                }
                IsConnected = false;
            }
            catch (Exception e)
            {
                return new Result(e);
            }
            finally
            {
                _lock.Release();
            }

            return new Result();
        }
        internal  async Task<Result> CreateDeleteChannel(string clientId,
            string channelName, string channelType, bool isCreate)
        {
            var request = CreateRequest(clientId, channelType, channelName);
            request.Metadata = isCreate ? "create-channel" : "delete-channel";
            return await ExecuteRequest(request);
        }
        private  Request CreateRequest(string clientId, string channelType, string channelName)
        {
            return new Request
            {
                RequestID = Guid.NewGuid().ToString(),
                ClientID = clientId,
                RequestTypeData = Request.Types.RequestType.Query,
                Channel = RequestChannel,
                Timeout = 10000,
                Tags =  { { "channel_type", channelType },
                    { "channel", channelName },
                    { "client_id", clientId }} ,
            };
        }
        
        private async Task<Result> ExecuteRequest( Request request)
        {
            try
            {
                Response response = await KubemqClient.SendRequestAsync(request);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    return new Result(response.Error);
                }
                else
                {
                    return new Result();
                }
            }
            catch (Exception e)
            {
                return new Result(e);
            }
        }
        
       
        
        private dynamic HandleListErrors<T>(Response response)
        {
            if (!string.IsNullOrEmpty(response.Error))
            {
                return Activator.CreateInstance(typeof(T), new object[] {  response.Error });
            }
            else
            {
                return Activator.CreateInstance(typeof(T), new object[] { response.Body.ToByteArray() });
            }
        }
        
        private async Task<Response> List(string clientId, string search, string channelType)
        {
            var request = new Request
            {
                RequestID = Guid.NewGuid().ToString(),
                RequestTypeData = Request.Types.RequestType.Query,
                Channel = RequestChannel,
                ClientID = clientId,
                Timeout = 10000,
                Metadata = ("list-channels"),
                Tags = { { "channel_type", channelType }, { "search", search }, { "client_id", clientId } }
            };
            return await KubemqClient.SendRequestAsync(request);
        }
        
        internal async Task<ListPubSubAsyncResult> ListPubSubChannels(string clientId, string search, string channelType)
        {
            return HandleListErrors<ListPubSubAsyncResult>(await List(clientId, search, channelType));
        }

        internal  async Task<ListQueuesAsyncResult> ListQueuesChannels(string clientId, string search, string channelType)
        {
            return HandleListErrors<ListQueuesAsyncResult>(await List(clientId, search, channelType));
        }
        internal async Task<ListCqAsyncResult> ListCqChannels(string clientId, string search, string channelType)
        {
            return HandleListErrors<ListCqAsyncResult>(await List(clientId, search, channelType));
        }

    }
    
    
}