using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Results;
using KubeMQ.SDK.csharp.Transport;
using pb= KubeMQ.Grpc;
using static KubeMQ.Grpc.kubemq;
using static KubeMQ.SDK.csharp.Common.Common;

namespace KubeMQ.SDK.csharp.CQ.Commands
{
    public class CommandsClient : BaseClient
    {
        /// <summary>
        /// Creates a new channel for sending commands.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task that represents the asynchronous create operation.</returns>
        public async Task<Result> Create(string channelName)
        {
            return await  CreateDeleteChannel(  Cfg.ClientId, channelName, "commands", true);
        }

        /// <summary>
        /// Deletes a command channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>The result of the delete operation.</returns>
        public async Task<Result> Delete(string channelName)
        {
            return await  CreateDeleteChannel(  Cfg.ClientId, channelName, "commands", false);
        }

        /// <summary>
        /// Retrieves a list of command queries channels.
        /// </summary>
        /// <param name="search">Optional. A string to search for specific channels. The search is case-insensitive.</param>
        /// <returns>A <see cref="ListCqAsyncResult"/> object that represents the result of the asynchronous operation.</returns>
        public async Task<ListCqAsyncResult> List(string search="")
        {
            return await ListCqChannels(Cfg.ClientId, search, "commands");
        }

        /// <summary>
        /// Sends a command message to KubeMQ.
        /// </summary>
        /// <param name="command">The command message to be sent.</param>
        /// <returns>The response received from KubeMQ.</returns>
        public async Task<CommandResponse> Send(Command command)
        {
            {
                try
                {
                    if (!IsConnected)
                    {
                        return new CommandResponse().SetError("Client not connected").SetIsExecuted(false) ;
                    }

                    var grpcCommand= command.Validate().Encode(Cfg.ClientId);
                    var result = await KubemqClient.SendRequestAsync(grpcCommand);
                    return !string.IsNullOrEmpty(result.Error) ? new CommandResponse().SetError(result.Error).SetIsExecuted(false) : new CommandResponse().Decode(result);
                }
                catch (Exception e)
                {
                    return new CommandResponse().SetError(e.Message).SetIsExecuted(false) ;
                    
                }
                
            }
        }

        /// <summary>
        /// Subscribes to incoming commands.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <param name="cancellationToken">Cancellation token to stop the subscription.</param>
        /// <returns>The result of subscribing to commands. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>
        public Result Subscribe(CommandsSubscription subscription, CancellationToken cancellationToken)
        {
            try
            {
                if (!IsConnected )
                {
                    return new Result("Client not connected");
                }
                subscription.Validate();
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            using var stream = KubemqClient.SubscribeToRequests(subscription.Encode(Cfg.ClientId), null, null, cancellationToken);
                            while (await stream.ResponseStream.MoveNext(cancellationToken))
                            {
                                var receivedCommands = CommandReceived.Decode(stream.ResponseStream.Current);
                                subscription.RaiseOnCommandReceive(receivedCommands);
                            }
                        }
                        catch (Exception ex)
                        {
                            subscription.RaiseOnError(ex);
                            if (Cfg.DisableAutoReconnect)
                            {
                                break;
                            }

                            await Task.Delay(Cfg.GetReconnectIntervalDuration(), cancellationToken);
                        }
                    }

                }, cancellationToken);
            }
            catch (Exception e)
            {
                return new Result(e) ;
            }

            return new Result() ;
        }

        /// <summary>
        /// Sends a response to a command.
        /// </summary>
        /// <param name="commandResponse">The response to send.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the result of sending the response.</returns>
        public async Task<Result> Response(CommandResponse commandResponse)
        {
            try
            {
                if (!IsConnected)
                {
                    return new Result( "Client not connected");
                }

                var grpcCommandResponse = commandResponse.Encode(Cfg.ClientId);
                var result = await KubemqClient.SendResponseAsync(grpcCommandResponse);
                return new Result();
            }
            catch (Exception e)
            {
                return new Result(e);
            }
            
        }
    }
}