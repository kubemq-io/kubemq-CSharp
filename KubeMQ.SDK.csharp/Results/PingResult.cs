using System;
using KubeMQ.SDK.csharp.Transport;

namespace KubeMQ.SDK.csharp.Results
{
    /// <summary>
    /// Represents the result of a ping operation.
    /// </summary>
    public class PingResult : Result
    {
        /// <summary>
        /// Represents information about the KubeMQ server.
        /// </summary>
        public ServerInfo ServerInfo { get; set; }
        
        public PingResult(ServerInfo info):base()
        {
            ServerInfo = info;
            IsSuccess = true;
        }
        
        public PingResult(string errorMessage) : base(errorMessage)
        {
        }
        
        public PingResult(Exception e) : base(e)
        {
        }
    }
}