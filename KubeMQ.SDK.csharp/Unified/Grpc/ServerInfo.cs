namespace KubeMQ.SDK.csharp.Unified.Grpc
{
    public class ServerInfo
    {
        public string Host { get; set; }
        public string Version { get; set; }
        public long ServerStartTime { get; set; }
        public long ServerUpTimeSeconds { get; set; }
    }
}