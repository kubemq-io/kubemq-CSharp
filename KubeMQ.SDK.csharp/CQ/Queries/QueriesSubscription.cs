using System;
using pb= KubeMQ.Grpc;


namespace KubeMQ.SDK.csharp.CQ.Queries
{
    public class QueriesSubscription
    {
        /// <summary>
        /// The channel to subscribe to.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// The group to subscribe to.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Callback function to handle received commands.
        /// </summary>
        public delegate void ReceivedQueryHandler (QueryReceived receivedQuery);

        /// <summary>
        /// Callback function to handle errors.
        /// </summary>
        public delegate void ErrorHandler(Exception error);

        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public event ReceivedQueryHandler OnReceivedQuery;

        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public event ErrorHandler OnError;

        /// The QueriesSubscription class represents a subscription for receiving commands from a KubeMQ channel.
        /// /
        public QueriesSubscription (string channel , string group, ReceivedQueryHandler  onReceiveQueryHandler, ErrorHandler onErrorHandler)
        {
            Channel = channel;
            Group = group;
            OnReceivedQuery = onReceiveQueryHandler;
            OnError = onErrorHandler;
        }


        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public QueriesSubscription()
        {
            
        }


        /// <summary>
        /// Sets the channel for the command subscription.
        /// </summary>
        /// <param name="channel">The channel to be set.</param>
        /// <returns>The QueriesSubscription instance with the updated channel.</returns>
        public QueriesSubscription SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the group of the command subscription.
        /// </summary>
        /// <param name="group">The group to set for the command subscription.</param>
        /// <returns>The updated QueriesSubscription object.</returns>
        public QueriesSubscription SetGroup(string group)
        {
            Group = group;
            return this;
        }

        /// <summary>
        /// Sets the callback function to handle received commands.
        /// </summary>
        /// <param name="onReceiveQueryHandler">The callback function to handle the received commands.</param>
        /// <returns>The QueriesSubscription object.</returns>
        public QueriesSubscription SetReceivedQueryHandler(ReceivedQueryHandler onReceiveQueryHandler)
        {
            OnReceivedQuery = onReceiveQueryHandler;
            return this;
        }

        /// <summary>
        /// Sets the error handler callback function for the QueriesSubscription.
        /// </summary>
        /// <param name="onErrorHandler">The error handler callback function.</param>
        /// <returns>The QueriesSubscription instance.</returns>
        public QueriesSubscription SetErrorHandler(ErrorHandler onErrorHandler)
        {
            OnError = onErrorHandler;
            return this;
        }
        
        /// <summary>
        /// Validates the command subscription by checking if channel and on_receive_command_callback are set.
        /// </summary>
        internal void Validate()
        {
            if (string.IsNullOrEmpty(Channel))
                throw new ArgumentException("Queries subscription must have a channel.");

            if (OnReceivedQuery == null)
                throw new ArgumentException("Queries subscription must have an ReceiveQueryHandler.");
        }

        
        internal pb.Subscribe Encode(string clientId)
        {
            return new pb.Subscribe()
            {
                Channel = Channel,
                ClientID = clientId,
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.Queries,
                Group = Group
            };
        }

        public override string ToString()
        {
            return $"QueriesSubscription: channel={Channel}, group={Group}";
        }

        internal void RaiseOnQueryReceive(QueryReceived receivedQuery)
        {
            OnReceivedQuery?.Invoke(receivedQuery);
        }

        internal void RaiseOnError(Exception error)
        {
            OnError?.Invoke(error);
        }
    }
}