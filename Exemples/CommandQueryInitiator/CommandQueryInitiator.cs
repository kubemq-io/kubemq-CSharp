using System;
using CommonExample;
using KubeMQ.SDK.csharp.CommandQuery;
using KubeMQ.SDK.csharp.CommandQuery.LowLevel;

namespace CommandQueryInitiator
{
    public class CommandQueryInitiator : BaseExample
    {
        private Initiator initiator;
        public CommandQueryInitiator() :base("CommandQueryInitiator")
        {
            SendLowLevelRequest();
        }

        private void SendLowLevelRequest()
        {
            initiator = new Initiator( logger);
            Response response= initiator.SendRequest(CreateLowLevelRequest(RequestType.Query));
            initiator.SendRequest(CreateLowLevelRequest(RequestType.Command));
        }
    }
}