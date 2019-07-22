using KubeMQ.SDK.csharp.Tools;
using System.Collections.Generic;
using InnerRecivedEvent = KubeMQ.Grpc.EventReceive;

namespace KubeMQ.SDK.csharp.Events
{
    public class EventReceive
    {
        /// <summary>
        /// Represent the ID of the event.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Represent the channel name the event was sent to.
        /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// Represent General information about the message.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represent the main data of the message.
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        /// Represent the time the event was sent to Kubemq
        /// </summary>
        public long Timestamp { get; set; }
        /// <summary>
        /// ulong that Represent the order the event was sent to the kubemq.
        /// </summary>
        public ulong Sequence { get; set; }

        /// <summary>
        /// Represents a set of Key value pair of string string that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.RequestReply.RequestReceive .
        /// </summary>
        public EventReceive() { }
        public EventReceive(InnerRecivedEvent inner)
        {
            EventID = inner.EventID;
            Channel = inner.Channel;
            Metadata = inner.Metadata;
            Body = inner.Body.ToByteArray();
            Timestamp = inner.Timestamp;
            Sequence = inner.Sequence;
            Tags = Converter.ReadTags(inner.Tags);
        }

    }
}
