using System.Collections.Generic;
using Google.Protobuf;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Tools;
using Microsoft.Extensions.Logging;
using KubeMQ.SDK.csharp.Queue.Stream;
using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class QueueMessageConstans
    {
        public string QueueName { get; set; }
        public string ClientID { get; set; }
        /// <summary>
        ///Number of received messages in request
        /// </summary>
        public int? MaxNumberOfMessagesQueueMessages { get; set; }
        /// <summary>
        /// Wait time for received messages
        /// </summary>
        public int? WaitTimeSecondsQueueMessages { get; set; }

    }

    /// <summary>
    /// Represents a Queue pattern.
    /// </summary>
    public class Queue : GrpcClient
    {    
        /// <summary>
        /// Queue name as Channle name
        /// </summary>
        public string QueueName { get; private set; }       
        public string ClientID { get; private set; }
        /// <summary>
        ///Number of received messages
        /// </summary>
        public int MaxNumberOfMessagesQueueMessages
        {
            get
            {
                return _MaxNumberOfMessagesQueueMessages;
            }
        }
        /// <summary>
        /// Wait time for received messages
        /// </summary>
        public int WaitTimeSecondsQueueMessages
        {
            get
            {
                return _WaitTimeSecondsQueueMessages;
            }
        }

        private int _MaxNumberOfMessagesQueueMessages = 32;
        private int _WaitTimeSecondsQueueMessages =1;
        private static ILogger logger;
        private Transaction _transation;

        /// <summary>
        /// 
        /// </summary>
        public Queue(ILogger logger = null)
        {

        }
      
        public Queue(string queueName, string clientID, string kubeMQAddress = null, ILogger logger = null) :this(queueName,clientID,null,null,kubeMQAddress)
        {          

        }
        public Queue(QueueMessageConstans constans, ILogger logger = null) : this(constans.QueueName, constans.ClientID, constans.MaxNumberOfMessagesQueueMessages, constans.WaitTimeSecondsQueueMessages)
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="clientID"></param>
        /// <param name="maxNumberOfMessagesQueueMessages">Number of received messages in request</param>
        /// <param name="waitTimeSecondsQueueMessages">Wait time for received messages</param>
        /// <param name="kubeMQAddress"></param>
        /// <param name="logger"></param>
        public Queue(string queueName, string clientID, int? maxNumberOfMessagesQueueMessages, int? waitTimeSecondsQueueMessages, string kubeMQAddress = null, ILogger logger = null)
        {
            this.QueueName = queueName;
            this.ClientID = clientID;
            this._kubemqAddress = kubeMQAddress;
            this._MaxNumberOfMessagesQueueMessages = maxNumberOfMessagesQueueMessages?? _MaxNumberOfMessagesQueueMessages;
            this._WaitTimeSecondsQueueMessages = waitTimeSecondsQueueMessages?? _WaitTimeSecondsQueueMessages;
            logger = Logger.InitLogger(logger, "Queue");
            this.Ping();
        }

        /// <summary>
        /// Send single message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public SendMessageResult SendQueueMessage(Message message)
        {
            SendQueueMessageResult rec = GetKubeMQClient().SendQueueMessage(new KubeMQGrpc.QueueMessage
            {
                MessageID = message.MessageID,
                Metadata = message.Metadata,
                ClientID = ClientID,
                Channel = QueueName,
                Tags = { Tools.Converter.CreateTags(message.Tags) },
                Body = ByteString.CopyFrom(message.Body),              
            }) ;

            return new SendMessageResult(rec);
        }

        public SendBatchMessageResult SendQueueMessagesBatch(IEnumerable<Message> queueMessages, string batchID = null)
        {
            QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch(new QueueMessagesBatchRequest
            {
                BatchID = string.IsNullOrEmpty(batchID) ? Tools.IDGenerator.ReqID.Getid() : batchID,
                Messages = { Tools.Converter.ToQueueMessages(queueMessages, this) }
            });

            return new SendBatchMessageResult(rec);
        }

        public ReceiveMessagesResponse ReceiveQueueMessages()
        {
         
            ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
            {
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                ClientID = ClientID,
                
                Channel = QueueName,


                MaxNumberOfMessages = MaxNumberOfMessagesQueueMessages,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            });

            return new ReceiveMessagesResponse(rec);
        }

        public ReceiveMessagesResponse PeakQueueMessage()
        {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
            {
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                ClientID = ClientID,
                Channel= QueueName,
                IsPeak = true,
                MaxNumberOfMessages = MaxNumberOfMessagesQueueMessages,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            });

            return new ReceiveMessagesResponse(rec);
        }

        public AckAllMessagesResponse AckAllQueueMessagesResponse()
        {
            AckAllQueueMessagesResponse rec = GetKubeMQClient().AckAllQueueMessages(new AckAllQueueMessagesRequest
            {
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                Channel = QueueName,
                ClientID = ClientID,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            }) ;
            return new AckAllMessagesResponse(rec);
        }

        #region "Transactional"
        /// <summary>
        /// Advance manipulation of messages using stream
        /// </summary>
        /// <returns>Static Transaction stream</returns>
        public Transaction CreateTransaction()
        {
            if (_transation == null)
            {
                _transation = new Transaction(this);
            }
             return _transation;
        }
    
        #endregion

        public PingResult Ping()
        {
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            return rec;

        }
       
    }
}