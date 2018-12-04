using System;
using System.Threading.Tasks;
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
        /// <param name="store"></param>
        /// <param name="KubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000". </param>
        /// <param name="logger">Microsoft.Extensions.Logging.ILogger.</param>
        public Channel(string channelName, string clientID, bool store, string KubeMQAddress, ILogger logger)
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
            parameters.Store, parameters.KubeMQAddress, parameters.Logger) { }

        /// <summary>
        /// Sending a new Event using the KubeMQ.
        /// </summary>
        /// <param name="notification">KubeMQ.SDK.csharp.Events.Event which represent the data to send using the KubeMQ.</param>
        /// <returns>KubeMQ.SDK.csharp.Events.Result which show the status of the Event sent.</returns>
        public Result SendEvent(Event notification)
        {
            return _sender.SendEvent(CreateLowLevelEvent(notification));
        }

        public Result SendEvent(Event notification, bool returnResult)
        {
            return _sender.SendEvent(CreateLowLevelEvent(notification, returnResult));
        }

        public async Task StreamEvent(Event notification, ReceiveResultDelegate resultDelegate)
        {
            await _sender.StreamEvent(CreateLowLevelEvent(notification), resultDelegate);
        }

        public async Task StreamEvent(Event notification, bool returnResult, ReceiveResultDelegate resultDelegate)
        {
            await _sender.StreamEvent(CreateLowLevelEvent(notification, returnResult), resultDelegate);
        }

        public async Task ClosesEventStreamAsync()
        {
            await _sender.ClosesEventStreamAsync();
        }

        private bool IsValide(out Exception ex)
        {
            if (string.IsNullOrWhiteSpace(ChannelName))
            {
                ex = new ArgumentException("Parameter is mandatory", "ChannelName");
                return false;
            }
            //if (Store && string.IsNullOrWhiteSpace(ClientID))
            //{
            //    ex = new ArgumentException("Parameter is mandatory", "ClientID");
            //    return false;
            //}
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
                Metadata = notification.Metadata
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
