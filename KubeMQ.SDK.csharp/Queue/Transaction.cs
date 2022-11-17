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

        internal Transaction(Queue queue)
        {
            _queue = queue;
            _kubemqAddress = queue.ServerAddress;
            _metadata = queue.Metadata;
        }

        /// <summary>
        /// Receive queue messages request , waiting for response or timeout.
        /// </summary>
        /// <param name="visibilitySeconds">message access lock by receiver.</param>
        /// <param name="waitTimeSeconds">Wait time of request., default is from queue</param>
        /// <returns></returns>
        public TransactionMessagesResponse Receive(int visibilitySeconds = 1, int? waitTimeSeconds = null)
        {
            return ReceiveAsync(visibilitySeconds, waitTimeSeconds, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Receive queue messages request , waiting for response or timeout.
        /// </summary>
        /// <param name="visibilitySeconds">message access lock by receiver.</param>
        /// <param name="waitTimeSeconds">Wait time of request., default is from queue</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> ReceiveAsync(int visibilitySeconds = 1, int? waitTimeSeconds = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!OpenStream())
            {
                return new TransactionMessagesResponse("active queue message wait for ack/reject");
            }

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.ReceiveMessage,
                VisibilitySeconds = visibilitySeconds,
                WaitTimeSeconds = waitTimeSeconds ?? _queue.WaitTimeSecondsQueueMessages,
                ModifiedMessage = new QueueMessage(),
                RefSequence = 0
            };

            try
            {
                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new TransactionMessagesResponse(ex.Message);
            }
        }

        /// <summary>
        /// Will mark Message dequeued on queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public TransactionMessagesResponse AckMessage(ulong msgSequence)
        {
            return AckMessageAsync(msgSequence, CancellationToken.None).GetAwaiter().GetResult();
        }


        /// <summary>
        /// Will mark Message dequeued on queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> AckMessageAsync(ulong msgSequence, CancellationToken cancellationToken)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to ack, call Receive first");
            }

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.AckMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
            };

            try
            {
                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new TransactionMessagesResponse(ex.Message);
            }
        }

        /// <summary>
        /// Will return message to queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public TransactionMessagesResponse RejectMessage(ulong msgSequence)
        {
            return RejectMessageAsync(msgSequence, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Will return message to queue.
        /// </summary>
        /// <param name="msgSequence">Received message sequence Attributes.Sequence</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> RejectMessageAsync(ulong msgSequence, CancellationToken cancellationToken)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to reject, call Receive first");
            }

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.RejectMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = msgSequence
            };

            try
            {
                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
            return ExtendVisibilityAsync(visibilityinSeconds, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Extend the visibility time for the current receive message
        /// </summary>        
        /// <param name="visibilityinSeconds">new viability time</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> ExtendVisibilityAsync(int visibilityinSeconds, CancellationToken cancellationToken)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to extend visibility, call Next first");
            }

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = _queue.QueueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.ModifyVisibility,
                VisibilitySeconds = visibilityinSeconds,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = 0
            };

            try
            {
                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
            return ResendAsync(queueName, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resend the current received message to a new channel and ack the current message
        /// </summary>
        /// <param name="queueName">Resend queue name</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> ResendAsync(string queueName, CancellationToken cancellationToken)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = queueName,
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.ResendMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = null,
                RefSequence = 0
            };

            try
            {

                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new TransactionMessagesResponse(ex.Message);
            }
        }

        /// <summary>
        /// Resend the new message to a new channel.
        /// </summary>
        /// <param name="message">New Message</param>
        /// <returns></returns>
        public TransactionMessagesResponse Modify(Message message)
        {
            return ModifyAsync(message, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resend the new message to a new channel.
        /// </summary>
        /// <param name="message">New Message</param>
        /// <returns></returns>
        public async Task<TransactionMessagesResponse> ModifyAsync(Message message, CancellationToken cancellationToken)
        {
            if (!CheckCallIsInTransaction())
            {
                return new TransactionMessagesResponse("no active message to resend, call Receive first");
            }

            message.ClientID = _queue.ClientID;
            message.MessageID = Tools.IDGenerator.Getid();
            message.Queue = message.Queue ?? _queue.QueueName;
            message.Metadata = message.Metadata ?? "";

            var request = new StreamQueueMessagesRequest
            {
                ClientID = _queue.ClientID,
                Channel = "",
                RequestID = Tools.IDGenerator.Getid(),
                StreamRequestTypeData = StreamRequestType.SendModifiedMessage,
                VisibilitySeconds = 0,
                WaitTimeSeconds = 0,
                ModifiedMessage = Tools.Converter.ConvertQueueMessage(message),
                RefSequence = 0
            };

            try
            {
                var streamQueueMessagesResponse = await StreamQueueMessage(request, cancellationToken);
                return new TransactionMessagesResponse(streamQueueMessagesResponse);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new TransactionMessagesResponse(ex.Message);
            }

        }

        /// <summary>
        /// End the current stream of queue messages
        /// </summary>
        public void Close()
        {
            _cts?.Cancel();
            _transactionState = false;
        }

        private bool OpenStream()
        {
            if (CheckCallIsInTransaction())
            {
                return false;
            }
            else
            {
                _cts = new CancellationTokenSource();
                _stream = GetKubeMQClient().StreamQueueMessage(Metadata, null, _cts.Token);
                _transactionState = true;
                return true;
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
                _stream?.GetStatus();
                return false;
            }
            catch (Exception ex)
            {
                if (ex.Message == "Status can only be accessed once the call has finished.")
                    return true;
                else
                    throw;
            }
        }

        private async Task<StreamQueueMessagesResponse> StreamQueueMessage(StreamQueueMessagesRequest sr, CancellationToken cancellationToken)
        {
            if (_stream == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "stream is null"), "Transaction stream is not opened, please Receive new Message");
            }

            // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'

            // Send Event via GRPC RequestStream
            await _stream.RequestStream.WriteAsync(sr);
            await _stream.ResponseStream.MoveNext(cancellationToken);

            return _stream.ResponseStream.Current;
        }

    }
}