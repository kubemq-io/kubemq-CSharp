using System.Collections.Generic;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.QueueStream
{
    /// <summary>
    /// Queues Info.
    /// </summary>
    public class QueuesInfo
    {
        /// <summary>
        /// Total number of Queues
        /// </summary>
        public int TotalQueues { get; }
        
        /// <summary>
        /// Total number of messages were sent
        /// </summary>
        public long Sent { get; }
       
        /// <summary>
        /// Total number of messages waiting to be delivered
        /// </summary>
        public long Waiting { get; }
        
        /// <summary>
        /// Total number of messages were delivered
        /// </summary>
        public long Delivered { get; }
        
        /// <summary>
        /// Queues Info list
        /// </summary>
        public List<QueueInfo> Queues { get; }
        
        internal QueuesInfo(QueuesInfoResponse queuesInfoResponse)
        {
            this.Sent = queuesInfoResponse.Info.Sent;
            this.Waiting = queuesInfoResponse.Info.Waiting;
            this.Delivered = queuesInfoResponse.Info.Delivered;
            this.TotalQueues = queuesInfoResponse.Info.TotalQueue;
            this.Queues = new List<QueueInfo>();
            foreach (var infoQueue in queuesInfoResponse.Info.Queues)
            {
                this.Queues.Add(new QueueInfo(infoQueue));
            }
        }        
    }
}

public class QueueInfo
{
    /// <summary>
    /// Name of Queue
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Total number of bytes sent on queue
    /// </summary>
    public long Bytes { get; }
    
    /// <summary>
    /// Queue first sequence id
    /// </summary>
    public long FirstSequence { get; }
    
    /// <summary>
    /// Queue last sequence id was deliverd
    /// </summary>
    public long LastSequence { get; }  
    
    /// <summary>
    /// Queue number of messages were sent
    /// </summary>
    public long Sent { get; }
       
    /// <summary>
    /// Queue number of messages waiting to be delivered
    /// </summary>
    public long Waiting { get; }
        
    /// <summary>
    /// Queue number of messages were delivered
    /// </summary>
    public long Delivered { get; }
        
    internal QueueInfo(KubeMQ.Grpc.QueueInfo queueInfo)
    {
        this.Name = queueInfo.Name;
        this.Bytes = queueInfo.Bytes;
        this.FirstSequence = queueInfo.FirstSequence;
        this.LastSequence = queueInfo.LastSequence;
        this.Sent = queueInfo.Sent;
        this.Waiting = queueInfo.Waiting;
        this.Delivered = queueInfo.Delivered;
    }        
}