using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Queue.Stream;
using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Tools;
using Microsoft.Extensions.Logging;
using static KubeMQ.SDK.csharp.Common.Common;
using KubeMQGrpc = KubeMQ.Grpc;
using Result = KubeMQ.SDK.csharp.Results.Result;

namespace KubeMQ.SDK.csharp.Queue {

    /// <summary>
    /// Represents a Queue pattern.
    /// </summary>
    public class Queue : GrpcClient {
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
        public int MaxNumberOfMessagesQueueMessages {
            get {
                return _maxNumberOfMessagesQueueMessages;
            }
        }
        /// <summary>
        /// Wait time for received messages
        /// </summary>
        public int WaitTimeSecondsQueueMessages {
            get {
                return _waitTimeSecondsQueueMessages;
            }
            set {
                _waitTimeSecondsQueueMessages = value;
            }
        }

        private readonly int _maxNumberOfMessagesQueueMessages = 32;
        private int _waitTimeSecondsQueueMessages = 1;
        private static ILogger _logger;
        private Transaction _transation;

        /// <summary>
        /// Distributed durable FIFO based queues with the following core
        /// </summary>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
           /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Queue (ILogger logger = null, string authToken = null) {
            _logger = Logger.InitLogger (logger, "Queue");
        }
        /// <summary>
        /// Distributed durable FIFO based queues with the following core 
        /// </summary>
        /// <param name="queueName">Represents The FIFO queue name to send to using the KubeMQ.</param>
        /// <param name="clientID">Represents the sender ID that the messages will be send under.</param>
        /// <param name="kubeMQAddress">The address the of the KubeMQ including the GRPC Port ,Example: "LocalHost:50000".</param>
        /// <param name="logger">Optional Microsoft.Extensions.Logging.ILogger, Logger will write to default output with suffix KubeMQSDK.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Queue (string queueName, string clientID, string kubeMQAddress = null, ILogger logger = null, string authToken = null) : this (queueName, clientID, null, null, kubeMQAddress, logger, authToken) {

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
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Queue (string queueName, string clientID, int? maxNumberOfMessagesQueueMessages = null, int? waitTimeSecondsQueueMessages = null, string kubeMQAddress = null, ILogger logger = null, string authToken = null) {
            this.QueueName = queueName;
            this.ClientID = clientID;
            this._kubemqAddress = kubeMQAddress;
            this._maxNumberOfMessagesQueueMessages = maxNumberOfMessagesQueueMessages?? _maxNumberOfMessagesQueueMessages;
            this._waitTimeSecondsQueueMessages = waitTimeSecondsQueueMessages?? _waitTimeSecondsQueueMessages;
            this.addAuthToken (authToken);
            _logger = Logger.InitLogger (logger, "Queue");
            //this.Ping ();
        }
        /// <summary>
        /// Send multisubscribers message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public SendMessageResult Send (Message message) {
            message.Queue = message.Queue ?? this.QueueName;
            message.ClientID = message.ClientID ?? this.ClientID;
            SendQueueMessageResult rec = GetKubeMQClient ().SendQueueMessage (Tools.Converter.ConvertQueueMessage (message),Metadata);

            return new SendMessageResult (rec);
        }
        
