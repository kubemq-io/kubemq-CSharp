using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Events
{
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
        public bool Store { get; set; }
        public bool ReturnResult { get; set; }
        /// <summary>
        /// KubeMQ server address.
        /// </summary>
        public string KubeMQAddress { get; set; }
        /// <summary>
        /// Microsoft.Extensions.Logging Ilogger.
        /// </summary>
        public ILogger Logger { get; set; }
        #endregion

        public ChannelParameters() { }

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
