using InnerSubscribeRequest = KubeMQ.Grpc.Subscribe;

namespace KubeMQ.SDK.csharp.Subscription
{
    /// <summary>
    /// Represents a set of parameters which the Subscriber uses to subscribe to the KubeMQ.
    /// </summary>
    public class SubscribeRequest
    {
        #region Properties
        /// <summary>
        /// Represents the type of Subscriber operation KubeMQ.SDK.csharp.Subscription.SubscribeType.
        /// </summary>
        public SubscribeType SubscribeType { get; set; }
        /// <summary>
        /// Represents an identifier that will subscribe to kubeMQ under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents the channel name that will subscribe to under kubeMQ.
        /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// Represents the group the channel is assign to.
        /// </summary>
        public string Group { get; set; }     
        /// <summary>
        /// Represents the type of subscription to persistence using KubeMQ.SDK.csharp.Subscription.EventsStoreType.
        /// </summary>
        public EventsStoreType EventsStoreType { get; set; }
        /// <summary>
        /// Represents the value of subscription to persistence queue.
        /// </summary>
        public long EventsStoreTypeValue { get; set; }
        #endregion

        #region C'tor
        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.Subscription.SubscribeRequest.
        /// </summary>
        public SubscribeRequest() { }
        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.Subscription.SubscribeRequest using a set of parameters.
        /// </summary>
        /// <param name="subscriptionTypeValue">Represents the type of Subscriber operation KubeMQ.SDK.csharp.Subscription.SubscribeType.</param>
        /// <param name="clientID">Represents an identifier that will subscribe to kubeMQ under.</param>
        /// <param name="channel">Represents the channel name that will subscribe to under kubeMQ.</param>
        /// <param name="group">Represents the group the channel is assign to , if not filled will be empty string(no group).</param>        
        /// <param name="eventsStoreType"> Represents the type of subscription to persistence using KubeMQ.SDK.csharp.Subscription.EventsStoreType.</param>
        /// <param name="eventsStoreTypeValue">Represents the value of subscription to persistence queue.</param>
        public SubscribeRequest(SubscribeType subscriptionType, string clientID, string channel, EventsStoreType eventsStoreType, long eventsStoreTypeValue, string group="")
        {
            SubscribeType = subscriptionType;
            ClientID = clientID;
            Channel = channel;
            Group = group;
            EventsStoreType = eventsStoreType;
            EventsStoreTypeValue = eventsStoreTypeValue;            
        }
        #endregion

        internal SubscribeRequest(InnerSubscribeRequest inner)
        {
            SubscribeType = (SubscribeType)inner.SubscribeTypeData;
            ClientID = inner.ClientID;
            Channel = inner.Channel;
            Group = inner.Group ?? string.Empty;         
            EventsStoreTypeValue = inner.EventsStoreTypeValue;       
        }

        internal InnerSubscribeRequest ToInnerSubscribeRequest()
        {
            return new InnerSubscribeRequest()
            {
                SubscribeTypeData = (InnerSubscribeRequest.Types.SubscribeType)this.SubscribeType,
                ClientID = this.ClientID,
                Channel = this.Channel,
                Group = this.Group ?? string.Empty,
                EventsStoreTypeData = (InnerSubscribeRequest.Types.EventsStoreType)this.EventsStoreType,
                EventsStoreTypeValue = this.EventsStoreTypeValue
            };
        }

        internal bool IsValideType(string subscriber)
        {
            if (subscriber == "CommandQuery")
            {
                return (SubscribeType == SubscribeType.Commands || SubscribeType == SubscribeType.Queries);
            }
            else // (subscriber == "Events")
            {
                return (SubscribeType == SubscribeType.Events || SubscribeType == SubscribeType.EventsStore);
            }

        }

    }
}
