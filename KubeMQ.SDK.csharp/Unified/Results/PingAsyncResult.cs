using KubeMQ.SDK.csharp.Unified.Grpc;

namespace KubeMQ.SDK.csharp.Unified.Results
{
    /// <summary>
    /// Represents the result of a ping operation.
    /// </summary>
    public class PingAsyncResult : BaseResult
    {
        /// <summary>
        /// Represents information about the KubeMQ server.
        /// </summary>
        public ServerInfo ServerInfo { get; set; }
    }
}