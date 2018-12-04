using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Events
{
    public class Result
    {
        public string EventID { get; set; }
        public bool Sent { get; set; }
        public string Error { get; set; }

        public Result() { }

        public Result(KubeMQGrpc.Result innerResult)
        {
            EventID = innerResult.EventID;
            Sent = innerResult.Sent;
            Error = innerResult.Error;
        }
    }
}
