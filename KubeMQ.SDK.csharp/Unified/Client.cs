using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;
using KubeMQ.SDK.csharp.Unified.PubSub.Events;
namespace KubeMQ.SDK.csharp.Unified
{
    public class Client
    {
        private bool _isConnected = false;
        private Connection _cfg;
        private Transport _transport;
        private EventsClient _eventsClient;
        CancellationTokenSource _clientCts = new CancellationTokenSource();
        public Client()
        {
        }
        
        public async Task<Client> ConnectAsync(Connection cfg,CancellationToken cancellationToken)
        {
            if (_isConnected)
            {
                throw new Exception("Client already connected");
            }
            if (cfg == null)
            {
                throw new ArgumentNullException(nameof(cfg));
            }
            _clientCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cfg.Validate();
            _cfg = cfg;
            _transport = new Transport(cfg);
            await _transport.InitializeAsync(cancellationToken);
            _isConnected = _transport.IsConnected();
            _eventsClient = new EventsClient(_transport.KubeMqClient(),cfg.ClientId);
            return this;
        }


        public async Task<ServerInfo> PingAsync()
        {
            if (!_isConnected || _clientCts.Token.IsCancellationRequested)
            {
                throw new Exception("Client not connected");
            }
            return await _transport.PingAsync(_clientCts.Token);
        }
        
        public async Task SendEventAsync(Event eventToSend)
        {
            if (!_isConnected || _clientCts.Token.IsCancellationRequested)
            {
                throw new Exception("Client not connected");
            }
            eventToSend.Validate();
            CancellationTokenSource senderCts = CancellationTokenSource.CreateLinkedTokenSource(_clientCts.Token);
            try
            {
                await _eventsClient.SendAsync(eventToSend, senderCts.Token);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void SubscribeToEvents(EventsSubscription subscription, CancellationToken cancellationToken)
        {
            if (!_isConnected || _clientCts.Token.IsCancellationRequested)
            {
                throw new Exception("Client not connected");
            }
            subscription.Validate();
            CancellationTokenSource subscriberCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run((Func < Task > )(async() => {
                while (!subscriberCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _eventsClient.SubscribeAsync(subscription, subscriberCts.Token);
                    }
                    catch (Exception ex)
                    {
                        subscription.RaiseOnError(ex);
                        if (_cfg.DisableAutoReconnect)
                        {
                            break;
                        }
                        await Task.Delay(_cfg.GetReconnectIntervalDuration(), subscriberCts.Token);
                    }
                }
                
            }), subscriberCts.Token);
           
        }
        public async Task CloseAsync()
        {
            if (!_isConnected)
            {
                throw new Exception("Client not connected");
            }
            await _transport.CloseAsync();
            _clientCts.Cancel();
            _clientCts.Dispose();
            _clientCts = null;
            _isConnected = false;
        }
    }
}