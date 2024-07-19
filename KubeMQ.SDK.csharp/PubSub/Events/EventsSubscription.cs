using System;
using pb= KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.PubSub.Events
{
    /// <summary>
    /// Represents an event subscription configuration.
    /// </summary>
    public class EventsSubscription
    {
        /// <summary>
        /// Represents a channel for publishing and subscribing to events.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Represents a group for events subscription.
        /// </summary>
        public string Group { get; private set; }

        /// <summary>
        /// Represents an event received from KubeMQ.
        /// </summary>
        public delegate void ReceiveEventHandler(EventReceived eventReceived);

        /// <summary>
        /// Represents an event subscription for receiving events from KubeMQ.
        /// </summary>
        public delegate void ErrorHandler(Exception error);

        /// <summary>
        /// Represents an event subscription.
        /// </summary>
        public event ReceiveEventHandler OnReceiveEvent;

        /// <summary>
        /// Represents an event subscription for receiving events from KubeMQ.
        /// </summary>
        public event ErrorHandler OnError;

        /// <summary>
        /// Represents a subscription to events in KubeMQ.
        /// </summary>
        /// <remarks>
        /// This class provides methods to configure the subscription, handle received events, and handle errors.
        /// </remarks>
        public EventsSubscription()
        {
        }
        
        public EventsSubscription(string channel, string group, ReceiveEventHandler onReceiveEvent, ErrorHandler onError)
        {
            Channel = channel;
            Group = group;
            OnReceiveEvent = onReceiveEvent;
            OnError = onError;
        }

        /// <summary>
        /// Sets the channel for the <see cref="EventsSubscription"/>.
        /// </summary>
        /// <param name="value">The name of the channel.</param>
        /// <returns>The updated <see cref="EventsSubscription"/> instance.</returns>
        public EventsSubscription SetChannel(string value)
        {
            Channel = value;
            return this;
        }

        /// <summary>
        /// Sets the group for the event subscription.
        /// </summary>
        /// <param name="value">The group value to set.</param>
        /// <returns>The updated EventsSubscription instance.</returns>
        public EventsSubscription SetGroup(string value)
        {
            Group = value;
            return this;
        }

        /// <summary>
        /// Sets the event handler for receiving events.
        /// </summary>
        /// <param name="handler">The event handler to be set.</param>
        /// <returns>The updated EventsSubscription instance.</returns>
        public EventsSubscription SetOnReceiveEvent(ReceiveEventHandler handler)
        {
            OnReceiveEvent += handler; 
            return this;
        }

        /// <summary>
        /// Sets the error handler for the EventsSubscription instance.
        /// </summary>
        /// <param name="handler">The error handler delegate that will be invoked when an error occurs.</param>
        /// <returns>The EventsSubscription instance.</returns>
        public EventsSubscription SetOnError(ErrorHandler handler)
        {
            OnError += handler; 
            return this;
        }

        /// <summary>
        /// Raises the <see cref="OnReceiveEvent"/> event of the <see cref="EventsSubscription"/> class.
        /// </summary>
        /// <param name="receivedEvent">The received event.</param>
        internal void RaiseOnReceiveEvent(EventReceived receivedEvent)
        {
            OnReceiveEvent?.Invoke(receivedEvent);
        }


        /// <summary>
        /// Raises the error event with the given exception.
        /// </summary>
        /// <param name="exception">The exception to raise.</param>
        internal void RaiseOnError(Exception exception)
        {
            OnError?.Invoke(exception);
        }

        /// <summary>
        /// Validates the EventsSubscription object.
        /// </summary>
        /// <remarks>
        /// This method is used to ensure that the EventsSubscription object is valid and ready to be used for subscribing to events.
        /// It checks if the Channel property is not null or empty and if the OnReceiveEvent event has been assigned a callback function.
        /// If any of these conditions is not met, an exception is thrown.
        /// </remarks>
        internal void Validate()
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

        internal pb.Subscribe Encode( string clientId = "")
        {
            var pbRequest = new pb.Subscribe()
            {
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.Events,
                ClientID = clientId,
                Channel = Channel,
                Group = Group
            };
            return pbRequest;
        }
    }
}