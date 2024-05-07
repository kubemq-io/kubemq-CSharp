using System;
using pb= KubeMQ.Grpc;


namespace KubeMQ.SDK.csharp.CQ.Commands
{
    public class CommandsSubscription
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
        public delegate void ReceivedCommandHandler (CommandReceived receivedCommand);

        /// <summary>
        /// Callback function to handle errors.
        /// </summary>
        public delegate void ErrorHandler(Exception error);

        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public event ReceivedCommandHandler OnReceivedCommand;

        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public event ErrorHandler OnError;

        /// The CommandsSubscription class represents a subscription for receiving commands from a KubeMQ channel.
        /// /
        public CommandsSubscription (string channel , string group, ReceivedCommandHandler  onReceiveCommandHandler, ErrorHandler onErrorHandler)
        {
            Channel = channel;
            Group = group;
            OnReceivedCommand = onReceiveCommandHandler;
            OnError = onErrorHandler;
        }


        /// <summary>
        /// Represents a subscription for receiving commands from a KubeMQ channel.
        /// </summary>
        public CommandsSubscription()
        {
            
        }


        /// <summary>
        /// Sets the channel for the command subscription.
        /// </summary>
        /// <param name="channel">The channel to be set.</param>
        /// <returns>The CommandsSubscription instance with the updated channel.</returns>
        public CommandsSubscription SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the group of the command subscription.
        /// </summary>
        /// <param name="group">The group to set for the command subscription.</param>
        /// <returns>The updated CommandsSubscription object.</returns>
        public CommandsSubscription SetGroup(string group)
        {
            Group = group;
            return this;
        }

        /// <summary>
        /// Sets the callback function to handle received commands.
        /// </summary>
        /// <param name="onReceiveCommandHandler">The callback function to handle the received commands.</param>
        /// <returns>The CommandsSubscription object.</returns>
        public CommandsSubscription SetReceivedCommandHandler(ReceivedCommandHandler onReceiveCommandHandler)
        {
            OnReceivedCommand = onReceiveCommandHandler;
            return this;
        }

        /// <summary>
        /// Sets the error handler callback function for the CommandsSubscription.
        /// </summary>
        /// <param name="onErrorHandler">The error handler callback function.</param>
        /// <returns>The CommandsSubscription instance.</returns>
        public CommandsSubscription SetErrorHandler(ErrorHandler onErrorHandler)
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
                throw new ArgumentException("Commands subscription must have a channel.");

            if (OnReceivedCommand == null)
                throw new ArgumentException("Commands subscription must have an ReceivedCommandHandler.");
        }

        
        internal pb.Subscribe Encode(string clientId)
        {
            return new pb.Subscribe()
            {
                Channel = Channel,
                ClientID = clientId,
                SubscribeTypeData = pb.Subscribe.Types.SubscribeType.Commands,
                Group = Group
            };
        }

        public override string ToString()
        {
            return $"CommandsSubscription: channel={Channel}, group={Group}";
        }

        internal void RaiseOnCommandReceive(CommandReceived receivedCommand)
        {
            OnReceivedCommand?.Invoke(receivedCommand);
        }

        internal void RaiseOnError(Exception error)
        {
            OnError?.Invoke(error);
        }
    }
}