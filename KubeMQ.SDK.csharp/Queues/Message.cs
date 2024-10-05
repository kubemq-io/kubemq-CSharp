using System;
using KubeMQ.Grpc;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static System.Guid;
using Google.Protobuf;
using System.Threading;
using System.Linq;

namespace KubeMQ.SDK.csharp.Queues
{
    /// <summary>
    /// Queue stored message
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        public string MessageID
        {
            get => string.IsNullOrEmpty(_messageID) ? Tools.IDGenerator.Getid() : _messageID;
            set => _messageID = value;
        }

        /// <summary>
        /// Represents the sender ID that the messages will be sent under.
        /// </summary>
        public string ClientID { get; set; }

        /// <summary>
        /// Represents the FIFO queue name to send to using the KubeMQ.
        /// </summary>
        public string Queue { get; set; }

        /// <summary>
        /// General information about the message body.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// The information that you want to pass.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Dictionary of string , string pair:A set of Key value pair that help categorize the message.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }

        /// <summary>
        /// Information of received message
        /// </summary>
        public QueueMessageAttributes Attributes { get; }

        /// <summary>
        /// Information of received message
        /// </summary>
        public QueueMessagePolicy Policy { get; set; }

        private string _messageID;
        private string _transactionId = "";
        private DownstreamRequestHandler _requestHandler;

        // Added fields for additional functionalities
        private TimeSpan _visibilityDuration;
        private Timer _visibilityTimer;
        private bool _isCompleted;
        private string _completeReason;
        private readonly object _lock = new object();

        /// <summary>
        /// Queue stored message
        /// </summary>
        /// <param name="message"></param>
        internal Message(QueueMessage message, DownstreamRequestHandler requestHandler, string transactionId, int visibilitySeconds,bool isAutoAck)
        {
            _requestHandler = requestHandler;
            _transactionId = transactionId;
            this.MessageID = message.MessageID;
            this.ClientID = message.ClientID;
            this.Queue = message.Channel;
            this.Metadata = message.Metadata;
            this.Body = message.Body.ToByteArray();
            this.Tags = Tools.Converter.ReadTags(message.Tags);
            this.Attributes = message.Attributes;
            this.Policy = message.Policy;
            if (isAutoAck)
            {
                _isCompleted = true;
                _completeReason = "auto ack";
            }
            else
            {
                _isCompleted = false;
                _completeReason = "";
                _visibilityDuration = TimeSpan.FromSeconds(visibilitySeconds);
                StartVisibilityTimer();
            }
        }
        
        
        /// <summary>
        ///  Queue stored message
        /// </summary>
        public Message()
        {

        }

        /// <summary>
        /// Queue stored message
        /// </summary>
        /// <param name="queue">queue name</param>
        /// <param name="body">Message payload.</param>
        /// <param name="metadata">General information about the message body.</param>
        /// <param name="messageId">Unique for message</param>
        /// <param name="tags">Dictionary of string , string pair:A set of Key value pair that help categorize the message.</param>
        public Message(string queue, byte[] body, string metadata, string messageId = null, Dictionary<string, string> tags = null)
        {
            Queue = queue;
            MessageID = string.IsNullOrEmpty(messageId) ? Tools.IDGenerator.Getid() : messageId;
            Metadata = string.IsNullOrEmpty(metadata) ? "" : metadata;
            Tags = tags;
            Body = body;
        }

