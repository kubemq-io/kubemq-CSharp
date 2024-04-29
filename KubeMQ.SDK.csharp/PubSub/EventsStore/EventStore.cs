using System;
using System.Collections.Generic;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;

namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Represents an event message in the event store.
    /// </summary>
    public class EventStore
    {
        /// <summary>
        /// Gets or sets the unique identifier of the EventStore.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets or sets the channel to publish or subscribe event messages.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Represents the metadata of an event in the event store.
        /// </summary>
        public string Metadata { get; private set; }

        /// <summary>
        /// Represents the body of an event message in the EventStore.
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Represents an event store for storing event messages.
        /// </summary>
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Sets the ID of the event message in the EventStore.
        /// </summary>
        /// <param name="id">The ID of the event message.</param>
        /// <returns>The EventStore instance.</returns>
        public EventStore SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the channel for the event in the EventStore.
        /// </summary>
        /// <param name="channel">The channel to set for the event.</param>
        /// <returns>The updated EventStore object.</returns>
        public EventStore SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata of the event store.
        /// </summary>
        /// <param name="metadata">The metadata to set.</param>
        /// <returns>The updated EventStore instance.</returns>
        public EventStore SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the EventStore message.
        /// </summary>
        /// <param name="body">The byte array representing the message body.</param>
        /// <returns>A reference to the EventStore object.</returns>
        public EventStore SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// <summary>
        /// Sets the tags for the event.
        /// </summary>
        /// <param name="tags">The tags to set for the event.</param>
        /// <returns>The updated <see cref="EventStore"/> instance.</returns>
        public EventStore SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Validates the EventStore object.
        /// </summary>
        /// <remarks>
        /// This method validates the EventStore object to ensure that it meets the required conditions for a valid event message.
        /// It checks if the channel is not null or empty, and if at least one of the following is provided: metadata, body, or tags.
        /// If any of these conditions is not met, an InvalidOperationException is thrown.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the channel is null or empty, and none of the following are provided: metadata, body, or tags.</exception>
        internal EventStore Validate()
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
        /// Converts an instance of EventStore to an instance of Kubemq.Grpc.Event.
        /// </summary>
        /// <param name="clientId">The client ID to be set in the converted Kubemq.Grpc.Event instance.</param>
        /// <returns>The converted Kubemq.Grpc.Event instance.</returns>
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
            pbEvent.Store = true;
            foreach (var entry in Tags)
            {
                pbEvent.Tags.Add(entry.Key, entry.Value);
            }

            return pbEvent;
        }
    }
}