using System;
using KubeMQ.Grpc;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static System.Guid;
using Google.Protobuf;
namespace KubeMQ.SDK.csharp.QueueStream
{
    /// <summary>
    /// Queue stored message
    /// </summary>
    public class Message
    {
        
        /// <summary>
        /// Unique for message
        /// </summary>
        public string MessageID { get => string.IsNullOrEmpty(_messageID) ? Tools.IDGenerator.Getid() : _messageID; set => _messageID = value; }
        /// <summary>
        /// Represents the sender ID that the messages will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents The FIFO queue name to send to using the KubeMQ.
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
        /// <summary>
        /// Dictionary of string , string pair:A set of Key value pair that help categorize the message.
        /// </summary>
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

        private string _transactionId = "";
        private DownstreamRequestHandler _requestHandler;
        /// <summary>
        /// Queue stored message
        /// </summary>
        /// <param name="message"></param>
        internal Message(QueueMessage message, DownstreamRequestHandler requestHandler, string transactionId)
        {
            _requestHandler = requestHandler;
            _transactionId = transactionId;
            this.MessageID = message.MessageID;
            this.ClientID = message.ClientID;
            this.Queue = message.Channel;
            this.Metadata = message.Metadata;
            this.Body = message.Body.ToByteArray();
            this.Tags = Tools.Converter.ReadTags(message.Tags);
            this.Attributes = message.Attributes;
            this.Policy = message.Policy;
        }

        /// <summary>
        ///  Queue stored message
        /// </summary>
        public Message()
        {

        }

        /// <summary>
        /// Queue stored message
        /// </summary>
        /// <param name="queue">queue name</param>
        /// <param name="body">Message payload.</param>
        /// <param name="metadata">General information about the message body.</param>
        /// <param name="messageId">Unique for message</param>
        /// <param name="tags">Dictionary of string , string pair:A set of Key value pair that help categorize the message.</param>
        public Message(string queue,byte[] body, string metadata, string messageId = null, Dictionary<string, string> tags = null)
        {
            Queue = queue;
            MessageID = string.IsNullOrEmpty(messageId) ? Tools.IDGenerator.Getid() : messageId;
            Metadata = string.IsNullOrEmpty(metadata) ? "":metadata ;
            Tags = tags;
            Body = body;
        }
        private  MapField<string, string> ToMapFields(Dictionary<string, string> tags)
        {
            MapField<string, string> keyValuePairs = new MapField<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Key, item.Value);
                }
            }
            return keyValuePairs;
        }
        internal QueueMessage ToQueueMessage(string clientId)
        {
            QueueMessage pbMessage = new QueueMessage();
            pbMessage.MessageID=string.IsNullOrEmpty(MessageID)? NewGuid().ToString(): MessageID;
            pbMessage.Channel = Queue;
            pbMessage.ClientID = string.IsNullOrEmpty(ClientID) ? clientId : ClientID;
            pbMessage.Metadata = string.IsNullOrEmpty(Metadata) ? "" : Metadata;
            pbMessage.Body = Body == null ? ByteString.Empty : ByteString.CopyFrom(Body);
            pbMessage.Tags.Add(ToMapFields(Tags));
            pbMessage.Policy = Policy;
            return pbMessage;
        }
        private void CheckValidOperation()
        {
            if (_requestHandler == null)
            {
                throw new InvalidOperationException("this method is not valid in this context");
            }
        }
        /// <summary>
        /// Ack the current message (accept)
        /// </summary>
        public void Ack()
        {
            CheckValidOperation();
            QueuesDownstreamRequest ackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.AckRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            ackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(ackRequest);
        }
        /// <summary>
        /// NAck the current message (reject)
        /// </summary>
        public void NAck()
        {
            CheckValidOperation();
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            nackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(nackRequest);
        }
        /// <summary>
        /// Requeue the current message to a new queue
        /// </summary>
        /// <param name="queue">requeue  queue name</param>
        public void ReQueue(string queue)
        {
            CheckValidOperation();
            QueuesDownstreamRequest requeueRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.ReQueueRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
                ReQueueChannel = queue
            };
            requeueRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(requeueRequest);
        }
    }

}