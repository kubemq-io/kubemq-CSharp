using System;
using System.Collections.Generic;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.Unified.PubSub.Events
{
public class EventReceived
    {
        public string Id { get; private set; }
        public string FromClientId { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Channel { get; private set; }
        public string Metadata { get; private set; }
        public byte[] Body { get; private set; }
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        public EventReceived SetId(string id)
        {
            Id = id;
            return this;
        }

        public EventReceived SetFromClientId(string fromClientId)
        {
            FromClientId = fromClientId;
            return this;
        }

        public EventReceived SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
            return this;
        }

        public EventReceived SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        public EventReceived SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        public EventReceived SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        public EventReceived SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        public static EventReceived FromEvent(pb.EventReceive eventReceive)
        {
            string fromClientId = "";
            if (eventReceive.Tags != null && eventReceive.Tags.TryGetValue("x-kubemq-client-id", out var tag))
            {
                fromClientId = tag;
            }
            Dictionary<string,string> tags = new Dictionary<string, string>();
            if (eventReceive.Tags != null)
            {
                foreach (var item in eventReceive.Tags)
                {
                    tags.Add(item.Key,item.Value);
                }
            }
            return new EventReceived()
                .SetId(eventReceive.EventID)
                .SetTimestamp(DateTime.Now) 
                .SetFromClientId(fromClientId)
                .SetChannel(eventReceive.Channel)
                .SetMetadata(eventReceive.Metadata)
                .SetBody(eventReceive.Body.ToByteArray()) 
                .SetTags(tags);
        }
    }
}