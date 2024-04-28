using System;
using System.Collections.Generic;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;

namespace KubeMQ.SDK.csharp.Unified.PubSub.Events
{
public class Event
    {
        public string Id { get; private set; }
        public string Channel { get; private set; }
        public string Metadata { get; private set; }
        public byte[] Body { get; private set; }
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        public Event SetId(string id)
        {
            Id = id;
            return this;
        }

        public Event SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        public Event SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        public Event SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        public Event SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Channel))
            {
                throw new InvalidOperationException("Event message must have a channel.");
            }

            if (string.IsNullOrEmpty(Metadata) && (Body == null || Body.Length == 0) && Tags.Count == 0)
            {
                throw new InvalidOperationException("Event message must have at least one of the following: metadata, body, or tags.");
            }
        }

        public pb.Event ToKubemqEvent(string clientId)
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString();
            }

            Tags.Add("x-kubemq-client-id", clientId);
            pb.Event pbEvent = new pb.Event();
            pbEvent.EventID = Id;
            pbEvent.ClientID = clientId;
            pbEvent.Channel = Channel;
            pbEvent.Metadata = Metadata ?? "";
            pbEvent.Body = ByteString.CopyFrom(Body);
            pbEvent.Store = false;
            foreach (var entry in Tags)
            {
                pbEvent.Tags.Add(entry.Key, entry.Value);
            }

            return pbEvent;
        }
    }
}