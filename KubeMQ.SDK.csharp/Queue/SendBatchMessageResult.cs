using System.Collections.Generic;
using Google.Protobuf.Collections;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    /// <summary>
    ///  Queue request batch execution result.
    /// </summary>
    public class SendBatchMessageResult
    {
        /// <summary>
        ///  Unique for Request
        /// </summary>
        public string BatchID { get; }
        /// <summary>
        /// Returned if one or more messages process has error, false if no error.
        /// </summary>
        public bool HaveErrors { get; }
        /// <summary>
        /// Collection
        /// </summary>
        public IEnumerable<SendMessageResult> Results { get; }

        internal SendBatchMessageResult(QueueMessagesBatchResponse queueMessagesBatchResponse)
        {
            this.BatchID = queueMessagesBatchResponse.BatchID;
            this.HaveErrors = queueMessagesBatchResponse.HaveErrors;
            this.Results = ConvertToSendMessageResult(queueMessagesBatchResponse.Results);
        }


        private IEnumerable<SendMessageResult> ConvertToSendMessageResult(RepeatedField<SendQueueMessageResult> results)
        {
            foreach (var item in results)
            {
                yield return new SendMessageResult(item);
            }
        }
    }
}