        private MapField<string, string> ToMapFields(Dictionary<string, string> tags)
        {
            MapField<string, string> keyValuePairs = new MapField<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Key, item.Value);
                }
            }
            return keyValuePairs;
        }

        internal QueueMessage ToQueueMessage(string clientId)
        {
            QueueMessage pbMessage = new QueueMessage();
            pbMessage.MessageID = string.IsNullOrEmpty(MessageID) ? NewGuid().ToString() : MessageID;
            pbMessage.Channel = Queue;
            pbMessage.ClientID = string.IsNullOrEmpty(ClientID) ? clientId : ClientID;
            pbMessage.Metadata = string.IsNullOrEmpty(Metadata) ? "" : Metadata;
            pbMessage.Body = Body == null ? ByteString.Empty : ByteString.CopyFrom(Body);
            pbMessage.Tags.Add(ToMapFields(Tags));
            pbMessage.Policy = Policy;
            return pbMessage;
        }

        private void CheckValidOperation()
        {
            if (_requestHandler == null)
            {
                throw new InvalidOperationException("This method is not valid in this context");
            }
        }

        /// <summary>
        /// Ack the current message (accept)
        /// </summary>
        public void Ack()
        {
            lock (_lock)
            {
                if (_isCompleted)
                {
                    throw new InvalidOperationException($"Message already completed, reason: {_completeReason}");
                }
                _isCompleted = true;
            }
            _completeReason = "Message Acked";
            StopVisibilityTimer();
            CheckValidOperation();
            QueuesDownstreamRequest ackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.AckRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            ackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(ackRequest);
        }

        
        
        /// <summary>
        /// NAck the current message (reject)
        /// </summary>
        [Obsolete("Use Reject instead")]
        public void NAck()
        {
            Reject();
        }

        /// <summary>
        /// Reject the current message
        /// </summary>
        public void Reject()
        {
            lock (_lock)
            {
                if (_isCompleted)
                {
                    throw new InvalidOperationException($"Message already completed, reason: {_completeReason}");
                }
                _isCompleted = true;
            }
            _completeReason = "Message rejected";
            StopVisibilityTimer();
            CheckValidOperation();
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            nackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(nackRequest);
        }
        /// <summary>
        /// Requeue the current message to a new queue
        /// </summary>
        /// <param name="queue">Requeue queue name</param>
        public void ReQueue(string queue)
        {
            lock (_lock)
            {
                if (_isCompleted)
                {
                    throw new InvalidOperationException($"Message already completed, reason: {_completeReason}");
                }
                _isCompleted = true;
            }
            _completeReason = "Message re-queued";
            StopVisibilityTimer();
            CheckValidOperation();
            QueuesDownstreamRequest requeueRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.ReQueueRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
                ReQueueChannel = queue
            };
            requeueRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(requeueRequest);
        }

        private void NackOnVisibility()
        {
            lock (_lock)
            {
                if (_isCompleted)
                {
                    // Can't throw an exception from a Timer callback
                    return;
                }
                _isCompleted = true;
            }
            _completeReason = "visibility timeout";
            StopVisibilityTimer();
            if (_requestHandler == null)
            {
                // Can't throw an exception from a Timer callback
                return;
            }
            // Send NAck
            QueuesDownstreamRequest nackRequest = new QueuesDownstreamRequest()
            {
                RequestTypeData = QueuesDownstreamRequestType.NackRange,
                RefTransactionId = _transactionId,
                Channel = this.Queue,
            };
            nackRequest.SequenceRange.Add(Convert.ToInt64(this.Attributes.Sequence));
            _requestHandler(nackRequest);
        }

        internal void StartVisibilityTimer()
        {
            lock (_lock)
            {
                if (_visibilityDuration == TimeSpan.Zero)
                {
                    return;
                }
                if (_visibilityTimer != null)
                {
                    _visibilityTimer.Dispose();
                }
                _visibilityTimer = new Timer(state =>
                {
                    NackOnVisibility();
                }, null, _visibilityDuration, Timeout.InfiniteTimeSpan);
            }
        }

        private void StopVisibilityTimer()
        {
            if (_visibilityDuration == TimeSpan.Zero)
            {
                return;
            }
            lock (_lock)
            {
                if (_visibilityTimer != null)
                {
                    _visibilityTimer.Dispose();
                    _visibilityTimer = null;
                }
            }
        }

        /// <summary>
        /// Extend the visibility timeout of the message
        /// </summary>
        /// <param name="visibilitySeconds">New visibility timeout in seconds</param>
        public void ExtendVisibility(int visibilitySeconds)
        {
            if (visibilitySeconds < 1)
            {
                throw new ArgumentException("Visibility seconds must be greater than 0");
            }
            lock (_lock)
            {
                if (_isCompleted)
                {
                    throw new InvalidOperationException("Message already completed, cannot extend visibility");
                }
                if (_visibilityDuration == TimeSpan.Zero)
                {
                    throw new InvalidOperationException("Visibility timer not set for this message");
                }
                if (_visibilityTimer != null)
                {
                    try
                    {
                        _visibilityTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        _visibilityDuration = TimeSpan.FromSeconds(visibilitySeconds);
                        _visibilityTimer.Change(_visibilityDuration, Timeout.InfiniteTimeSpan);
                    }
                    catch (ObjectDisposedException)
                    {
                        throw new InvalidOperationException("Visibility timer already expired");
                    }
                }
            }
        }

        
        public override string ToString()
        {
            return $"Id: {this.MessageID}, ClientId: {this.ClientID}, Channel: {this.Queue}, Metadata: {this.Metadata}, Body: {System.Text.Encoding.UTF8.GetString(this.Body)}, Tags: {TagsToString()}, Policy: {PolicyToString()}, Attributes: {AttributesToString()}";
        }

        private string TagsToString()
        {
            if (Tags == null || Tags.Count == 0)
            {
                return "";
            }
            return string.Join(", ", Tags.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        private string PolicyToString()
        {
            if (Policy == null)
            {
                return "";
            }
            return $"ExpirationSeconds: {Policy.ExpirationSeconds}, DelaySeconds: {Policy.DelaySeconds}, MaxReceiveCount: {Policy.MaxReceiveCount}, MaxReceiveQueue: {Policy.MaxReceiveQueue}";
        }

        private string AttributesToString()
        {
            if (Attributes == null)
            {
                return "";
            }
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(Attributes.Timestamp);
            DateTimeOffset expirationAt = DateTimeOffset.FromUnixTimeSeconds(Attributes.ExpirationAt);
            DateTimeOffset delayedTo = DateTimeOffset.FromUnixTimeSeconds(Attributes.DelayedTo);
            return $"Sequence: {Attributes.Sequence}, Timestamp: {timestamp}, ReceiveCount: {Attributes.ReceiveCount}, ReRouted: {Attributes.ReRouted}, ReRoutedFromQueue: {Attributes.ReRoutedFromQueue}, ExpirationAt: {expirationAt}, DelayedTo: {delayedTo}";
        }
        
        internal void SetComplete(string reason)
        {
            lock (_lock)
            {
                _isCompleted = true;    
            }
            _completeReason = reason;
            StopVisibilityTimer();
        }
    }
}
