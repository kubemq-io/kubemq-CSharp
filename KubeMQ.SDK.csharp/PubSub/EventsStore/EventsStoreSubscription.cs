using System;

namespace KubeMQ.SDK.csharp.PubSub.EventsStore
{
    /// <summary>
    /// Enum representing the different types of starting points for subscribing to events in the EventsStore.
    /// </summary>
    public enum StartAtType : int
    {
        /// <summary>
        /// Represents the start at type "Undefined" for the EventsStoreSubscription.
        /// </summary>
        StartAtTypeUndefined, // Defaults to 0

        /// <summary>
        /// Represents the start at type for an event subscription in the EventsStoreSubscription class.
        /// </summary>
        StartAtTypeFromNew,   // 1

        /// <summary>
        /// Represents the start-at type of an event subscription in the EventsStore.
        /// </summary>
        StartAtTypeFromFirst, // 2

        /// <summary>
        /// Represents the start position of an events store subscription from the last event onwards.
        /// </summary>
        /// <remarks>
        /// This enum member is used to specify the start position for a subscription to the events store.
        /// When set to StartAtTypeFromLast, the subscription will start from the last event in the store and receive all subsequent events.
        /// </remarks>
        StartAtTypeFromLast,  // 3

        /// <summary>
        /// Represents a start-at type for an events store subscription.
        /// </summary>
        StartAtTypeFromSequence, // 4

        /// <summary>
        /// Represents the start type of an EventsStoreSubscription.
        /// </summary>
        StartAtTypeFromTime // 5
    }

    /// <summary>
    /// Represents a subscription to events in an event store.
    /// </summary>
    public class EventsStoreSubscription
    {
        /// <summary>
        /// Represents a subscription to an events store channel.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Represents a subscription to the KubeMQ events store.
        /// </summary>
        public string Group { get; private set; }

        /// <summary>
        /// Enum representing the type of StartAt value for the EventsStoreSubscription.
        /// </summary>
        public StartAtType StartAt { get; set; }

        /// <summary>
        /// Represents the start time value used when setting the <see cref="EventsStoreSubscription.StartAtType"/> to <see cref="StartAtType.StartAtTypeFromTime"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="StartAtTimeValue"/> property determines the specific time in UTC from where to start consuming events.
        /// </remarks>
        public DateTime StartAtTimeValue { get; set; }

        /// <summary>
        /// Represents the start position for subscribing to events in EventsStore.
        /// </summary>
        public long StartAtSequenceValue { get; set; }
        
        // Delegates for the event handling
        /// <summary>
        /// Represents an event store subscription for receiving events.
        /// </summary>
        public delegate void ReceiveEventHandler(EventStoreReceived eventStoreReceived);

        /// <summary>
        /// Represents a subscription to events in the event store.
        /// </summary>
        public delegate void ErrorHandler(Exception error);

        // Events based on the delegates
        /// <summary>
        /// Represents an event subscription for the KubeMQ.EventsStore.
        /// </summary>
        public event ReceiveEventHandler OnReceiveEvent;

        /// <summary>
        /// Represents the event handler for error events in the event store subscription.
        /// </summary>
        public event ErrorHandler OnError;

        /// <summary>
        /// Represents a subscription for events in the event store.
        /// </summary>
        public EventsStoreSubscription()
        {
        }

        /// <summary>
        /// Sets the channel for the EventStoreSubscription.
        /// </summary>
        /// <param name="value">The channel to subscribe to.</param>
        /// <returns>The modified EventStoreSubscription.</returns>
        public EventsStoreSubscription SetChannel(string value)
        {
            Channel = value;
            return this;
        }

        /// <summary>
        /// Sets the group for the EventsStoreSubscription.
        /// </summary>
        /// <param name="value">The group value to set.</param>
        /// <returns>The EventsStoreSubscription object.</returns>
        public EventsStoreSubscription SetGroup(string value)
        {
            Group = value;
            return this;
        }

        /// <summary>
        /// Sets the event handler for receiving events from the events store.
        /// </summary>
        /// <param name="handler">The event handler to set</param>
        /// <returns>The instance of the EventsStoreSubscription</returns>
        public EventsStoreSubscription SetOnReceiveEvent(ReceiveEventHandler handler)
        {
            OnReceiveEvent += handler; 
            return this;
        }

        /// <summary>
        /// Sets the error handler for the EventsStoreSubscription.
        /// </summary>
        /// <param name="handler">The error handler to set.</param>
        /// <returns>The EventsStoreSubscription instance.</returns>
        public EventsStoreSubscription SetOnError(ErrorHandler handler)
        {
            OnError += handler; 
            return this;
        }

        /// <summary>
        /// Set the start at type for the events store subscription.
        /// </summary>
        /// <param name="startAtType">The start at type value</param>
        /// <returns>The modified EventsStoreSubscription object</returns>
        public EventsStoreSubscription SetStartAtType(StartAtType startAtType)
        {
            StartAt = startAtType;
            return this;
        }

        /// <summary>
        /// Sets the start time for the subscription to retrieve events from the EventStore.
        /// </summary>
        /// <param name="dateTime">The start time value.</param>
        /// <returns>The updated EventsStoreSubscription instance.</returns>
        public EventsStoreSubscription SetStartAtTime(DateTime dateTime)
        {
            StartAtTimeValue = dateTime;
            return this;
        }

        /// <summary>
        /// Sets the start sequence for the event subscription.
        /// </summary>
        /// <param name="sequence">The sequence number to start from.</param>
        /// <returns>The updated EventsStoreSubscription object.</returns>
        public EventsStoreSubscription SetStartAtSequence(long sequence)
        {
            StartAtSequenceValue = sequence;
            return this;
        }

        /// <summary>
        /// Raises the OnReceiveEvent event.
        /// </summary>
        /// <param name="eventStoreReceived">The event received by the event store.</param>
        internal void RaiseOnReceiveEvent(EventStoreReceived eventStoreReceived)
        {
            OnReceiveEvent?.Invoke(eventStoreReceived);
        }


        /// <summary>
        /// Raises the OnError event and invokes the registered error handlers.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        internal void RaiseOnError(Exception exception)
        {
            OnError?.Invoke(exception);
        }

        /// <summary>
        /// Validates the event subscription.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the event subscription is not valid.</exception>
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
            if (StartAt == StartAtType.StartAtTypeUndefined)
            {
                throw new InvalidOperationException("Event subscription must have a StartAt type.");
            }
            
            if (StartAt == StartAtType.StartAtTypeFromSequence && StartAtSequenceValue == 0)
            {
                throw new InvalidOperationException("Event subscription type of StartAtTypeFromSequence must have a sequence value.");
            }
            
            if (StartAt == StartAtType.StartAtTypeFromTime && StartAtTimeValue == DateTime.MinValue)
            {
                throw new InvalidOperationException("Event subscription type of StartAtTypeFromTime must have a time value.");
            }
        }
    }
}