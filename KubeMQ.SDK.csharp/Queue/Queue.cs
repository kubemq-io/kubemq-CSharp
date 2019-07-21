using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using KubeMQ.SDK.csharp.Tools;
using Microsoft.Extensions.Logging;
using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class QueueMessageConstans
    {
        public string QueueName { get; set; }
        public string ClientID { get; set; }
        public int? MaxNumberOfMessagesQueueMessages { get; set; }
        public int? WaitTimeSecondsQueueMessages { get; set; }

    }

    /// <summary>
    /// Represents a Queue patteren.
    /// </summary>
    public class Queue : GrpcClient
    {    
        /// <summary>
        /// Queue name 
        /// </summary>
        public string QueueName { get; private set; }       
        public string ClientID { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public int MaxNumberOfMessagesQueueMessages
        {
            get
            {
                return _MaxNumberOfMessagesQueueMessages;
            }
        }
        /// <summary>
        /// 
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

        public SendMessageResult SendQueueMessage(Message message)
        {
            SendQueueMessageResult rec = GetKubeMQClient().SendQueueMessage(new KubeMQGrpc.QueueMessage
            {
                MessageID = message.MessageID,
                Metadata = message.Metadata,
                ClientID = ClientID,
                Channel = QueueName,
                Tags = { Tools.Converter.ConvertTags(message.Tags) },
                Body = ByteString.CopyFrom(message.Body)
            });

            return new SendMessageResult(rec);
        }

        public SendBatchMessageResult SendQueueMessagesBatch(IEnumerable<Message> queueMessages, string batchID = null)
        {
            QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch(new QueueMessagesBatchRequest
            {
                BatchID = string.IsNullOrEmpty(batchID) ? Tools.IDGenerator.ReqID.Getid() : batchID,
                Messages = { convertMesages(queueMessages) }
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

        public AckAllMessagesResponse ackAllQueueMessagesResponse()
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
        public Transaction CreateTransaction()
        {
             return new Transaction(this);
        }
    
        #endregion

        public PingResult Ping()
        {
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            return rec;

        }
        private RepeatedField<QueueMessage> convertMesages(IEnumerable<Message> queueMessages)
        {
            RepeatedField<QueueMessage> testc = new RepeatedField<QueueMessage>();
            foreach (var item in queueMessages)
            {
                testc.Add(new QueueMessage
                {
                    ClientID = ClientID,
                    Channel = QueueName,
                    MessageID = item.MessageID,
                    Body = ByteString.CopyFrom(item.Body),
                    Metadata = item.Metadata,
                    Tags = { Tools.Converter.ConvertTags(item.Tags) },
                });
            }
            return testc;
        }
    }
}