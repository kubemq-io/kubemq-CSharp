using KubeMQ.SDK.csharp.Tools;
using System.Collections.Generic;
using InnerRecivedEvent = KubeMQ.Grpc.EventReceive;

namespace KubeMQ.SDK.csharp.Events
{
    public class EventReceive
    {
        public string EventID { get; set; }
        public string Channel { get; set; }
        public string Metadata { get; set; }
        public byte[] Body { get; set; }
        public long Timestamp { get; set; }
        public ulong Sequence { get; set; }

        /// <summary>
        /// Represents a set of Key value pair that help categorize the message. 
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }
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
