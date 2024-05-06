using System;
using System.Collections.Generic;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Commands
{
    public class CommandReceived
    {
        /// <summary>
        /// The ID of the command message.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the client that sent the command message.
        /// </summary>
        public string FromClientId { get; set; } = string.Empty;

        /// <summary>
        /// The timestamp when the command message was received.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.FromBinary(0);

        /// <summary>
        /// The channel on which the command message was received.
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata associated with the command message.
        /// </summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>
        /// The body of the command message.
        /// </summary>
        public byte[] Body { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The channel to which any reply to the command message should be sent.
        /// </summary>
        public string ReplyChannel { get; set; } = string.Empty;

        /// <summary>
        /// Additional tags associated with the command message.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Static method that takes a pbRequest object as input and decodes it into a CommandMessageReceived object.
        /// </summary>
        /// <param name="commandReceive">The pbRequest object to be decoded.</param>
        /// <returns>A CommandMessageReceived object.</returns>
        public static CommandReceived Decode(pb.Request commandReceive)
        {
            var message = new CommandReceived
            {
                Id = commandReceive.RequestID,
                FromClientId = commandReceive.ClientID,
                Timestamp = DateTime.Now,
                Channel = commandReceive.Channel,
                Metadata = commandReceive.Metadata,
                Body = commandReceive.Body.ToByteArray(),
                ReplyChannel = commandReceive.ReplyChannel
            };

            return message;
        }

        public override string ToString()
        {
            return $"CommandMessageReceived: id={Id}, channel={Channel}, metadata={Metadata}, body={Body}, from_client_id={FromClientId}, timestamp={Timestamp}, reply_channel={ReplyChannel}, tags={string.Join(",", Tags)}";
        }
    }
}