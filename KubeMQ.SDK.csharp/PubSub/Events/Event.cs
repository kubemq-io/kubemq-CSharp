using System;
using System.Collections.Generic;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;

namespace KubeMQ.SDK.csharp.PubSub.Events
{
    /// <summary>
    /// Represents an event message to be sent or received.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Gets or sets the identifier of the event.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Represents an event message that can be published to a channel.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Represents the metadata associated with an event message.
        /// </summary>
        public string Metadata { get; private set; }

        /// <summary>
        /// Represents the body of event message.
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Represents an event message in the KubeMQ SDK.
        /// </summary>
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Sets the ID of the event.
        /// </summary>
        /// <param name="id">The ID of the event.</param>
        /// <returns>The current event instance.</returns>
        public Event SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the channel for the event.
        /// </summary>
        /// <param name="channel">The channel to set for the event.</param>
        /// <returns>The modified Event object.</returns>
        public Event SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata for the event.
        /// </summary>
        /// <param name="metadata">The metadata to set.</param>
        /// <returns>The updated Event object.</returns>
        public Event SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the event.
        /// </summary>
        /// <param name="body">The body of the event.</param>
        /// <returns>The updated event with the specified body.</returns>
        public Event SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// <summary>
        /// Sets the tags for the event.
        /// </summary>
        /// <param name="tags">The dictionary of tags to set.</param>
        /// <returns>The current instance of the Event class.</returns>
        public Event SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Validates the Event instance.
        /// </summary>
        /// <returns>The validated Event instance.</returns>
        internal Event Validate()
        {
            if (string.IsNullOrEmpty(Channel))
            {
                throw new InvalidOperationException("Event message must have a channel.");
            }

            if (string.IsNullOrEmpty(Metadata) && (Body == null || Body.Length == 0) && Tags.Count == 0)
            {
                throw new InvalidOperationException("Event message must have at least one of the following: metadata, body, or tags.");
            }

            return this;
        }

        /// <summary>
        /// Converts the Event object to a Kubemq Event object.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The Kubemq Event object.</returns>
        internal pb.Event ToKubemqEvent(string clientId)
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