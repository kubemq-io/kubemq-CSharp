using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Tools;

namespace KubeMQ.SDK.csharp.Common
{
    /// <summary>
    /// Represents the result of an asynchronous common operation.
    /// </summary>
    public class ListCqAsyncResult : BaseResult
    {
        public CQStats[] Channels { get;  }
        public ListCqAsyncResult(byte[] data, bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Channels = JsonConverter.FromByteArray<CQStats[]>(data);
        }
    }
    public class ListPubSubAsyncResult : BaseResult
    {
        public PubSubStats[] Channels { get;  }
        public ListPubSubAsyncResult(byte[] data, bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Channels = JsonConverter.FromByteArray<PubSubStats[]>(data);
        }
    }
    
    public class ListQueuesAsyncResult : BaseResult
    {
        public QueuesChannel[] Channels { get;  }
        public ListQueuesAsyncResult(byte[] data, bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Channels = JsonConverter.FromByteArray<QueuesChannel[]>(data);
        }
    }
}