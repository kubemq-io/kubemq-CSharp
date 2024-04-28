using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Unified.Config;
using KubeMQ.SDK.csharp.Unified.Grpc;
using KubeMQ.SDK.csharp.Unified.PubSub.Events;
using KubeMQ.SDK.csharp.Unified.PubSub.EventsStore;
using KubeMQ.SDK.csharp.Unified.Results;

namespace KubeMQ.SDK.csharp.Unified
{
    public class Client
    {
        private bool _isConnected = false;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Connection _cfg;
        private Transport _transport;
        private EventsClient _eventsClient;
        private EventsStoreClient _eventsStoreClient;

        public Client()
        {
        }

        public async Task<ConnectAsyncResult> ConnectAsync(Connection cfg, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_isConnected)
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
                    _cfg = cfg;
                    _transport = new Transport(cfg);
                    await _transport.InitializeAsync(cancellationToken);
                    _isConnected = _transport.IsConnected();
                    _eventsClient = new EventsClient(_transport.KubeMqClient(), cfg.ClientId);
                    _eventsStoreClient = new EventsStoreClient(_transport.KubeMqClient(), cfg.ClientId);
                }
                catch (Exception ex)
                {
                    _cfg = null;
                    _transport = null;
                    _eventsClient = null;
                    _eventsStoreClient = null;
                    _isConnected = false;
                    return new ConnectAsyncResult() { IsSuccess = false, ErrorMessage = ex.Message };
                }

                return new ConnectAsyncResult() { IsSuccess = true };
            }
            finally
            {
                _lock.Release();
            }
        }



        public async Task<PingAsyncResult> PingAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected )
                {
                    return new PingAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                ServerInfo result = await _transport.PingAsync(cancellationToken);
                return new PingAsyncResult() { IsSuccess = true, ServerInfo = result };
            }
            catch (Exception ex)
            {
                return new PingAsyncResult() { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<SendEventAsyncResult> SendEventAsync(Event eventToSend,CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected)
                {
                    return new SendEventAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                await _eventsClient.SendAsync(eventToSend, cancellationToken);
            }
            catch (Exception e)
            {
                return new SendEventAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            return new SendEventAsyncResult() { IsSuccess = true };
        }

         public SubscribeToEventsResult SubscribeToEvents(EventsSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected )
                {
                    return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _eventsClient.SubscribeAsync(subscription, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            subscription.RaiseOnError(ex);
                            if (_cfg.DisableAutoReconnect)
                            {
                                break;
                            }

                            await Task.Delay(_cfg.GetReconnectIntervalDuration(), cancellationToken);
                        }
                    }

                }, cancellationToken);
            }
            catch (Exception e)
            {
                return new SubscribeToEventsResult() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new SubscribeToEventsResult() { IsSuccess = true };
        }

        public async Task<SendEventStoreAsyncResult> SendEventStoreAsync(EventStore eventStoreToSend,CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected)
                {
                    return new SendEventStoreAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                await _eventsStoreClient.SendAsync(eventStoreToSend, cancellationToken);
            }
            catch (Exception e)
            {
                return new SendEventStoreAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            return new SendEventStoreAsyncResult() { IsSuccess = true };
        }
        
        
        public SubscribeToEventsStoreResult SubscribeToEventsStore(EventsStoreSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!_isConnected )
                {
                    return new SubscribeToEventsStoreResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _eventsStoreClient.SubscribeAsync(subscription, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            subscription.RaiseOnError(ex);
                            if (_cfg.DisableAutoReconnect)
                            {
                                break;
                            }

                            await Task.Delay(_cfg.GetReconnectIntervalDuration(), cancellationToken);
                        }
                    }

                }, cancellationToken);
            }
            catch (Exception e)
            {
                return new SubscribeToEventsStoreResult() { IsSuccess = false, ErrorMessage = e.Message };
            }

            return new SubscribeToEventsStoreResult() { IsSuccess = true };
        }
        public async Task<CloseAsyncResult> CloseAsync()
        {
            try
            {
                await _lock.WaitAsync();
                if (!_isConnected)
                {
                    return new CloseAsyncResult() { IsSuccess = false, ErrorMessage = "Client not connected" };
                }

                if (_transport != null)
                {
                    await _transport.CloseAsync();
                    _transport = null;
                }
                _isConnected = false;
            }
            catch (Exception e)
            {
                return new CloseAsyncResult() { IsSuccess = false, ErrorMessage = e.Message };
            }
            finally
            {
                _lock.Release();
            }

            return new CloseAsyncResult() { IsSuccess = true };
       }
    }
}