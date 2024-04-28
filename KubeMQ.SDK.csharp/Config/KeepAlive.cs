using System;

namespace KubeMQ.SDK.csharp.Config
{
    public class KeepAliveConfig
    {
        public bool Enabled { get; private set; }
        public int PingIntervalInSeconds { get; private set; } = 0;
        public int PingTimeOutInSeconds { get; private set; } = 0;

        public KeepAliveConfig SetEnable(bool enable)
        {
            Enabled = enable;
            return this;
        }

        public KeepAliveConfig SetPingIntervalInSeconds(int pingIntervalInSeconds)
        {
            PingIntervalInSeconds = pingIntervalInSeconds;
            return this;
        }

        public KeepAliveConfig SetPingTimeOutInSeconds(int pingTimeOutInSeconds)
        {
            PingTimeOutInSeconds = pingTimeOutInSeconds;
            return this;
        }

        public void Validate()
        {
            if (!Enabled)
            {
                return;
            }
            if (PingIntervalInSeconds <= 0)
            {
                throw new ArgumentException("Keep alive ping interval must be greater than 0");
            }
            if (PingTimeOutInSeconds <= 0)
            {
                throw new ArgumentException("Keep alive ping timeout must be greater than 0");
            }
        }
    }
}