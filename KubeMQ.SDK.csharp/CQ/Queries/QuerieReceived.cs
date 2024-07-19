using System;
using System.Collections.Generic;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Queries
{
    public class QueryReceived
    {
        /// <summary>
        /// The ID of the query message.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the client that sent the query message.
        /// </summary>
        public string FromClientId { get; set; } = string.Empty;

        /// <summary>
        /// The timestamp when the query message was received.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.FromBinary(0);

        /// <summary>
        /// The channel on which the query message was received.
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata associated with the query message.
        /// </summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>
        /// The body of the query message.
        /// </summary>
        public byte[] Body { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The channel to which any reply to the query message should be sent.
        /// </summary>
        public string ReplyChannel { get; set; } = string.Empty;

        /// <summary>
        /// Additional tags associated with the query message.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Static method that takes a pbRequest object as input and decodes it into a QueryMessageReceived object.
        /// </summary>
        /// <param name="queryReceive">The pbRequest object to be decoded.</param>
        /// <returns>A QueryMessageReceived object.</returns>
        public static QueryReceived Decode(pb.Request queryReceive)
        {
            var message = new QueryReceived
            {
                Id = queryReceive.RequestID,
                FromClientId = queryReceive.ClientID,
                Timestamp = DateTime.Now,
                Channel = queryReceive.Channel,
                Metadata = queryReceive.Metadata,
                Body = queryReceive.Body.ToByteArray(),
                ReplyChannel = queryReceive.ReplyChannel
            };

            return message;
        }

        public override string ToString()
        {
            return $"QueryMessageReceived: id={Id}, channel={Channel}, metadata={Metadata}, body={Body}, from_client_id={FromClientId}, timestamp={Timestamp}, reply_channel={ReplyChannel}, tags={string.Join(",", Tags)}";
        }
    }
}