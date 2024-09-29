using System;
using System.Collections.Generic;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Commands
{
    public class Command
    {
        /// <summary>
        /// The ID of the command message.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The channel through which the command message will be sent.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Additional metadata associated with the command message.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// The body of the command message as bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// A dictionary of key-value pairs representing tags associated with the command message.
        /// </summary>
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// The maximum time in seconds for which the command message is valid.
        /// </summary>
        public int TimeoutInSeconds { get; set; }
        
        // public Command()
        // {
        //     Id = Guid.NewGuid().ToString();
        //     Channel = string.Empty;
        //     Metadata = string.Empty;
        //     Body = Array.Empty<byte>();
        //     Tags = new Dictionary<string, string>();
        //     TimeoutInSeconds = 0;
        // }
        //
        // public Command()
        // {
        //     
        // }
        // public Command(string id = null, string channel = null, string metadata = null, byte[] body = null, Dictionary<string, string> tags = null, int timeoutInSeconds = 0)
        // {
        //     Id = id;
        //     Channel = channel;
        //     Metadata = metadata;
        //     Body = body ?? Array.Empty<byte>();
        //     Tags = tags ?? new Dictionary<string, string>();
        //     TimeoutInSeconds = timeoutInSeconds;
        // }

        /// <summary>
        /// Sets the identifier of the command message.
        /// </summary>
        /// <param name="id">The identifier to set.</param>
        /// <returns>The <see cref="Command"/> instance with the modified identifier.</returns>
        public Command SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the channel for the Command message.
        /// </summary>
        /// <param name="channel">The channel to set for the Command message.</param>
        /// <returns>The updated Command instance.</returns>
        public Command SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata value for the command.
        /// </summary>
        /// <param name="metadata">The metadata value for the command.</param>
        /// <returns>The updated <see cref="Command"/> object.</returns>
        public Command SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the command message.
        /// </summary>
        /// <param name="body">The byte array representing the body of the command.</param>
        /// <returns>The updated <see cref="Command"/> instance.</returns>
        public Command SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// <summary>
        /// Sets the tags for the command message.
        /// </summary>
        /// <param name="tags">The dictionary of key-value pairs representing the tags.</param>
        /// <returns>The <see cref="Command"/> instance with the updated tags.</returns>
        public Command SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Sets the timeout for the command message.
        /// </summary>
        /// <param name="timeoutInSeconds">The timeout duration in seconds.</param>
        /// <returns>The updated <see cref="Command"/> instance.</returns>
        public Command SetTimeout(int timeoutInSeconds)
        {
            TimeoutInSeconds = timeoutInSeconds;
            return this;
        }
        
        
        /// <summary>
        /// Validates the command message. Throws a <see cref="ArgumentException"/> if the message is invalid.
        /// </summary>
        /// <returns>The validated <see cref="Command"/> instance.</returns>
        internal Command Validate()
        {
            if (string.IsNullOrEmpty(Channel))
                throw new ArgumentException("Command message must have a channel.");

            if (string.IsNullOrEmpty(Metadata) && Body.Length == 0 && Tags.Count == 0)
                throw new ArgumentException("Command message must have at least one of the following: metadata, body, or tags.");

            if (TimeoutInSeconds <= 0)
                throw new ArgumentException("Command message timeout must be a positive integer.");

            return this;
        }

        /// <summary>
        /// Encodes the command message into a protocol buffer message.
        /// </summary>
        /// <param name="clientId">The ID of the client.</param>
        /// <returns>The encoded message.</returns>
        internal pb.Request Encode(string clientId)
        {
            var pbCommand = new pb.Request()
            {
                RequestID = Id ?? Guid.NewGuid().ToString(),
                ClientID = clientId,
                Channel = Channel,
                Metadata = Metadata ?? string.Empty,
                Body = ByteString.CopyFrom(Body),
                Timeout = TimeoutInSeconds * 1000,
                RequestTypeData = pb.Request.Types.RequestType.Command
            };

            foreach (var kvp in Tags)
                pbCommand.Tags.Add(kvp.Key, kvp.Value);

            return pbCommand;
        }
    }
}