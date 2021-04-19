using Grpc.Core;
using KubeMQ.Grpc;

namespace KubeMQ.Client
{
    public class BaseClient
    {
        private ClientOptions _clientOptions;
        private kubemq.kubemqClient _client;
       
        
        protected ClientOptions ClientOptions => _clientOptions;
        protected kubemq.kubemqClient Client => _client;
        
        
        public BaseClient(ClientOptions clientOptions)
        {
            _clientOptions = clientOptions;
            Channel channel;
            var sslCreds = _clientOptions.GetSslCredentials();
            if (sslCreds != null)
            {
                channel = new Channel(_clientOptions.Address, sslCreds);
            }
            else
            {
                channel = new Channel(_clientOptions.Address, ChannelCredentials.Insecure);
            }

            _client = new kubemq.kubemqClient(channel);
        }
        
        public PingResult Ping () {
            PingResult rec = _client.Ping (new Empty ());
            return rec;
        }
        
    }
}