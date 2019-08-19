using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Events
{
    public class Result
    {
        /// <summary>
        /// Represent integer that help identify the message.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Boolean ,Represent if the event was sent successfully to the kubemq. 
        /// </summary>
        public bool Sent { get; set; }
        /// <summary>
        /// string , will show error message.
        /// </summary>
        public string Error { get; set; }

        public Result() { }

        /// <summary>
        /// Represent Result of the event sent request.
        /// </summary>
        public Result(KubeMQGrpc.Result innerResult)
        {
            EventID = innerResult.EventID;
            Sent = innerResult.Sent;
            Error = innerResult.Error;
        }
    }
}
