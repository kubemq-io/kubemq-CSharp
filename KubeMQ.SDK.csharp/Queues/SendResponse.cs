using System.Collections.Generic;
using System.Threading.Tasks;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Queues
{
    public class SendResponse
    {
        private List<SendMessageResult> _results;
        private string _error = null;
        private SendRequest _request;
        private TaskCompletionSource<bool> _waitForResponseTask = new TaskCompletionSource<bool>();
        
        public List<SendMessageResult> Results => _results;
        public string Error => _error;
        internal SendRequest Request
        {
            get => _request;
            set => _request = value;
        }
        internal TaskCompletionSource<bool> WaitForResponseTask
        {
            get => _waitForResponseTask;
            set => _waitForResponseTask = value;
        }

        internal string RequestId
        {
            get => _request.RequestId;
        }
        internal SendResponse( SendRequest request)
        {
            _request = request;
            _results = new List<SendMessageResult>() ;
        }
        internal SendResponse setSendResponse(QueuesUpstreamResponse response)
        {
           
            foreach (var result in response.Results)
            {
                _results.Add(new SendMessageResult(result));
            }

            if (response.IsError)
            {
                _error = response.Error;
            }
            _waitForResponseTask.TrySetResult(true);
            return this;
        }
    }
}