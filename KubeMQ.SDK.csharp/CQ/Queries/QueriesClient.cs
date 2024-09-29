using System;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Common;
using KubeMQ.SDK.csharp.Results;

namespace KubeMQ.SDK.csharp.CQ.Queries
{
    public class QueriesClient : BaseClient
    {
        /// <summary>
        /// Creates a new channel for sending queries.
        /// </summary>
        /// <param name="channelName">The name of the channel to create.</param>
        /// <returns>A task that represents the asynchronous create operation.</returns>
        public async Task<Result> Create(string channelName)
        {
            return await  CreateDeleteChannel(  Cfg.ClientId, channelName, "queries", true);
        }

        /// <summary>
        /// Deletes a queries channel.
        /// </summary>
        /// <param name="channelName">The name of the channel to delete.</param>
        /// <returns>The result of the delete operation.</returns>
        public async Task<Result> Delete(string channelName)
        {
            return await  CreateDeleteChannel(  Cfg.ClientId, channelName, "queries", false);
        }

        /// <summary>
        /// Retrieves a list of queries channels.
        /// </summary>
        /// <param name="search">Optional. A string to search for specific channels. The search is case-insensitive.</param>
        /// <returns>A <see cref="ListCqAsyncResult"/> object that represents the result of the asynchronous operation.</returns>
        public async Task<ListCqAsyncResult> List(string search="")
        {
            return await ListCqChannels(Cfg.ClientId, search, "queries");
        }

        /// <summary>
        /// Sends a query message to KubeMQ.
        /// </summary>
        /// <param name="query">The query message to be sent.</param>
        /// <returns>The response received from KubeMQ.</returns>
        public async Task<QueryResponse> Send(Query query)
        {
            {
                try
                {
                    if (!IsConnected)
                    {
                        return new QueryResponse().SetError("Client not connected").SetIsExecuted(false) ;
                    }

                    var grpcCommand= query.Validate().Encode(Cfg.ClientId);
                    var result = await KubemqClient.SendRequestAsync(grpcCommand);
                    return !string.IsNullOrEmpty(result.Error) ? new QueryResponse().SetError(result.Error).SetIsExecuted(false) : new QueryResponse().Decode(result);
                }
                catch (Exception e)
                {
                    return new QueryResponse().SetError(e.Message).SetIsExecuted(false) ;
                    
                }
                
            }
        }

        /// <summary>
        /// Subscribes to incoming queries.
        /// </summary>
        /// <param name="subscription">The subscription details, including the channel and group.</param>
        /// <param name="cancellationToken">Cancellation token to stop the subscription.</param>
        /// <returns>The result of subscribing to queries. If successful, the IsSuccess property will be true; otherwise, the IsSuccess property will be false and the ErrorMessage property will contain an error message.</returns>
        public Result Subscribe(QueriesSubscription subscription, CancellationToken cancellationToken)
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
                                var receivedQueries =QueryReceived.Decode(stream.ResponseStream.Current);
                                subscription.RaiseOnQueryReceive(receivedQueries);
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
        /// Sends a response to a query.
        /// </summary>
        /// <param name="queryResponse">The response to send.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the result of sending the response.</returns>
        public async Task<Result> Response(QueryResponse queryResponse)
        {
            try
            {
                if (!IsConnected)
                {
                    return new Result( "Client not connected");
                }

                var grpcCommandResponse = queryResponse.Validate().Encode(Cfg.ClientId);
                var result = await KubemqClient.SendResponseAsync(grpcCommandResponse);
                return new Result() ;
            }
            catch (Exception e)
            {
                return new Result(e);
            }
            
        }
    }
}