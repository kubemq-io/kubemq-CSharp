using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class TransactionMessage
    {
        public string MessageID { get; set; }
        public string ClientID { get; set; }
        public string Queue { get; set; }
        public string Metadata { get; set; }
        public byte[] Body { get; set; }
        public object Tags { get; set; }
        public  QueueMessageAttributes Attributes { get; }
        public QueueMessagePolicy Policy { get;  }

        public TransactionMessage(QueueMessage message)
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

    }


}