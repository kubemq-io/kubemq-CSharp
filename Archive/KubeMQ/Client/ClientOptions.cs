using System;
using System.Threading;
using Grpc.Core;

namespace KubeMQ.Client
{
    public class ClientOptions
    {
        
        private string _address = "localhost:5000";
        private string _clientId = Guid.NewGuid().ToString();
        private double _callTimeout = 0;
        private string _authToken = "";
        private string _sslRootCertificate = "";
        private string _sslKey = "";
        private string _sslCert = "";
        private int _reconnectInterval = 1000;

        public string Address => _address;
        public string ClientId => _clientId;
        public int ReconnectInterval => _reconnectInterval;
        
        public ClientOptions()
        {
        }
        public ClientOptions(string address)
        {
            _address = address;
        }

        public ClientOptions WithAddress(string address)
        {
            var newOptions = this;
            newOptions._address = address;
            return newOptions;
        }
        
        public ClientOptions WithClientId(string clientId)
        {
            var newOptions = this;
            newOptions._clientId = clientId;
            return newOptions;
        }
        
        public ClientOptions WithCallTimeout(double callTimeout)
        {
            var newOptions = this;
            newOptions._callTimeout = callTimeout;
            return newOptions;
        }
        public ClientOptions WithAuthToken(string authToken)
        {
            var newOptions = this;
            newOptions._authToken = authToken;
            return newOptions;
        }
        public ClientOptions WithSslRootCertificate(string rootCertificate)
        {
            var newOptions = this;
            newOptions._sslRootCertificate = rootCertificate;
            return newOptions;
        }
        public ClientOptions WithSslPrivateKey(string sslPrivateKey)
        {
            var newOptions = this;
            newOptions._sslKey = sslPrivateKey;
            return newOptions;
        }
        
        public ClientOptions WithSslCertificateChain(string sslCertificateChain)
        {
            var newOptions = this;
            newOptions._sslCert = sslCertificateChain;
            return newOptions;
        }
        
        public ClientOptions WithReconnectInterval(int reconnectInterval)
        {
            var newOptions = this;
            newOptions._reconnectInterval = reconnectInterval;
            return newOptions;
        }
        
        public SslCredentials GetSslCredentials()
        {
            if (string.IsNullOrEmpty(_sslRootCertificate))
            {
                return null;
            }
            if (!string.IsNullOrEmpty(_sslCert) && !string.IsNullOrEmpty(_sslKey))
            {
                return new SslCredentials(_sslRootCertificate, new KeyCertificatePair(_sslCert, _sslKey));
            }
            else
            {
                return new SslCredentials(_sslRootCertificate);
            }            
        }

        public Metadata GetGrpcMetadata()
        {
            Metadata metadata = new Metadata();
            if (!string.IsNullOrEmpty(_authToken))
            {
                metadata.Add(new Metadata.Entry ("authorization", _authToken));
            }

            return metadata;
        }
        public CallOptions GetGrpcCallOptions(CancellationToken? cancellationToken)
        {
            CallOptions callOptions = new CallOptions();
            if (_callTimeout > 0)
            {
                callOptions = callOptions.WithDeadline(DateTime.Now.AddMilliseconds(_callTimeout));
            }

            return callOptions;
        }

        public string PopulateClientId(string clientId)
        {
            return string.IsNullOrEmpty(clientId) ? _clientId : clientId;
        }
    }
}