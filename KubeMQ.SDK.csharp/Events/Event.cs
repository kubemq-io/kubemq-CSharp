
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
        /// Represents text as System.String.
        /// </summary>
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

        public Event() { }

        /// <summary>
        /// Represents a message to be send to kubemq.
        /// </summary>
        /// <param name="eventID">Event Identifier</param>
        /// <param name="metadata">General information about the message body</param>
        /// <param name="body">The information that you want to pass.</param>
        /// <param name="tags">a set of Key value pair that help categorize the message</param>
        public Event(string eventID, string metadata, byte[] body,Dictionary <string,string>tags)
        {
            EventID = eventID;
            Metadata = metadata;
            Body = body;
            Tags = tags;
        }
    }
}