        /// <summary>
        /// Send multisubscribers message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Send instead.", false)]
        public SendMessageResult SendQueueMessage (Message message) {
            message.Queue = message.Queue ?? this.QueueName;
            message.ClientID = message.ClientID ?? this.ClientID;

            SendQueueMessageResult rec = GetKubeMQClient ().SendQueueMessage (Tools.Converter.ConvertQueueMessage (message),Metadata);

            return new SendMessageResult (rec);

        }
        /// <summary>
        /// Sending queue messages array request , waiting for response or timeout 
        /// </summary>
        /// <param name="queueMessages">Array of Messages</param>     
        /// <returns></returns>
        public SendBatchMessageResult Batch (IEnumerable<Message> queueMessages) {

            QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch (new QueueMessagesBatchRequest {
                BatchID = Tools.IDGenerator.Getid (),
                Messages = { Tools.Converter.ToQueueMessages (queueMessages, this) }
            },Metadata);

            return new SendBatchMessageResult (rec);
        }
        /// <summary>
        /// Sending queue messages array request , waiting for response or timeout 
        /// </summary>
        /// <param name="queueMessages">Array of Messages</param>     
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Batch instead.", false)]
        public SendBatchMessageResult SendQueueMessagesBatch (IEnumerable<Message> queueMessages) {

            QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch (new QueueMessagesBatchRequest {
                BatchID = Tools.IDGenerator.Getid (),
                    Messages = { Tools.Converter.ToQueueMessages (queueMessages, this) }
            },Metadata);

            return new SendBatchMessageResult (rec);
        }

