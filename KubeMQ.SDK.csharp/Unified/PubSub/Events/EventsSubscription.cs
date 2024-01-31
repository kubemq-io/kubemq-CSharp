using System;

namespace KubeMQ.SDK.csharp.Unified.PubSub.Events
{
    public class EventsSubscription
    {
        public string Channel { get; private set; }
        public string Group { get; private set; }

        // Delegates for the event handling
        public delegate void ReceiveEventHandler(EventReceived eventReceived);
        public delegate void ErrorHandler(Exception error);

        // Events based on the delegates
        public event ReceiveEventHandler OnReceiveEvent;
        public event ErrorHandler OnError;

        public EventsSubscription()
        {
        }

        public EventsSubscription SetChannel(string value)
        {
            Channel = value;
            return this;
        }

        public EventsSubscription SetGroup(string value)
        {
            Group = value;
            return this;
        }

        public EventsSubscription SetOnReceiveEvent(ReceiveEventHandler handler)
        {
            OnReceiveEvent += handler; 
            return this;
        }

        public EventsSubscription SetOnError(ErrorHandler handler)
        {
            OnError += handler; 
            return this;
        }

        public void RaiseOnReceiveEvent(EventReceived receivedEvent)
        {
            OnReceiveEvent?.Invoke(receivedEvent);
        }


        public void RaiseOnError(Exception exception)
        {
            OnError?.Invoke(exception);
        }
        
        public void Validate()
        {
            if (string.IsNullOrEmpty(Channel))
            {
                throw new InvalidOperationException("Event subscription must have a channel.");
            }
            if (OnReceiveEvent == null)
            {
                throw new InvalidOperationException("Event subscription must have an OnReceiveEvent callback function.");
            }
        }
    }
}