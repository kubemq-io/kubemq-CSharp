using System;

namespace KubeMQ.SDK.csharp.Config
{
    public class Connection
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
        public int ReconnectIntervalSeconds { get; private set; }
        public TlsConfig Tls { get; private set; } = new TlsConfig();
        public KeepAliveConfig KeepAlive { get; private set; } = new KeepAliveConfig();

        public Connection SetAddress(string address)
        {
            Address = address;
            return this;
        }

        public Connection SetClientId(string clientId)
        {
            ClientId = clientId;
            return this;
        }

        public Connection SetAuthToken(string authToken)
        {
            AuthToken = authToken;
            return this;
        }

        public Connection SetTls(TlsConfig tls)
        {
            Tls = tls;
            return this;
        }

        public Connection SetMaxSendSize(int maxSendSize)
        {
            MaxSendSize = maxSendSize;
            return this;
        }

        public Connection SetMaxReceiveSize(int maxReceiveSize)
        {
            MaxReceiveSize = maxReceiveSize;
            return this;
        }

        public Connection SetDisableAutoReconnect(bool disableAutoReconnect)
        {
            DisableAutoReconnect = disableAutoReconnect;
            return this;
        }

        public Connection SetReconnectIntervalSeconds(int reconnectIntervalSeconds)
        {
            ReconnectIntervalSeconds = reconnectIntervalSeconds;
            return this;
        }

        public Connection SetKeepAlive(KeepAliveConfig keepAlive)
        {
            KeepAlive = keepAlive;
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

        public Connection Complete()
        {
            if (MaxSendSize == 0)
            {
                MaxSendSize = DefaultMaxSendSize;
            }
            if (MaxReceiveSize == 0)
            {
                MaxReceiveSize = DefaultMaxRcvSize;
            }
            if (ReconnectIntervalSeconds == 0)
            {
                ReconnectIntervalSeconds = DefaultReconnectIntervalSeconds;
            }
            return this;
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
            if (MaxSendSize < 0)
            {
                throw new ArgumentException("Connection max send size must be greater than 0");
            }
            if (MaxReceiveSize < 0)
            {
                throw new ArgumentException("Connection max receive size must be greater than 0");
            }
            if (ReconnectIntervalSeconds < 0)
            {
                throw new ArgumentException("Connection reconnect interval must be greater than 0");
            }
            if (Tls != null)
            {
                Tls.Validate(); 
            }
            if (KeepAlive != null)
            {
                KeepAlive.Validate();
            }
        }
    }
}
