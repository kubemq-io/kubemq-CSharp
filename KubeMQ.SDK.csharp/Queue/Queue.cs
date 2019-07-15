using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using static KubeMQ.Grpc.kubemq;
using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue {
    public class QueueMessageConstans {
        public string QueueName { get; set; }
        public string ClientID { get; set; }

        public string KubeMQAddress { get; set; }
    }

    public class Message {
        private static int _id = 0;
        public string MessageID { get; set; }
        public string Metadata { get; set; }
        public IEnumerable < (string, string) > Tags { get; set; }
        public byte[] Body { get; set; }      
        public Message () {

        }
        public Message (byte[] body, string metadata, string messageID = null, IEnumerable < (string, string) > tags = null) {
            MessageID = string.IsNullOrEmpty (messageID) ? ReqID.Getid() : messageID;
            Metadata = metadata;
            Tags = tags;
            Body = body;
        }

      

    }
    public class Queue : GrpcClient
    {
        private static int _id = 0;
         public string QueueName { get; private set; }
        public string ClientID { get; private set; }
        public int MaxNumberOfMessagesQueueMessages
        {
            get
            { return _MaxNumberOfMessagesQueueMessages; }
        }
        public int WaitTimeSecondsQueueMessages { get
            { return _WaitTimeSecondsQueueMessages; }  }

        private int _MaxNumberOfMessagesQueueMessages = 32;

        private int _WaitTimeSecondsQueueMessages =0;
        public Queue()
        {

        }
        public Queue(string queueName, string clientID, string KubeMQAddress = null)
        {
            QueueName = queueName;
            ClientID = clientID;
            _kubemqAddress = KubeMQAddress;

        }
        public Queue(QueueMessageConstans constans) : this(constans.QueueName, constans.ClientID, constans.KubeMQAddress)
        {

        }
        public Queue(string queueName, string clientID, int maxNumberOfMessagesQueueMessages, int waitTimeSecondsQueueMessages)
        {
            this.QueueName = queueName;
            this.ClientID = clientID;
            this._MaxNumberOfMessagesQueueMessages = maxNumberOfMessagesQueueMessages;
            this._WaitTimeSecondsQueueMessages = waitTimeSecondsQueueMessages;

        }

        public SendQueueMessageResult SendQueueMessage(Message message)
        {
            SendQueueMessageResult rec = GetKubeMQClient().SendQueueMessage(new KubeMQGrpc.QueueMessage
            {
                MessageID = message.MessageID,
                Metadata = message.Metadata,
                ClientID = ClientID,
                Channel = QueueName,
                Tags = { convertTags(message.Tags) },
                Body = ByteString.CopyFrom(message.Body)
            });

            return rec;
        }

        private MapField<string, string> convertTags(IEnumerable<(string, string)> tags)
        {
            MapField<string, string> keyValuePairs = new MapField<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Item1, item.Item2);
                }
            }
            return keyValuePairs;
        }

        public QueueMessagesBatchResponse SendQueueMessagesBatch(IEnumerable<Message> queueMessages, string batchID = null)
        {
            QueueMessagesBatchResponse rec = GetKubeMQClient().SendQueueMessagesBatch(new QueueMessagesBatchRequest
            {
                BatchID = string.IsNullOrEmpty(batchID) ? ReqID.Getid() : batchID,
                Messages = { convertMesages(queueMessages) }
            });

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
                    Tags = { convertTags(item.Tags) },
                });
            }
            return testc;
        }

        public ReceiveQueueMessagesResponse ReceiveQueueMessages()
        {
         
            ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
            {
                RequestID = ReqID.Getid(),
                ClientID = ClientID,
                
                Channel = QueueName,


                MaxNumberOfMessages = MaxNumberOfMessagesQueueMessages,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            });

            return rec;
        }
        public ReceiveQueueMessagesResponse PeakQueueMessage()
        {
            ReceiveQueueMessagesResponse rec = GetKubeMQClient().ReceiveQueueMessages(new ReceiveQueueMessagesRequest
            {
                RequestID = ReqID.Getid(),
                ClientID = ClientID,
                Channel= QueueName,
                IsPeak = true,
                MaxNumberOfMessages = MaxNumberOfMessagesQueueMessages,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            });

            return rec;
        }

        public AckAllQueueMessagesResponse ackAllQueueMessagesResponse()
        {
            AckAllQueueMessagesResponse rec = GetKubeMQClient().AckAllQueueMessages(new AckAllQueueMessagesRequest
            {
                RequestID = ReqID.Getid(),
                Channel = QueueName,
                ClientID = ClientID,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            }) ;
            return rec;
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

    }
    public class Transaction : GrpcClient
    {
        private Queue queue;
        private int _visibilitySeconds;
        private AsyncDuplexStreamingCall<StreamQueueMessagesRequest, StreamQueueMessagesResponse> stream;

        public string Status { get



            {
                return
                   stream==null?"stream is null":   stream.GetStatus().Detail;
            }       }


        public StreamQueueMessagesResponse msg { get; set; }
        //public Transaction(int visibilitySeconds=1)
        //{
        //    _visibilitySeconds = visibilitySeconds;
        //}

        public Transaction(Queue queue, int visibilitySeconds = 1)
        {
            this.queue = queue;
            _kubemqAddress = queue.ServerAddress;
            _visibilitySeconds = visibilitySeconds;
        }

        public StreamQueueMessagesResponse Receive()
        {
            stream = GetKubeMQClient().StreamQueueMessage();
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,     
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ReceiveMessage,
                VisibilitySeconds = _visibilitySeconds,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = new QueueMessage(),
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;
        }

        // public int WaitTimeSeconds { get; set; }
        public StreamQueueMessagesResponse AckMessage(QueueMessage r)
        {
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.AckMessage,
                VisibilitySeconds = _visibilitySeconds,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = new QueueMessage(),
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;
        }
        public StreamQueueMessagesResponse RejectMessage(QueueMessage r)
        {
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.RejectMessage,
                VisibilitySeconds = _visibilitySeconds,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = r,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;
        }
        public StreamQueueMessagesResponse ModifyVisibility(QueueMessage r, int visibility)
        {
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ModifyVisibility,
                VisibilitySeconds = visibility,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = r,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;

        }
        public StreamQueueMessagesResponse ResendMessage(QueueMessage r)
        {


            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ResendMessage,
                VisibilitySeconds = _visibilitySeconds,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = r,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;
        }
        public StreamQueueMessagesResponse ModifiedMessage(QueueMessage r)
        {

            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = queue.ClientID,
                Channel = queue.QueueName,
                RequestID = ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.SendModifiedMessage,
                VisibilitySeconds = _visibilitySeconds,
                WaitTimeSeconds = queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = r,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return streamQueueMessagesResponse.Result;
        }


        private async Task<StreamQueueMessagesResponse> StreamQueueMessage(StreamQueueMessagesRequest sr)
        {


            // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'
            try
            {
                // Send Event via GRPC RequestStream
                await stream.RequestStream.WriteAsync(sr);
                await stream.ResponseStream.MoveNext(CancellationToken.None);
              
                return stream.ResponseStream.Current;

                
            }
            catch (RpcException ex)
            {
                // logger.LogError(ex, "RPC Exception in StreamEvent");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                // logger.LogError(ex, "Exception in StreamEvent");

                throw new Exception(ex.Message);
            }
        }

    }

    public class ReqID
    {

        static int _id;
        public static string Getid()
        {

            //return Interlocked.Increment(ref _id);

            int temp, temp2;

            do
            {
                temp = _id;
                temp2 = temp == ushort.MaxValue ? 1 : temp + 1;
            }
            while (Interlocked.CompareExchange(ref _id, temp2, temp) != temp);

            return _id.ToString();
        }
       
    }
}