
using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.Events
{
    public class Event
    {
        #region Properties
        /// <summary>
        /// Represents a Event identifier.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Represents the channel name to send to using the KubeMQ .
        /// </summary> 
        public string Channel { get; set; }
        
        /// <summary>
        /// Represents if the events should be send to persistence.
        /// </summary>
        public bool Store { get; set; }
        public string Metadata { get; set; }
        /// <summary>
        /// Represents the content of the KubeMQ.SDK.csharp.Events.Event.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Represents a set of Key value pair that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }
        #endregion
        /// <summary>
        /// Represents a message to be send to kubemq.
        /// </summary>
        public Event() { }

        /// <summary>
        /// Represents a message to be send to kubemq.
        /// </summary>
        /// <param name="eventID">Event Identifier</param>
        /// <param name="metadata">General information about the message body</param>
        /// <param name="body">The information that you want to pass.</param>
        /// <param name="tags">a set of Key value pair that help categorize the message</param>
        /// <param name="channel">The channel name to send to using the KubeMQ</param>
        /// <param name="store">If true the event will be sent to the kubemq storage</param>
        public Event(string eventID, string metadata, byte[] body,Dictionary <string,string>tags, string channel="", string clientID="",bool store=false)
        {
            EventID = eventID;
            Metadata = metadata;
            Body = body;
            Tags = tags;
            Channel = channel;
            Store = store;
        }
    }
}
