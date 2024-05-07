using System;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Tools;

namespace KubeMQ.SDK.csharp.Results
{
    /// <summary>
    /// Represents the result of an asynchronous common operation.
    /// </summary>
    public class ListCqAsyncResult : Result
    {
        public CQChannel[] Channels { get;  }
        public ListCqAsyncResult(byte[] data)
        {
            IsSuccess = true;
            Channels = JsonConverter.FromByteArray<CQChannel[]>(data);
        }
        
        public ListCqAsyncResult(string errorMessage) : base(errorMessage)
        {
        }
        
        public ListCqAsyncResult(Exception e) : base(e)
        {
        }
    }
    public class ListPubSubAsyncResult : Result
    {
        public PubSubChannel[] Channels { get;  }
        public ListPubSubAsyncResult(byte[] data)
        {
            IsSuccess = true;
            Channels = JsonConverter.FromByteArray<PubSubChannel[]>(data);
        }
        
        public ListPubSubAsyncResult(string errorMessage) : base(errorMessage)
        {
        }
        
        public ListPubSubAsyncResult(Exception e) : base(e)
        {
        }
    }
    
    public class ListQueuesAsyncResult : Result
    {
        public QueuesChannel[] Channels { get;  }
        public ListQueuesAsyncResult(byte[] data)
        {
            IsSuccess = true;
            Channels = JsonConverter.FromByteArray<QueuesChannel[]>(data);
        }
        
        public ListQueuesAsyncResult(string errorMessage) : base(errorMessage)
        {
        }
        
        public ListQueuesAsyncResult(Exception e) : base(e)
        {
        }
    }
}