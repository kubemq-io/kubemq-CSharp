using System;
using CommonExample;
using KubeMQ.SDK.csharp.CommandQuery;
using KubeMQ.SDK.csharp.Tools;

namespace CommandQueryChannel
{
    public class CommandQueryChannel : BaseExample
    {
        private Channel requestChannel;
        private ChannelParameters requestChannelParameters;
        public CommandQueryChannel() : base("CommandQueryChannel")
        {
            SendQueryRequest();
            SendCommandRequest();
        }


        private void SendQueryRequest()
        {
            requestChannelParameters = CreateRequestChannelParam(RequestType.Query);
            requestChannel = new Channel(requestChannelParameters);
            Response request = requestChannel.SendRequest(CreateChannelRequest());
        }

        private void SendCommandRequest()
        {
            requestChannelParameters = CreateRequestChannelParam(RequestType.Command);
            requestChannel = new Channel(requestChannelParameters);
            requestChannel.SendRequest(CreateChannelRequest());
        }

        private Request CreateChannelRequest()
        {
            Request request = new Request()
            {
                Metadata = "CommandQueryChannel",
                Body = Converter.FromString("Request")
            };
            return request;
        }

        private ChannelParameters CreateRequestChannelParam(RequestType requestType)
        {
            ChannelParameters channelParameters = new ChannelParameters()
            {
                ChannelName = this.ChannelName,
                ClientID = this.ClientID,
                Timeout = this.Timeout,
                CacheKey = "",
                CacheTTL = 0,
                Logger = logger,
                RequestsType = requestType
            };
            return channelParameters;
        }
    }
}