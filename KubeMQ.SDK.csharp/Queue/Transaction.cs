using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;


namespace KubeMQ.SDK.csharp.Queue.Stream
{
    /// <summary>
    /// Advance manipulation of messages using stream
    /// </summary>
    public class Transaction : GrpcClient
    {
        private readonly Queue _queue;

        private AsyncDuplexStreamingCall<StreamQueueMessagesRequest, StreamQueueMessagesResponse> _stream;

        private bool _transactionState = false;
        private CancellationTokenSource _cts;
        /// <summary>
        /// Status of current message handled, when false there is no active message to resend, call Receive first
        /// </summary>
        public bool InTransaction
        {
            get { return CheckCallIsInTransaction(); }
        }

        internal Transaction(Queue queue )
        {
            this._queue = queue;
            _kubemqAddress = queue.ServerAddress;  
            this._metadata =   queue.Metadata;
            
        }

        /// <summary>
        /// Receive queue messages request , waiting for response or timeout.
        /// </summary>
        /// <param name="visibilitySeconds">message access lock by receiver.</param>
        /// <param name="waitTimeSeconds">Wait time of request., default is from queue</param>
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
                RequestID = Tools.IDGenerator.Getid(),
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
              
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message);
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
            if(!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to ack, call Receive first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.AckMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
                return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
            }
            catch (Exception ex)
            {
                
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message);
            }
            //stream = null;
           
        }
        /// <summary>
        /// Will return message to queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public TransactionMessagesResponse RejectMessage(ulong msgSequence)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to reject, call Receive first");
            }
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.RejectMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
            });
            try
            {
                streamQueueMessagesResponse.Wait();
                return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message);
               
            }           
           
        }
        /// <summary>
        /// Extend the visibility time for the current receive message
        /// </summary>        
        /// <param name="visibilityinSeconds">new viability time</param>
        /// <returns></returns>
        public TransactionMessagesResponse ExtendVisibility(int visibilityinSeconds)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to extend visibility, call Next first");
            }
            
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.ModifyVisibility,
                VisibilitySeconds = visibilityinSeconds,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
                return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message);
            }
            
        }
        /// <summary>
        /// Resend the current received message to a new channel and ack the current message
        /// </summary>
        /// <param name="queueName">Resend queue name</param>
        /// <returns></returns>
        public TransactionMessagesResponse Resend(string queueName)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }         
            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = queueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.ResendMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage =null,
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
                return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message);
            }
     
           
        }
        /// <summary>
        /// Resend the new message to a new channel.
        /// </summary>
        /// <param name="msg">New Message</param>
        /// <returns></returns>
        public TransactionMessagesResponse Modify(Message msg)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }

            msg.ClientID = _queue.ClientID;
            msg.MessageID = Tools.IDGenerator.Getid();
            msg.Queue = msg.Queue ?? _queue.QueueName;
            msg.Metadata = msg.Metadata ?? "";       

            Task<StreamQueueMessagesResponse> streamQueueMessagesResponse = StreamQueueMessage(new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = "",
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.SendModifiedMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(msg),
                RefSequence = 0
            });
            try
            {
                streamQueueMessagesResponse.Wait();
                return new TransactionMessagesResponse(streamQueueMessagesResponse.Result);
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(RpcException))
                {
                    throw ex.InnerException;
                }
                return new TransactionMessagesResponse(ex.Message, msg);
            }
            
        }
        /// <summary>
        /// End the current stream of queue messages
        /// </summary>
        public void Close()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }

            _transactionState = false;
        }

        private bool OpenStream()
        { 
            if (!CheckCallIsInTransaction())
            {
                _cts = new CancellationTokenSource();
                _stream = GetKubeMQClient().StreamQueueMessage(Metadata,null, _cts.Token);
                _transactionState = true;
                return true;
            }
            else
            {
                return false;
            }         
          
        }

        private bool CheckCallIsInTransaction()
        {
            if (!_transactionState)
            {
                return false;
            }
            try
            {
                if (_stream == null)
                {
                    return false;
                }

                if (_stream.GetStatus().StatusCode == StatusCode.OK)
                {
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                if (ex.Message == "Status can only be accessed once the call has finished.")
                {
                    return true;
                }
                else
                    throw ex;
               
            }
        }

        private async Task<StreamQueueMessagesResponse> StreamQueueMessage(StreamQueueMessagesRequest sr)
        {
            if (_stream == null )
            {
                throw new RpcException(new Status(StatusCode.NotFound, "stream is null"), "Transaction stream is not opened, please Receive new Message");
            }

            // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'
            try
            {
                // Send Event via GRPC RequestStream
                await _stream.RequestStream.WriteAsync(sr);
                await _stream.ResponseStream.MoveNext(CancellationToken.None);
            
                return _stream.ResponseStream.Current;
                
            }
            catch (RpcException ex)
            {               
                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {               
                throw new Exception(ex.Message);
            }
        }

    }
}