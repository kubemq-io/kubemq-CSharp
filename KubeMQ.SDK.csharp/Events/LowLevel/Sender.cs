using Grpc.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using KubeMQ.SDK.csharp.Basic;
using InnerEvent = KubeMQ.Grpc.Event;
using KubeMQGrpc = KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Events.LowLevel
{
    /// <summary>
    /// Represents the instance that is responsible to send events to the kubemq.
    /// </summary>
    public class Sender : GrpcClient
    {
        private static ILogger logger;

        private readonly BufferBlock<KubeMQGrpc.Result> _RecivedResults = new BufferBlock<KubeMQGrpc.Result>();


        #region C'tor
        /// <summary>
        /// Initialize a new Sender.
        /// Logger will write to default output with suffix KubeMQSDK.
        /// KubeMQAddress will be parsed from Config or environment parameter.
        /// </summary>
        public Sender() : this(null, null) { }

        /// <summary>    
        /// Initialize a new Sender with the requested ILogger.
        /// KubeMQAddress will be parsed from Config or environment parameter.
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger.</param>
        public Sender(ILogger plogger) : this(null, plogger) { }

        /// <summary>
        /// Initialize a new Sender under the requested KubeMQ Server Address.
        /// Logger will write to default output with suffix KubeMQSDK.
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address.</param>
        public Sender(string KubeMQAddress) : this(KubeMQAddress, null) { }

        /// <summary>
        /// Initialize a new Sender under the requested KubeMQ Server Address with the requested ILogger .
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address.</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger.</param>
        public Sender(string KubeMQAddress, ILogger plogger)
        {
            logger = Tools.Logger.InitLogger(plogger, "Sender");

            _kubemqAddress = KubeMQAddress;
        }
        #endregion

        /// <summary>
        /// Publish a single event using the KubeMQ.
        /// </summary>
        /// <param name="event">KubeMQ:Event Class.</param>
        /// <returns>KubeMQ.SDK.csharp.PubSub.Result that contain info regarding event status.</returns>
        public Result SendEvent(Event notification)
        {
            try
            {
                InnerEvent innerEvent = notification.ToInnerEvent();

                KubeMQGrpc.Result innerResult = GetKubeMQClient().SendEvent(innerEvent, _metadata);

                if (innerResult == null)
                {
                    return null;
                }

                Result result = new Result(innerResult);

                return result;
            }
            catch (RpcException ex)
            {
                logger.LogError(ex, "Exception in SendEvent");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in SendEvent");

                throw new Exception(ex.Message);
            }
        }

        internal async Task StreamEventWithoutResponse(Event notification)
        {
            try
            {
                notification.ReturnResult = false;
                InnerEvent innerEvent = notification.ToInnerEvent();

                await GetKubeMQClient().SendEventsStream(_metadata).RequestStream.WriteAsync(innerEvent);
            }
            catch (RpcException ex)
            {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// publish a constant stream of events.
        /// </summary>
        /// <param name="event">KubeMQ:Event Class.</param>
        /// <param name="resultDelegate"></param>
        /// <returns>A task that represents the event request that was sent using the StreamEvent .</returns>
        public async Task StreamEvent(Event notification, ReceiveResultDelegate resultDelegate)
        {
            if (!notification.ReturnResult)
            {
                await StreamEventWithoutResponse(notification);
                return;
            }

            // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'
            try
            {
                InnerEvent innerEvent = notification.ToInnerEvent();

                // Send Event via GRPC RequestStream
                await GetKubeMQClient().SendEventsStream(_metadata).RequestStream.WriteAsync(innerEvent);
                
                // Listen for Async Response (Result)
                using (var call = GetKubeMQClient().SendEventsStream(_metadata))
                {
                    // Wait for Response..
                    await call.ResponseStream.MoveNext(CancellationToken.None);

                    // Received a Response
                    KubeMQGrpc.Result response = call.ResponseStream.Current;

                    // add response to queue
                    _RecivedResults.Post(response);
                    LogResponse(response);
                }

                // send result (response) to end-user 
                var resultTask = Task.Run((Func<Task>)(async () =>
                {
                    while (true)
                    {
                        // await for response from queue
                        KubeMQGrpc.Result response = await _RecivedResults.ReceiveAsync();

                        // Convert KubeMQ.Grpc.Result to outter Result
                        Result result = new Result(response);

                        // Activate end-user Receive-Result-Delegate
                        resultDelegate(result);
                    }
                }));
            }
            catch (RpcException ex)
            {
                logger.LogError(ex, "RPC Exception in StreamEvent");

                throw new RpcException(ex.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Close a publish constant stream of events.
        /// </summary>
        /// <returns>A task that represents the closing request of the stream events .</returns>
        public async Task ClosesEventStreamAsync()
        {
            await GetKubeMQClient().SendEventsStream(_metadata).RequestStream.CompleteAsync();
        }

        private void LogResponse(KubeMQGrpc.Result response)
        {
            logger.LogInformation($"Sender received 'Result': EventID:'{response.EventID}', Sent:'{response.Sent}', Error:'{response.Error}'");
        }

    }
}
