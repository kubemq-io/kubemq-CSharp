using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Basic;

namespace KubeMQ.SDK.csharp.QueueStream
{
    public class QueueStream : GrpcClient
    {
        private string _clientId = Guid.NewGuid().ToString();
        private kubemq.kubemqClient _client ;
        private Downstream _downstream = null;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken _downstreamToken;
        public QueueStream(string address) :this (address,null,null)
        {
        }
        public QueueStream(string address,string clientId) :this (address,clientId,null)
        {
        }
        public QueueStream(string address,string clientId, string authToken) 
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                _clientId = clientId;
            }

            if (!string.IsNullOrEmpty(address))
            {
                _kubemqAddress = address;
            }
            this.addAuthToken(authToken);
            _client = GetKubeMQClient();
           
        }

        private async Task StartDownstream()
        {
            _downstreamToken = _tokenSource.Token;
            _downstream = new Downstream(_client, null,_clientId);
            _downstream.Connect();
            await _downstream.WaitForConnectionTask.Task;
        }
        
        public async Task<PollResponse> Poll(PollRequest request)
        {
            if (_downstream == null)
            {
                await StartDownstream();
            }
            
            return await _downstream.Poll(request, _clientId);
        }
        public void Close()
        {
            _downstream.ClearResponses();
            _tokenSource.Cancel();
        }
        public PingResult Ping()
        {
           return  _client.Ping(new Empty());
        }
    }
}