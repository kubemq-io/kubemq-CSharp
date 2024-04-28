using System;
using System.Collections.Generic;
using System.Threading;
using KubeMQ.SDK.csharp.Tools;
using InnerEvent = KubeMQ.Grpc.Event;


namespace KubeMQ.SDK.csharp.Events.LowLevel
{
    [Obsolete("This class is obsolete. Use KubeMQ.SDK.csharp.PubSub.Event instead.")]
    public class Event
    {
        #region Properties
        private static int _id = 0;
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ .
        /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// Represents text as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.PubSub.LowLevel.Event.
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        /// Represents a Event identifier.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Represents the sender ID that the events will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents if the events should be send to persistence.
        /// </summary>
        public bool Store { get; set; }
        /// <summary>
        /// Represents a set of Key value pair that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }

        public bool ReturnResult { get; set; }

        #endregion
        /// <summary>
        /// Create a new instance of KubeMQ.SDK.csharp.Events.LowLevel.Event to be sent through the Kubemq.
        /// </summary>
        public Event() { }

        /// <summary>
        /// Create a new instance of KubeMQ.SDK.csharp.Events.LowLevel.Event to be sent through the Kubemq.
        /// </summary>
        /// <param name="channel">String: the name of the channel to send the Event to.</param>
        /// <param name="metadata">String: General information about the message.</param>
        /// <param name="body">Byte[] the main data of the message.</param>
        /// <param name="eventID">string: EventID to help distinguish the message.</param>
        /// <param name="clientID">String: Represent the sender.</param>
        /// <param name="store">Bool:If true the event will be sent to the kubemq storage.</param>
        /// <param name="tags">Dictionary of string , string pair:A set of Key value pair that help categorize the message.</param>
        public Event(string channel, string metadata, byte[] body, string eventID, string clientID, bool store,Dictionary<string,string>tags)
        {
            Channel = channel;
            Metadata = metadata;
            Body = body;
            EventID = eventID;
            ClientID = clientID;
            Store = store;
            Tags = tags;
        }

        internal InnerEvent ToInnerEvent()
        {
            return new InnerEvent()
            {
                Channel = this.Channel,
                Metadata = this.Metadata ?? string.Empty,
                Body = Converter.ToByteString(this.Body),
                EventID = string.IsNullOrEmpty(this.EventID) ? Guid.NewGuid().ToString() : EventID,
                ClientID = string.IsNullOrEmpty(this.ClientID) ? Guid.NewGuid().ToString() : ClientID,
                Store = this.Store,
                Tags = { Converter.CreateTags(this.Tags) }
            };
        }

    }
}
