using System;
using CommonExample;
using KubeMQ.SDK.csharp.Events;
using KubeMQ.SDK.csharp.Tools;

namespace EventChannel
{
    public class EventChannel : BaseExample
    {
        private Channel messageChannel;
        public EventChannel() :base("EventChannel")
        {
            ChannelParameters eventChannelParameters = CreateEventChannelParam(true);
            messageChannel = new Channel(eventChannelParameters);
            messageChannel.SendEvent(CreateChannelEvent());
        }

        private Event CreateChannelEvent()
        {
            Event message = new Event()
            {
                Body = Converter.ToByteArray("Event"),
                Metadata = "EventChannel"
            };
            return message;
        }

        private ChannelParameters CreateEventChannelParam(bool ToStore)
        {
            ChannelParameters parameters = new ChannelParameters()
            {
                ChannelName = this.ChannelName,
                ClientID = "EventChannelID",
                Store = ToStore,
                Logger = this.logger              
            };
            return parameters;
        }
    }
}