using System;

namespace KubeMQ.SDK.csharp.Common
{
    public class QueuesStats
    {
        public int Messages { get; set; }
        public int Volume { get; set; }
        public int Waiting { get; set; }
        public int Expired { get; set; }
        public int Delayed { get; set; }

        public override string ToString()
        {
            return $"Stats: messages={Messages}, volume={Volume}, waiting={Waiting}, expired={Expired}, delayed={Delayed}";
        }
    }

    public class QueuesChannel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Int64 LastActivity { get; set; }
        public bool IsActive { get; set; }
        public QueuesStats Incoming { get; set; }
        public QueuesStats Outgoing { get; set; }

        public override string ToString()
        {
            return $"Channel: name={Name}, type={Type}, last_activity={LastActivity}, is_active={IsActive}, incoming={Incoming}, outgoing={Outgoing}";
        }
    }

    public class PubSubStats
    {
        public int Messages { get; set; }
        public int Volume { get; set; }

        public override string ToString()
        {
            return $"Stats: messages={Messages}, volume={Volume}";
        }
    }

    public class PubSubChannel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Int64 LastActivity { get; set; }
        public bool IsActive { get; set; }
        public PubSubStats Incoming { get; set; }
        public PubSubStats Outgoing { get; set; }

        public override string ToString()
        {
            return $"Channel: name={Name}, type={Type}, last_activity={LastActivity}, is_active={IsActive}, incoming={Incoming}, outgoing={Outgoing}";
        }
    }

    public class CQStats
    {
        public int Messages { get; set; }
        public int Volume { get; set; }
        public int Responses { get; set; }

        public override string ToString()
        {
            return $"Stats: messages={Messages}, volume={Volume}, responses={Responses}";
        }
    }

    public class CQChannel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Int64 LastActivity { get; set; }
        public bool IsActive { get; set; }
        public CQStats Incoming { get; set; }
        public CQStats Outgoing { get; set; }

        public override string ToString()
        {
            return $"Channel: name={Name}, type={Type}, last_activity={LastActivity}, is_active={IsActive}, incoming={Incoming}, outgoing={Outgoing}";
        }
    }
}