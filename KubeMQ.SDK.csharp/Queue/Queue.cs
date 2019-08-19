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
  
    /// <summary>
    /// Represents a Queue pattern.
    /// </summary>
    public class Queue : GrpcClient
    {
        /// <summary>
        /// Represents The FIFO queue name to send to using the KubeMQ.
        /// </summary>
        public string QueueName { get; private set; }
        /// <summary>
        /// Represents the sender ID that the messages will be send under.
        /// </summary>
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
            set
            {
                _WaitTimeSecondsQueueMessages = value;
            }
        }

        private readonly int _MaxNumberOfMessagesQueueMessages = 32;
        private int _WaitTimeSecondsQueueMessages =1;
        private static ILogger _logger;
        private Transaction _transation;

        /// <summary>
        /// Distributed durable FIFO based queues with the following core
        /// </summary>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        public Queue(ILogger logger = null)
        {
            _logger = Logger.InitLogger(logger, "Queue");
        }
        /// <summary>
        /// Distributed durable FIFO based queues with the following core 
        /// </summary>
        /// <param name="queueName">Represents The FIFO queue name to send to using the KubeMQ.</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under.</param>
        /// <param name="kubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000".</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        public Queue(string queueName, string clientID, string kubeMQAddress = null, ILogger logger = null) :this(queueName,clientID,null,null,kubeMQAddress, logger)
        {          

        }
        /// <summary>
        /// Distributed durable FIFO based queues with the following core 
        /// </summary>
        /// <param name="queueName">Represents The FIFO queue name to send to using the KubeMQ.</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under.</param>
        /// <param name="maxNumberOfMessagesQueueMessages">Number of received messages in request</param>
        /// <param name="waitTimeSecondsQueueMessages">Wait time for received messages</param>
        /// <param name="kubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000".</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        public Queue(string queueName, string clientID, int? maxNumberOfMessagesQueueMessages=null, int? waitTimeSecondsQueueMessages = null, string kubeMQAddress = null, ILogger logger = null)
        {
            this.QueueName = queueName;
            this.ClientID = clientID;
            this._kubemqAddress = kubeMQAddress;
            this._MaxNumberOfMessagesQueueMessages = maxNumberOfMessagesQueueMessages?? _MaxNumberOfMessagesQueueMessages;
            this._WaitTimeSecondsQueueMessages = waitTimeSecondsQueueMessages?? _WaitTimeSecondsQueueMessages;
            _logger = Logger.InitLogger(logger, "Queue");
            this.Ping();
        }

        /// <summary>
        /// Send single message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public SendMessageResult SendQueueMessage(Message message)
        {
            message.Queue = message.Queue ?? this.QueueName;
            message.ClientID = message.ClientID ?? this.ClientID;

            SendQueueMessageResult rec = GetKubeMQClient().SendQueueMessage(Tools.Converter.ConvertQueueMessage(message));

            return new SendMessageResult(rec);

        }

        /// <summary>
        /// Sending queue messages array request , waiting for response or timeout 
        /// </summary>
        /// <param name="queueMessages">Array of Messages</param>     
        /// <returns></returns>
        public SendBatchMessageResult SendQueueMessagesBatch(IEnumerable<Message> queueMessages)
        {
          
                QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch(new QueueMessagesBatchRequest
                {
                    BatchID = Tools.IDGenerator.ReqID.Getid(),
                    Messages = { Tools.Converter.ToQueueMessages(queueMessages, this) }
                });

                return new SendBatchMessageResult(rec);
        }

        /// <summary>
        /// Recessive messages from queues
        /// </summary>
        /// <param name="maxNumberOfMessagesQueueMessages">number of returned messages, default is 32</param>
        /// <returns></returns>
        public ReceiveMessagesResponse ReceiveQueueMessages(int? maxNumberOfMessagesQueueMessages = null)
        {
                ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
                {
                    RequestID = Tools.IDGenerator.ReqID.Getid(),
                    ClientID = ClientID,
                    Channel = QueueName,
                    MaxNumberOfMessages = maxNumberOfMessagesQueueMessages ?? MaxNumberOfMessagesQueueMessages,
                    WaitTimeSeconds = WaitTimeSecondsQueueMessages
                });

                return new ReceiveMessagesResponse(rec);         
        }

        /// <summary>
        /// QueueMessagesRequest for peak queue messages
        /// </summary>
        /// <param name="maxNumberOfMessagesQueueMessages">number of returned messages, default is 32 </param>
        /// <returns></returns>
        public ReceiveMessagesResponse PeakQueueMessage(int? maxNumberOfMessagesQueueMessages = null)
        {          
                ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
                {
                    RequestID = Tools.IDGenerator.ReqID.Getid(),
                    ClientID = ClientID,
                    Channel = QueueName,
                    IsPeak = true,
                    MaxNumberOfMessages = maxNumberOfMessagesQueueMessages ?? MaxNumberOfMessagesQueueMessages,
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
                });

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
        /// <summary>
        /// Ping KubeMQ address to check Grpc connection
        /// </summary>
        /// <returns></returns>
        public PingResult Ping()
        {
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            _logger.LogDebug($"Queue KubeMQ address:{_kubemqAddress} ping result:{rec}");
            return rec;

        }
       
    }
}