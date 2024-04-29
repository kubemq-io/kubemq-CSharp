using System;
using System.Collections.Generic;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.PubSub.Events
{
    /// <summary>
    /// Represents an event received from KubeMQ.
    /// </summary>
    public class EventReceived
    {
        /// <summary>
        /// Gets the identifier of the event.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Represents an event that is received from a client.
        /// </summary>
        public string FromClientId { get; private set; }

        /// <summary>
        /// Represents the timestamp of an event.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// received.
        public string Channel { get; private set; }

        /// <summary>
        /// Represents the metadata associated with an event received by the KubeMQ SDK.
        /// </summary>
        public string Metadata { get; private set; }

        /// <summary>
        /// Represents the body of an event.
        /// </summary>
        /// <remarks>
        /// This property contains the actual data of the event. It is stored as a byte array.
        /// The body can be encoded and decoded based on the needs of the application.
        /// </remarks>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Represents the event received from KubeMQ.
        /// </summary>
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Sets the ID of the event.
        /// </summary>
        /// <param name="id">The ID to set.</param>
        /// <returns>The <see cref="EventReceived"/> instance.</returns>
        private EventReceived SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the FromClientId property of the EventReceived object.
        /// </summary>
        /// <param name="fromClientId">The client ID from which the event is received.</param>
        /// <returns>The modified EventReceived object.</returns>
        private EventReceived SetFromClientId(string fromClientId)
        {
            FromClientId = fromClientId;
            return this;
        }

        /// <summary>
        /// Sets the timestamp of the event.
        /// </summary>
        /// <param name="timestamp">The timestamp to set for the event.</param>
        /// <returns>The EventReceived object with the updated timestamp.</returns>
        private EventReceived SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
            return this;
        }

        /// <summary>
        /// Sets the channel of the event received.
        /// </summary>
        /// <param name="channel">The channel to set.</param>
        /// <returns>The <see cref="EventReceived"/> instance with the channel set.</returns>
        private EventReceived SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata of the event received.
        /// </summary>
        /// <param name="metadata">The metadata to set for the event received.</param>
        /// <returns>The updated EventReceived object.</returns>
        private EventReceived SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the event received.
        /// </summary>
        /// <param name="body">The body of the event received as a byte array.</param>
        /// <returns>The updated EventReceived object.</returns>
        private EventReceived SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// <summary>
        /// Sets the tags for the event.
        /// </summary>
        /// <param name="tags">The dictionary of tags.</param>
        /// <returns>The updated EventReceived object.</returns>
        private EventReceived SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Converts a protobuf event object into the EventReceived object.
        /// </summary>
        /// <param name="eventReceive">The event to convert.</param>
        /// <returns>The converted EventReceived object.</returns>
        internal static EventReceived FromEvent(pb.EventReceive eventReceive)
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