
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using System.Net.Http;
using System.Net.Security;
using Grpc.Core.Interceptors;
using KubeMQ.SDK.csharp.Config;
using static KubeMQ.Grpc.kubemq;
namespace KubeMQ.SDK.csharp.Transport
{
    public class Transport
    {
        private readonly Connection _opts;
        private Channel _channel;
        private kubemqClient _client;
        private CancellationToken _clientCts;
        public Transport(Connection cfg)
        {
            _opts = cfg;
        }

        public async Task<Transport> InitializeAsync(CancellationToken cancellationToken)
        {
            List<ChannelOption> channelOptions = new List<ChannelOption>
            {
                new ChannelOption(ChannelOptions.MaxSendMessageLength, _opts.MaxSendSize),
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, _opts.MaxReceiveSize),
                new ChannelOption("grpc.keepalive_time_ms", _opts.KeepAlive.PingIntervalInSeconds * 1000),
                new ChannelOption("grpc.keepalive_timeout_ms", _opts.KeepAlive.PingTimeOutInSeconds * 1000),
                new ChannelOption("grpc.keepalive_permit_without_calls", 1),
                new ChannelOption("grpc.http2.min_time_between_pings_ms", _opts.KeepAlive.PingIntervalInSeconds * 1000),
                new ChannelOption("grpc.http2.min_ping_interval_without_data_ms", _opts.KeepAlive.PingIntervalInSeconds * 1000),
            };
            Channel channel = null;
            if (_opts.Tls != null && _opts.Tls.Enabled)
            {
                try
                {
                    SslCredentials credentials = GetSslCredentials(_opts.Tls);
                    channel = new Channel(_opts.Address, credentials, channelOptions);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }else
            {
                channel = new Channel(_opts.Address,  ChannelCredentials.Insecure, channelOptions);
            }
            var interceptor = new CustomInterceptor(_opts);
            CallInvoker interceptedInvoker = channel.Intercept(interceptor);

            _channel = channel;
            _client = new kubemqClient(interceptedInvoker);
            try
            {
                var response = await PingAsync( cancellationToken);
            }
            catch (Exception ex)
            {   
                channel.ShutdownAsync().Wait(cancellationToken);
                throw;
            }
            _clientCts = cancellationToken;
            return this;
        }
        private SslCredentials GetSslCredentials(TlsConfig tlsConfig)
        {
            KeyCertificatePair keyCertificatePair = null;
            if (!string.IsNullOrEmpty(tlsConfig.CertFile) && !string.IsNullOrEmpty(tlsConfig.KeyFile))
            {
                keyCertificatePair = new KeyCertificatePair(File.ReadAllText(tlsConfig.CertFile), File.ReadAllText(tlsConfig.KeyFile));
            }

            SslCredentials sslCredentials;
            if (!string.IsNullOrEmpty(tlsConfig.CaFile))
            {
                // If a CA certificate is provided, use it for SSL credentials
                sslCredentials = new SslCredentials(File.ReadAllText(tlsConfig.CaFile), keyCertificatePair);
            }
            else
            {
                // Otherwise, use the certificate without a CA
                sslCredentials = keyCertificatePair == null ? new SslCredentials() : new SslCredentials("", keyCertificatePair);
            }
            return sslCredentials;
        }
        

        public async Task<ServerInfo> PingAsync(CancellationToken cancellationToken)
        {
            var response = await _client.PingAsync(new KubeMQ.Grpc.Empty(), cancellationToken: cancellationToken);
            return new ServerInfo()
            {
                Host = response.Host,
                Version = response.Version,
                ServerStartTime = response.ServerStartTime,
                ServerUpTimeSeconds = response.ServerUpTimeSeconds
            };
        }
        public kubemqClient KubeMqClient()
        {
            return _client;
        }

        public bool IsConnected()
        {
            if (_clientCts.IsCancellationRequested) return false;
            return _channel.State == ChannelState.Ready;
        }
        public async Task CloseAsync()
        {
            await _channel.ShutdownAsync();
        }
    }
}