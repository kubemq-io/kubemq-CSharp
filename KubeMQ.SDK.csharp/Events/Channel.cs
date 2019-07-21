using System;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Events
{
    /// <summary>
    /// Represents a Sender with a set of predefined parameters.
    /// </summary>
    public class Channel
    {
        private LowLevel.Sender _sender;

        private string ChannelName { get; set; }
        private string ClientID { get; set; }
        private bool Store { get; set; }
        private bool ReturnResult { get; set; }

        /// <summary>
        ///  Initializes a new instance of the KubeMQ.SDK.csharp.Events.Channel class using "Manual" Parameters. 
        /// </summary>
        /// <param name="channelName">Represents The channel name to send to using the KubeMQ .</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under.</param>
        /// <param name="store">If true will save data to kubemq storage.</param>
        /// <param name="KubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000". </param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        public Channel(string channelName, string clientID, bool store, string KubeMQAddress, ILogger logger=null)
        {
            ChannelName = channelName;
            ClientID = clientID;
            Store = store;

            if (!IsValide(out Exception ex))
            {
                throw ex;
            }

            _sender = new LowLevel.Sender(KubeMQAddress, logger);
        }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.Events.Channel class using "ChannelParameters" class.
        /// </summary>
        /// <param name="parameters"></param>
        public Channel(ChannelParameters parameters) : this(parameters.ChannelName, parameters.ClientID,
            parameters.Store, parameters.KubeMQAddress, parameters.Logger)
        { }

        /// <summary>
        /// Sending a new Event using the KubeMQ.
        /// </summary>
        /// <param name="notification">KubeMQ.SDK.csharp.Events.Event which represent the data to send using the KubeMQ.</param>
        /// <returns>KubeMQ.SDK.csharp.Events.Result which show the status of the Event sent.</returns>
        public Result SendEvent(Event notification)
        {
            return _sender.SendEvent(CreateLowLevelEvent(notification));
        }

        /// <summary>
        /// bi-di streams 'SendEventStream (stream Event) returns (stream Result) ,closed by ClosesEventStreamAsync()
        /// </summary>
        /// <param name="notification">KubeMQ.SDK.csharp.Events.Event which represent the data to send using the KubeMQ.</param>
        /// <param name="resultDelegate">Result stream handler delegate, use null when using Unidirectional no Result</param>
        /// <returns></returns>
        public async Task StreamEvent(Event notification, ReceiveResultDelegate resultDelegate = null)
        {
            await _sender.StreamEvent(CreateLowLevelEvent(notification, (resultDelegate != null)), resultDelegate);
        }

        /// <summary>
        /// close bi-di streams 'SendEventStream (stream Event)
        /// </summary>
        /// <returns></returns>
        public async Task ClosesEventStreamAsync()
        {
            await _sender.ClosesEventStreamAsync();
        }

        /// <summary>
        /// Ping check Kubemq response using channel.
        /// </summary>
        /// <returns>ping status of kubemq.</returns>
        public PingResult Ping()
        {
            return _sender.Ping();

        }

        private bool IsValide(out Exception ex)
        {
            if (string.IsNullOrWhiteSpace(ChannelName))
            {
                ex = new ArgumentException("Parameter is mandatory", "ChannelName");
                return false;
            }

            ex = null;
            return true;
        }

        private LowLevel.Event CreateLowLevelEvent(Event notification)
        {
            return new LowLevel.Event()
            {
                Channel = ChannelName,
                ClientID = ClientID,
                Store = Store,

                EventID = notification.EventID,
                Body = notification.Body,
                Metadata = notification.Metadata,
                Tags = notification.Tags
            };
        }

        private LowLevel.Event CreateLowLevelEvent(Event notification, bool returnResult)
        {
            LowLevel.Event @event = CreateLowLevelEvent(notification);
            @event.ReturnResult = returnResult;
            return @event;
        }
    }
}
