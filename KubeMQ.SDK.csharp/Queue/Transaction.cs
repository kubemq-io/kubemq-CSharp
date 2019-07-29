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
        public bool InTransaction
        {
            get { return !CheckCallStatus(); }
        }

        CancellationTokenSource cts;

        internal Transaction(Queue queue )
        {
            this._queue = queue;
            _kubemqAddress = queue.ServerAddress;  
            
        }

        /// <summary>
        /// Receive queue messages request , waiting for response or timeout.
        /// </summary>
        /// <param name="visibilitySeconds">message access lock by receiver.</param>
        /// <param name="waitTimeSeconds">Wait time of request.</param>
        /// <returns></returns>
        public TransactionMessagesResponse Receive(int visibilitySeconds = 1, int? waitTimeSeconds=null)
        {
          if( !OpenStream())
            {
                return new TransactionMessagesResponse("active queue message wait for ack/reject");
            }
        
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,     
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ReceiveMessage,
                VisibilitySeconds = visibilitySeconds,
                WaitTimeSeconds = waitTimeSeconds??_queue.WaitTimeSecondsQueueMessages,
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

        /// <summary>
        /// Will mark Message dequeued on queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public TransactionMessagesResponse AckMessage(ulong msgSequence)
        {        
            if(CheckCallStatus())
            {
                return new TransactionMessagesResponse("no active message to ack, call Receive first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.AckMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
            }); ;
            try
            {
                streamQueueMessagesResponse.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            //stream = null;
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }
        /// <summary>
        /// Will return message to queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public TransactionMessagesResponse RejectMessage(ulong msgSequence)
        {
            if (CheckCallStatus())
            {
                return new TransactionMessagesResponse("no active message to reject, call Receive first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.RejectMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
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
        /// <summary>
        /// Extend the visibility time for the current receive message
        /// </summary>        
        /// <param name="visibilityinSeconds">new viability time</param>
        /// <returns></returns>
        public TransactionMessagesResponse ExtendVisibility(int visibilityinSeconds)
        {
            if (CheckCallStatus())
            {
                return new TransactionMessagesResponse("no active message to extend visibility, call Next first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ModifyVisibility,
                VisibilitySeconds = visibilityinSeconds,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
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

        /// <summary>
        /// Resend the current received message to a new channel and ack the current message
        /// </summary>
        /// <param name="queueName">Resend queue name</param>
        /// <returns></returns>
        public TransactionMessagesResponse Resend(string queueName)
        {
            if (CheckCallStatus())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = queueName,
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.ResendMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage =null,
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
            stream = null;
            return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
        }

        /// <summary>
        /// Resend the new message to a new channel.
        /// </summary>
        /// <param name="msg">New Message</param>
        /// <returns></returns>
        public TransactionMessagesResponse Modifiy(Message msg)
        {
            if (CheckCallStatus())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }

            msg.ClientID = _queue.ClientID;
            msg.MessageID = Tools.IDGenerator.ReqID.Getid();
            msg.Queue = msg.Queue ?? _queue.QueueName;
            msg.Metadata = msg.Metadata ?? "";


            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = "",
                RequestID = Tools.IDGenerator.ReqID.Getid(),
                StreamRequestTypeData = StreamRequestType.SendModifiedMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(msg),
                RefSequence = 0
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


        /// <summary>
        /// End the current stream of queue messages
        /// </summary>
        public void Close()
        {
            cts.Cancel();          
        }

        private bool OpenStream()
        {
            
            if (stream == null)
            {
                cts = new CancellationTokenSource();
                stream = GetKubeMQClient().StreamQueueMessage(null,null,cts.Token);
            }
            else
            {

                if (CheckCallStatus())
                {
                    cts = new CancellationTokenSource();
                    stream = GetKubeMQClient().StreamQueueMessage(null, null, cts.Token);
                }
                else
                {
                    return false;
                }
                
              }

          var res =  GetKubeMQClient().Ping(new Empty());
          return true;
        }

        internal bool CheckCallStatus()
        {
            try
            {
                if (stream.GetStatus().StatusCode == StatusCode.OK)
                {
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message == "Status can only be accessed once the call has finished.")
                {
                    return false;
                }
                else
                    throw ex;

               
            }
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