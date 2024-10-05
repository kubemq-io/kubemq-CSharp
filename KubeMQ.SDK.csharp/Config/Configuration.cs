using System;

namespace KubeMQ.SDK.csharp.Config
{
    public class Configuration
    {
        private const int DefaultMaxSendSize = 1024 * 1024 * 100; // 100MB
        private const int DefaultMaxRcvSize = 1024 * 1024 * 100; // 100MB
        private const int DefaultReconnectIntervalSeconds = 5;

        public string Address { get; private set; } = "";
        public string ClientId { get; private set; } = "";
        public string AuthToken { get; private set; } = "";
        public int MaxSendSize { get; private set; } = DefaultMaxSendSize;
        public int MaxReceiveSize { get; private set; } = DefaultMaxRcvSize;
        public bool DisableAutoReconnect { get; private set; }
        public int ReconnectIntervalSeconds { get; private set; } = DefaultReconnectIntervalSeconds;
        public TlsConfig Tls { get; private set; } = new TlsConfig();
        
    
        public Configuration SetAddress(string address)
        {
            Address = address;
            return this;
        }

        public Configuration SetClientId(string clientId)
        {
            ClientId = clientId;
            return this;
        }

        public Configuration SetAuthToken(string authToken)
        {
            AuthToken = authToken;
            return this;
        }

        public Configuration SetTls(TlsConfig tls)
        {
            Tls = tls;
            return this;
        }

        public Configuration SetMaxSendSize(int maxSendSize)
        {
            MaxSendSize = maxSendSize;
            return this;
        }

        public Configuration SetMaxReceiveSize(int maxReceiveSize)
        {
            MaxReceiveSize = maxReceiveSize;
            return this;
        }

        public Configuration SetDisableAutoReconnect(bool disableAutoReconnect)
        {
            DisableAutoReconnect = disableAutoReconnect;
            return this;
        }

        public Configuration SetReconnectIntervalSeconds(int reconnectIntervalSeconds)
        {
            ReconnectIntervalSeconds = reconnectIntervalSeconds;
            return this;
        }

        

        public int GetReconnectIntervalDuration()
        {
            if (ReconnectIntervalSeconds == 0)
            {
                return DefaultReconnectIntervalSeconds * 1000;
            }
            return ReconnectIntervalSeconds*1000;
        }

       
        public void Validate()
        {
            if (string.IsNullOrEmpty(Address))
            {
                throw new ArgumentException("Connection must have an address");
            }
            if (string.IsNullOrEmpty(ClientId))
            {
                throw new ArgumentException("Connection must have a clientId");
            }
            if (MaxSendSize <= 0)
            {
                throw new ArgumentException("Connection max send size must be greater than 0");
            }
            if (MaxReceiveSize <= 0)
            {
                throw new ArgumentException("Connection max receive size must be greater than 0");
            }
            if (ReconnectIntervalSeconds <= 0)
            {
                throw new ArgumentException("Connection reconnect interval must be greater than 0");
            }
            if (Tls != null)
            {
                Tls.Validate(); 
            }
        }
    }
}
