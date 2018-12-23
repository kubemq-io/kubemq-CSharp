using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Events
{
    /// <summary>
    /// Configuration parameters for channel.
    /// </summary>
    public class ChannelParameters
    {
        #region Properties
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ .
        /// </summary>
        public string ChannelName { get; set; }
        /// <summary>
        /// Represents the sender ID that the messages will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents the channel persistence property.
        /// </summary>
        public bool Store { get; set; }  
        /// <summary>
        /// KubeMQ server address.
        /// </summary>
        public string KubeMQAddress { get; set; }
        /// <summary>
        /// Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK when null.
        /// </summary>
        public ILogger Logger { get; set; }
        #endregion

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.Events.ChannelParameters class with set parameters.
        /// </summary>
        public ChannelParameters() { }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.Events.ChannelParameters class with set parameters.
        /// </summary>
        /// <param name="channelName">Represents The channel name to send to using the KubeMQ.</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under.</param>
        /// <param name="store">Represents the channel persistence property.</param>
        /// <param name="kubeMQAddress">Represents The address of the KubeMQ server.</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK when null.</param>

        public ChannelParameters(string channelName, string clientID, bool store, string kubeMQAddress, ILogger logger)
        {
            ChannelName = channelName;
            ClientID = clientID;
            Store = store;
            KubeMQAddress = kubeMQAddress;
            Logger = logger;
        }
    }
}
