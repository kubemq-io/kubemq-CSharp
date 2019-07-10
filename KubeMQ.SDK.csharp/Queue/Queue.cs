using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static KubeMQ.Grpc.kubemq;
using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class QueueMessageConstans
    {
        public string QueueName { get; set; }
        public string ClientID { get; set; }
    }

    public class Message
    {
        private static int _id = 0;
        public string MessageID { get; set; }
        public string Metadata { get; set; }
        public IEnumerable<(string, string)> Tags { get; set; }
        public byte[] Body { get; set; }

        public Message()
        {

        }
        public Message(byte[] body, string metadata, string messageID = null, IEnumerable<(string, string)> tags = null)
        {
            MessageID = string.IsNullOrEmpty(messageID) ? GetNextId().ToString() : messageID;
            Metadata = metadata;
            Tags = tags;
            Body = body;
        }

        private int GetNextId()
        {
            //return Interlocked.Increment(ref _id);

            int temp, temp2;

            do
            {
                temp = _id;
                temp2 = temp == ushort.MaxValue ? 1 : temp + 1;
            }
            while (Interlocked.CompareExchange(ref _id, temp2, temp) != temp);
            return _id;
        }

    }
    public class Queue:  GrpcClient
    {
        private static int _id = 0;
        public string QueueName { get; set; }
        public string ClientID { get; set; }
        public int MaxNumberOfMessagesQueueMessages { get; private set; }
        public int WaitTimeSecondsQueueMessages { get; private set; }

        public Queue()
        {
            
        }
        public Queue(string queueName, string clientID)
        {
            QueueName = queueName;
            ClientID = clientID;
        }
        public Queue(QueueMessageConstans constans):this (constans.QueueName, constans.ClientID)
        {
          
        }

        public SendQueueMessageResult SendQueueMessage(Message message)
        {
            
            SendQueueMessageResult rec = GetKubeMQClient().SendQueueMessage(new KubeMQGrpc.QueueMessage
            {

                MessageID= message.MessageID,
                Metadata = message.Metadata,
                ClientID = ClientID,
                QueueName = QueueName,
                Tags = { convertTags(message.Tags) },
                Body = ByteString.CopyFrom(message.Body)
            });
            
          
            return rec;
        }

        private MapField<string, string> convertTags(IEnumerable<(string, string)> tags)
        {
            MapField<string, string> keyValuePairs = new MapField<string, string>();
            if (tags!=null)
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
                BatchID = string.IsNullOrEmpty(batchID) ? GetNextId().ToString() : batchID,
                Messages = { convertMesages(queueMessages) }
            });

            return rec;
        }

        private int GetNextId()
        {
            //return Interlocked.Increment(ref _id);

            int temp, temp2;

            do
            {
                temp = _id;
                temp2 = temp == ushort.MaxValue ? 1 : temp + 1;
            }
            while (Interlocked.CompareExchange(ref _id, temp2, temp) != temp);
            return _id;
        }

        private RepeatedField<QueueMessage> convertMesages(IEnumerable<Message> queueMessages)
        {
            RepeatedField<QueueMessage> testc = new RepeatedField<QueueMessage>();
            foreach (var item in queueMessages)
            {
                testc.Add(new QueueMessage
                {
                    ClientID = ClientID,
                    QueueName = QueueName,
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
                RequestID = GetNextId().ToString(),
                ClientID = ClientID,
                QueueName = QueueName,

                MaxNumberOfMessages = MaxNumberOfMessagesQueueMessages,
                WaitTimeSeconds = WaitTimeSecondsQueueMessages
            });

            return rec;
        }
        public PeakQueueMessageResponse PeakQueueMessage()
        {
            PeakQueueMessageResponse rec = GetKubeMQClient().PeakQueueMessage(new PeakQueueMessageRequest
            {
               RequestID = GetNextId().ToString(),
               QueueName = QueueName
            });

            return rec;
        }

        public AckAllQueueMessagesResponse ackAllQueueMessagesResponse()
        {
            AckAllQueueMessagesResponse rec = GetKubeMQClient().AckAllQueueMessages(new AckAllQueueMessagesRequest
            {
                RequestID = GetNextId().ToString(),
                QueueName = QueueName,              
                WaitTimeout = WaitTimeSecondsQueueMessages
            });
            return rec;
        }

        #region "Transactional"
        //public void AckMessage(StreamQueueMessagesResponse streamQueueMessagesResponse)
        //{
           

        //    // Send Event via GRPC RequestStream
        //     x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());

        //    }
        //public void RejectMessage()
        //{
        //    kubemqClient x = new kubemqClient();

        //    // Send Event via GRPC RequestStream
        //    x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());
        //}
        //public void ModifyVisibility()
        //{
        //    kubemqClient x = new kubemqClient();

        //    // Send Event via GRPC RequestStream
        //    x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());
        //}
        //public void ResendMessage()
        //{
        //    kubemqClient x = new kubemqClient();

        //    // Send Event via GRPC RequestStream
        //    x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());
        //}
        //public void ModifiedMessage()
        //{
        //    kubemqClient x = new kubemqClient();

        //    // Send Event via GRPC RequestStream
        //    x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());
        //}

        //public StreamQueueMessagesResponse ReceiveQueueTannMessage()
        //{
        //    kubemqClient x = new kubemqClient();
        //    x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest()
        //    {

        //        ClientID = ClientID,
        //        QueueName= QueueName
        //    }                
        //    );
        //    using (var call = x.StreamQueueMessage())
        //    {
        //        // Wait for Response..
        //         call.ResponseStream.MoveNext(CancellationToken.None);

        //       return call.ResponseStream.Current;
        //    }
        //}



        //public async Task StreamQueueMessage()
        //{
        //    kubemqClient x = new kubemqClient();

        //    // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'
        //    try
        //    {
           
        //        // Send Event via GRPC RequestStream
        //        await x.StreamQueueMessage().RequestStream.WriteAsync(new StreamQueueMessagesRequest());

        //        // Listen for Async Response (Result)
        //        using (var call = x.StreamQueueMessage())
        //        {
        //            // Wait for Response..
        //            await call.ResponseStream.MoveNext(CancellationToken.None);

        //            // Received a Response
        //            StreamQueueMessagesResponse response = call.ResponseStream.Current;
        //        }
        //    }
        //    catch (RpcException ex)
        //    {
        //       // logger.LogError(ex, "RPC Exception in StreamEvent");

        //        throw new RpcException(ex.Status);
        //    }
        //    catch (Exception ex)
        //    {
        //       // logger.LogError(ex, "Exception in StreamEvent");

        //        throw new Exception(ex.Message);
        //    }
        //}

        #endregion

        public PingResult Ping()
        {
            PingResult rec = GetKubeMQClient().Ping(null);
            return rec;
                
        }


       
    }
}
