using KubeMQ.Grpc;
using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.Queue
{
    public class Message
    {

        /// <summary>
        /// Unique for message
        /// </summary>
        public string MessageID { get => string.IsNullOrEmpty(_messageID) ? Tools.IDGenerator.ReqID.Getid() : _messageID; set => _messageID = value; }
        public string ClientID { get; set; }
        /// <summary>
        /// Queue name
        /// </summary>
        public string Queue { get; set; }
        /// <summary>
        /// General information about the message body.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// The information that you want to pass.
        /// </summary>
        public byte[] Body { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        /// <summary>
        /// Information of received message
        /// </summary>
        public QueueMessageAttributes Attributes { get; }
        /// <summary>
        /// Information of received message
        /// </summary>
        public QueueMessagePolicy Policy { get; set; }
        private string _messageID;
        public Message(QueueMessage message)
        {
            this.MessageID = message.MessageID;
            this.ClientID = message.ClientID;
            this.Queue = message.Channel;
            this.Metadata = message.Metadata;
            this.Body = message.Body.ToByteArray();
            this.Tags = null;// item.Tags,
            this.Attributes = message.Attributes;
            this.Policy = message.Policy;
        }

        public Message()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="body">The information that you want to pass.</param>
        /// <param name="metadata">General information about the message body.</param>
        /// <param name="messageID">Unique for message</param>
        /// <param name="tags"></param>

        public Message(byte[] body, string metadata, string messageID = null, Dictionary<string, string> tags = null)
        {
            MessageID = string.IsNullOrEmpty(messageID) ? Tools.IDGenerator.ReqID.Getid() : messageID;
            Metadata = metadata;
            Tags = tags;
            Body = body;
        }

    }


}