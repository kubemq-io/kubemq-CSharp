using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;


namespace KubeMQ.SDK.csharp.Queue.Stream
{
    public class Transaction : GrpcClient
    {
        private Queue _queue;
       
        private int _visibilitySeconds;
        private AsyncDuplexStreamingCall<StreamQueueMessagesRequest, StreamQueueMessagesResponse> stream;

        public string Status
        {
            get
            {
                return stream == null ? "stream is null" : stream.GetStatus().Detail;
            }
        }
        public int VisibilitySeconds { get;  private set; }

        public Transaction(Queue queue, int visibilitySeconds = 1)
        {
            this._queue = queue;
            _kubemqAddress = queue.ServerAddress;
            VisibilitySeconds = visibilitySeconds;
        }

        public TransactionMessagesResponse Receive()
        {
                stream = GetKubeMQClient().StreamQueueMessage();              
        
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,     
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ReceiveMessage,
                VisibilitySeconds = VisibilitySeconds,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
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
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }        
        public TransactionMessagesResponse AckMessage(Message r)
        {
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.AckMessage,
                VisibilitySeconds = VisibilitySeconds,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = new QueueMessage(),
                RefSequence = r.Attributes.Sequence
            }) ;
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }
        public TransactionMessagesResponse RejectMessage(Message r)
        {             
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.RejectMessage,
                VisibilitySeconds = VisibilitySeconds,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(r),
                RefSequence = r.Attributes.Sequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }

        public TransactionMessagesResponse ExtendVisibility(Message r, int visibility)
        {
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ModifyVisibility,
                VisibilitySeconds = visibility,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(r),
                RefSequence = r.Attributes.Sequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }
        public TransactionMessagesResponse Resend(Message r)
        {
          
                Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ResendMessage,
                VisibilitySeconds = VisibilitySeconds,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(r),
                RefSequence = r.Attributes.Sequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }
        public TransactionMessagesResponse Modifiy(Message r)
        {

            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.SendModifiedMessage,
                VisibilitySeconds = VisibilitySeconds,
                WaitTimeSeconds = _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(r),
                RefSequence = r.Attributes.Sequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }

        public bool OpenStream()
        {
            if (stream == null)
            {
                stream = GetKubeMQClient().StreamQueueMessage();
            }
            else
            {
                try
                {
                    if (stream.GetStatus().StatusCode != StatusCode.OK)
                    {
                        stream = GetKubeMQClient().StreamQueueMessage();
                    }
                  
                }
                catch (Exception ex)
                {
                    
                }
            }

          var res =  GetKubeMQClient().Ping(new Empty());
            return true;
        }

        private async Task<StreamQueueMessagesResponse> StreamQueueMessage(StreamQueueMessagesRequest sr)
        {

            if (stream == null )
            {
                throw new RpcException(new Status(StatusCode.NotFound, "stream is null"), "Transaction stream is not opened, please Receive new Message");
            }
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
}