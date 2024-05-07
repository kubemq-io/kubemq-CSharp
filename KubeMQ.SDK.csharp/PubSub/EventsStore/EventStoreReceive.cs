using System;
using System.Collections.Generic;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Represents an event received by the event store.
    /// </summary>
    public class EventStoreReceived
    {
        /// <summary>
        /// Gets or sets the ID of the event received from the KubeMQ server.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets or sets the client ID from which the event was received.
        /// </summary>
        public string FromClientId { get; private set; }

        /// <summary>
        /// Represents an event received in the event store.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Class representing a received event from the KubeMQ channel.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Represents the metadata associated with an event received from KubeMQ.
        /// </summary>
        public string Metadata { get; private set; }

        /// <summary>
        /// Represents an event received from the KubeMQ event store.
        /// </summary>
        public ulong Sequence { get; private set; }

        /// <summary>
        /// Represents the body of an event received from the KubeMQ server.
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Represents an event received from the KubeMQ server.
        /// </summary>
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Sets the ID of the EventStoreReceived object.
        /// </summary>
        /// <param name="id">The ID to set.</param>
        /// <returns>The updated EventStoreReceived object.</returns>
        private EventStoreReceived SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the FromClientId property of the EventStoreReceived object.
        /// </summary>
        /// <param name="fromClientId">The value to set the FromClientId property to.</param>
        /// <returns>The modified EventStoreReceived object.</returns>
        private EventStoreReceived SetFromClientId(string fromClientId)
        {
            FromClientId = fromClientId;
            return this;
        }

        /// <summary>
        /// Sets the timestamp of the EventStoreReceived instance.
        /// </summary>
        /// <param name="timestamp">The timestamp to set.</param>
        /// <returns>The modified EventStoreReceived instance.</returns>
        private EventStoreReceived SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
            return this;
        }

        /// <summary>
        /// Sets the channel of an EventStoreReceived object.
        /// </summary>
        /// <param name="channel">The channel to set.</param>
        /// <returns>The modified EventStoreReceived object.</returns>
        private EventStoreReceived SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata for the event.
        /// </summary>
        /// <param name="metadata">The metadata to be set for the event.</param>
        /// <returns>The updated EventStoreReceived object with the modified metadata.</returns>
        private EventStoreReceived SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the event.
        /// </summary>
        /// <param name="body">The body of the event.</param>
        /// <returns>A reference to the updated EventStoreReceived object.</returns>
        private EventStoreReceived SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// SetTags method sets the tags of an EventStoreReceived object.
        /// @param tags A dictionary containing the key-value pairs of tags to be set.
        /// @returns The modified EventStoreReceived object with the updated tags.
        /// /
        private EventStoreReceived SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Sets the sequence value of the EventStoreReceived object.
        /// </summary>
        /// <param name="sequence">The sequence value.</param>
        /// <returns>The modified EventStoreReceived object.</returns>
        private EventStoreReceived SetSequence(ulong sequence)
        {
            Sequence = sequence;
            return this;
        }

        /// <summary>
        /// Converts the <see cref="KubeMQ.Grpc.EventReceive"/> object to <see cref="EventStoreReceived"/>.
        /// </summary>
        /// <param name="eventReceive">The <see cref="KubeMQ.Grpc.EventReceive"/> object to convert.</param>
        /// <returns>The converted <see cref="EventStoreReceived"/> object.</returns>
        internal  EventStoreReceived Decode(pb.EventReceive eventReceive)
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
            return new EventStoreReceived()
                .SetId(eventReceive.EventID)
                .SetTimestamp(new DateTime(eventReceive.Timestamp))
                .SetSequence(eventReceive.Sequence)
                .SetFromClientId(fromClientId)
                .SetChannel(eventReceive.Channel)
                .SetMetadata(eventReceive.Metadata)
                .SetBody(eventReceive.Body.ToByteArray()) 
                .SetTags(tags);
        }
    }
}