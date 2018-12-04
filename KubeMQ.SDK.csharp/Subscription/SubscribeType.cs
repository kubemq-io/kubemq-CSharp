using System;
using System.Collections.Generic;
using System.Text;

namespace KubeMQ.SDK.csharp.Subscription
{
    /// <summary>
    /// Type of subscription operation pattern
    /// </summary>
    public enum SubscribeType
    {
        /// <summary>
        /// Default
        /// </summary>
        SubscribeTypeUndefined = 0,
        /// <summary>
        /// PubSub event
        /// </summary>
        Events = 1,
        /// <summary>
        /// PubSub event with persistence
        /// </summary>
        EventsStore = 2,
        /// <summary>
        /// ReqRep perform action
        /// </summary>
        Commands = 3,
        /// <summary>
        /// ReqRep return data
        /// </summary>
        Queries = 4
    }
}