        /// <summary>
        /// Pull messages from queue.
        /// </summary>
        /// <param name="maxPullMessages">number of max returned messages to pull</param>
        /// <param name="waitTimeoutSeconds">how long to wait for all messages </param>
        /// <returns></returns>
        public ReceiveMessagesResponse Pull (int maxPullMessages , int waitTimeoutSeconds ) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                ClientID = ClientID,
                Channel = QueueName,
                MaxNumberOfMessages = maxPullMessages ,
                WaitTimeSeconds = waitTimeoutSeconds
            },Metadata);
            return new ReceiveMessagesResponse (rec);
        }

        /// <summary>
        /// Pull messages from queue.
        /// </summary>
        /// <param name="queue">queue name to pull</param>
        /// <param name="maxPullMessages">number of max returned messages to pull</param>
        /// <param name="waitTimeoutSeconds">how long to wait for all messages </param>
        /// <returns></returns>
        public ReceiveMessagesResponse Pull (string queue, int maxPullMessages , int waitTimeoutSeconds ) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                ClientID = ClientID,
                Channel = queue,
                MaxNumberOfMessages = maxPullMessages ,
                WaitTimeSeconds = waitTimeoutSeconds
            },Metadata);
            return new ReceiveMessagesResponse (rec);
        }
        /// <summary>
        /// Peek messages from queue.
        /// </summary>
        /// <param name="queue">queue name to peek</param>
        /// <param name="maxPeekMessages">number of max returned messages to peek</param>
        /// <param name="waitTimeoutSeconds">how long to wait for all messages </param>
        /// <returns></returns>
        public ReceiveMessagesResponse Peek (string queue, int maxPeekMessages , int waitTimeoutSeconds ) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                ClientID = ClientID,
                Channel = queue,
                MaxNumberOfMessages = maxPeekMessages ,
                WaitTimeSeconds = waitTimeoutSeconds,
                IsPeak = true,
            },Metadata);
            return new ReceiveMessagesResponse (rec);
        }
        /// <summary>
        /// Peek messages from queue.
        /// </summary>
        /// <param name="maxPeekMessages">number of max returned messages to peek</param>
        /// <param name="waitTimeoutSeconds">how long to wait for all messages </param>
        /// <returns></returns>
       
        public ReceiveMessagesResponse Peek (int maxPeekMessages , int waitTimeoutSeconds ) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                ClientID = ClientID,
                Channel = QueueName,
                MaxNumberOfMessages = maxPeekMessages ,
                WaitTimeSeconds = waitTimeoutSeconds,
                IsPeak = true,
            },Metadata);
            return new ReceiveMessagesResponse (rec);
        }
        /// <summary>
        /// Recessive messages from queue.
        /// </summary>
        /// <param name="maxNumberOfMessagesQueueMessages">number of returned messages, default is 32</param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Pull", false)]
        public ReceiveMessagesResponse ReceiveQueueMessages (int? maxNumberOfMessagesQueueMessages = null) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                    ClientID = ClientID,
                    Channel = QueueName,
                    MaxNumberOfMessages = maxNumberOfMessagesQueueMessages ?? MaxNumberOfMessagesQueueMessages,
                    WaitTimeSeconds = WaitTimeSecondsQueueMessages
            },Metadata);
            return new ReceiveMessagesResponse (rec);
        }

        /// <summary>
        /// QueueMessagesRequest for peak queue messages
        /// </summary>
        /// <param name="maxNumberOfMessagesQueueMessages">number of returned messages, default is 32 </param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Peek instead.", false)]
        public ReceiveMessagesResponse PeekQueueMessage (int? maxNumberOfMessagesQueueMessages = null) {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient ().ReceiveQueueMessages (new ReceiveQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                    ClientID = ClientID,
                    Channel = QueueName,
                    IsPeak = true,
                    MaxNumberOfMessages = maxNumberOfMessagesQueueMessages ?? MaxNumberOfMessagesQueueMessages,
                    WaitTimeSeconds = WaitTimeSecondsQueueMessages
            },Metadata);

            return new ReceiveMessagesResponse (rec);

        }
        /// <summary>
        /// Mark all the messages as dequeued on queue.
        /// </summary>
        /// <param name="waitTimeoutSeconds">how long to wait for all messages to ack</param>
        /// <returns></returns>
        public AckAllMessagesResponse AckAll (int waitTimeoutSeconds) {
            AckAllQueueMessagesResponse rec = GetKubeMQClient ().AckAllQueueMessages (new AckAllQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                Channel = QueueName,
                ClientID = ClientID,
                WaitTimeSeconds = waitTimeoutSeconds
            },Metadata);

            return new AckAllMessagesResponse (rec);
        }
        /// <summary>
        /// Mark all the messages as dequeued on queue.
        /// </summary>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call AckAll instead.", false)]
        public AckAllMessagesResponse AckAllQueueMessages () {
            AckAllQueueMessagesResponse rec = GetKubeMQClient ().AckAllQueueMessages (new AckAllQueueMessagesRequest {
                RequestID = Tools.IDGenerator.Getid (),
                    Channel = QueueName,
                    ClientID = ClientID,
                    WaitTimeSeconds = WaitTimeSecondsQueueMessages
            },Metadata);

            return new AckAllMessagesResponse (rec);
        }


        /// <summary>
        /// Creates a channel with the given name.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task representing the asynchronous channel creation operation.</returns>
        public async Task<Result> CreateChannel (string channelName) {
            return await CreateDeleteChannel (GetKubeMQClient (), ClientID, channelName, "queues", true);
        }

        /// <summary>
        /// Deletes a channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>A task that represents the asynchronous delete operation. The task result is of type CommonAsyncResult.</returns>
        public async Task<Result> DeleteChannel (string channelName) {
            return await CreateDeleteChannel (GetKubeMQClient (), ClientID, channelName, "queues", false);
        }

        /// <summary>
        /// Lists all the channels within the KubeMQ server that match the search criteria.
        /// </summary>
        /// <param name="search">The search criteria to filter the channels. If left empty, all channels will be returned.</param>
        /// <returns>A task result containing a ListQueuesAsyncResult object with the channels that match the search criteria.</returns>
        public async Task<ListQueuesAsyncResult> ListChannels (string search = "") {
            return await ListQueuesChannels(GetKubeMQClient(), ClientID, search, "queues");
        }
        
        
        #region "Transactional"
        /// <summary>
        /// Advance manipulation of messages using stream
        /// </summary>
        /// <returns>Static Transaction stream</returns>
        public Transaction CreateTransaction () {
            if (_transation == null) {
                _transation = new Transaction (this);
            }
            return _transation;
        }

        #endregion
        /// <summary>
        /// Ping KubeMQ address to check Grpc connection
        /// </summary>
        /// <returns></returns>
        public KubeMQGrpc.PingResult Ping () {
            KubeMQGrpc.PingResult rec = GetKubeMQClient ().Ping (new Empty ());
            _logger.LogDebug ($"Queue KubeMQ address:{_kubemqAddress} ping result:{rec}");
            return rec;
        }

    }
}