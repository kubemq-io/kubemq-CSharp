using System;
using CommonExample;
using Microsoft.Extensions.Logging;
using KubeMQ.SDK.csharp.Events.LowLevel;

namespace EventSender
{
    internal class EventSender : BaseExample
    {
        private Sender sender;

        public EventSender() : base("EventSender")
        {
            SendLowLevelEvents();
        }

        private void SendLowLevelEvents()
        {
            sender = new Sender(logger);
            Event @event =CreateLowLevelEventWithoutStore();
            sender.SendEvent(@event);
        }
    }
}