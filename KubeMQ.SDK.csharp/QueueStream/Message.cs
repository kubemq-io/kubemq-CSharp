using System;
using KubeMQ.Grpc;
using System.Collections.Generic;
using Google.Protobuf.Collections;

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
        /// <param name="body">The information that you want to pass.</param>
        /// <param name="metadata">General information about the message body.</param>
        /// <param name="messageID">Unique for message</param>
        /// <param name="tags">Dictionary of string , string pair:A set of Key value pair that help categorize the message.</param>

        public Message(byte[] body, string metadata, string messageID = null, Dictionary<string, string> tags = null)
        {
            MessageID = string.IsNullOrEmpty(messageID) ? Tools.IDGenerator.Getid() : messageID;
            Metadata = string.IsNullOrEmpty(metadata) ? "":metadata ;
            Tags = tags;
            Body = body;
        }


        public void Ack()
        {
            QueuesDownstreamRequest ackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.AckRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            ackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(ackRequest);
        }
        public void NAck()
        {
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            nackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(nackRequest);
        }
        public void ReQueue(string queue)
        {
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