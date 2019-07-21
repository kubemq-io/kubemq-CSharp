using KubeMQ.Grpc;
using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.Queue
{
    public class Message
    {
        private string messageID;

        public string MessageID { get => string.IsNullOrEmpty(messageID) ? Tools.IDGenerator.ReqID.Getid() : messageID; set => messageID = value; }
        public string ClientID { get; set; }
        public string Queue { get; set; }
        public string Metadata { get; set; }
        public byte[] Body { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public QueueMessageAttributes Attributes { get; }
        public QueueMessagePolicy Policy { get; }

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

        public Message(byte[] body, string metadata, string messageID = null, Dictionary<string, string> tags = null)
        {
            MessageID = string.IsNullOrEmpty(messageID) ? Tools.IDGenerator.ReqID.Getid() : messageID;
            Metadata = metadata;
            Tags = tags;
            Body = body;
        }

    }


}