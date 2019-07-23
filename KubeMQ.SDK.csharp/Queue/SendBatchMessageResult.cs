using System.Collections.Generic;
using Google.Protobuf.Collections;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queue
{
    public class SendBatchMessageResult
    {
        public string BatchID { get; }
        public bool HaveErrors { get; }
        public IEnumerable<SendMessageResult> Results { get; }

        public SendBatchMessageResult(QueueMessagesBatchResponse queueMessagesBatchResponse)
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