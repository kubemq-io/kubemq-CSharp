using System;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;
using Microsoft.Extensions.Logging;
using static KubeMQ.SDK.csharp.Common.Common;
namespace KubeMQ.SDK.csharp.Events
{
    /// <summary>
    /// Represents a Sender with a set of predefined parameters.
    /// </summary>
    public class Sender
    {
        private readonly LowLevel.Sender  _sender;
        private string _clientId { get; set; }
       // private bool ReturnResult { get; set; }

        /// <summary>
        ///  Initializes a new instance of the KubeMQ.SDK.csharp.Events.Channel class using "Manual" Parameters. 
        /// </summary>
        /// <param name="KubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000".</param>
        /// <param name="channel">Represents The channel name to send to using the KubeMQ.</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Sender(string KubeMQAddress, string clientId="", ILogger logger=null, string authToken = null)
        {
            _clientId = clientId;
            _sender = new LowLevel.Sender(KubeMQAddress, logger, authToken);
        }

        /// <summary>
        /// Sending a new Event using the KubeMQ.
        /// </summary>
        /// <param name="notification">KubeMQ.SDK.csharp.Events.Event which represent the data to send using the KubeMQ.</param>
        /// <returns>KubeMQ.SDK.csharp.Events.Result which show the status of the Event sent.</returns>
        public Result SendEvent(Event notification)
        {
            if ((notification.Body == null || notification.Body.Length == 0) && (string.IsNullOrEmpty(notification.Metadata)))
            {
                throw new ArgumentException("either body or metadata must be set");
            }
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

        /// <summary>
        /// Creates a new events channel with the given name.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the result of the operation.</returns>
        public async Task<CommonAsyncResult> CreateEventsChannel(string channelName)
        {
            return await CreateDeleteChannel(_sender.Client(),_clientId, channelName, "events", true);
        }

        /// <summary>
        /// Creates a channel for storing events.
        /// </summary>
        /// <param name="channelName">The name of the channel to be created.</param>
        /// <returns>A task that represents the asynchronous operation. The task result represents the result of the operation.</returns>
        public async Task<CommonAsyncResult> CreateEventsStoreChannel(string channelName)
        {
            return await CreateDeleteChannel(_sender.Client(),_clientId, channelName, "events_store", true);
        }

        /// <summary>
        /// Deletes an events channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A <see cref="CommonAsyncResult"/> object that represents the result of the delete operation.</returns>
        public async Task<CommonAsyncResult> DeleteEventsChannel(string channelName)
        {
            return await CreateDeleteChannel(_sender.Client(), _clientId, channelName, "events", false);
        }

        /// <summary>
        /// Deletes an events store channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<CommonAsyncResult> DeleteEventsStoreChannel(string channelName)
        {
            return await CreateDeleteChannel(_sender.Client(), _clientId, channelName, "events_store", false);
        }


        /// <summary>
        /// Lists the events channels.
        /// </summary>
        /// <param name="search">The search string to filter channel names.</param>
        /// <returns>A list of pub/sub channels.</returns>
        public async Task<ListPubSubAsyncResult> ListEventsChannels (string search = "") {
            
            return await ListPubSubChannels(_sender.Client(), _clientId, search, "events");
        }


        /// <summary>
        /// Lists all events store channels.
        /// </summary>
        /// <param name="search">Optional parameter for filtering the results by channel name</param>
        /// <returns>Returns a ListPubSubAsyncResult object containing the channels that match the search criteria</returns>
        public async Task<ListPubSubAsyncResult> ListEventsStoreChannels (string search = "") {
            return await ListPubSubChannels(_sender.Client(), _clientId, search, "events_store");
        }
        
        private LowLevel.Event CreateLowLevelEvent(Event notification)
        {
            return new LowLevel.Event()
            {
                Channel = notification.Channel,
                ClientID = _clientId,
                Store = notification.Store,
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
