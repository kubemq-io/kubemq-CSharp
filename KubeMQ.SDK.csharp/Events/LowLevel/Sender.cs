using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Grpc.Core;
using KubeMQ.SDK.csharp.Basic;
using Microsoft.Extensions.Logging;
using InnerEvent = KubeMQ.Grpc.Event;
using KubeMQGrpc = KubeMQ.Grpc;
using KubeMQ.Grpc;

namespace KubeMQ.SDK.csharp.Events.LowLevel {
    /// <summary>
    /// Represents the instance that is responsible to send events to the kubemq.
    /// </summary>
    public class Sender : GrpcClient {
        private static ILogger logger;

        private readonly BufferBlock<KubeMQGrpc.Result> _RecivedResults = new BufferBlock<KubeMQGrpc.Result> ();

        #region C'tor
        /// <summary>
        /// Initialize a new Sender.
        /// Logger will write to default output with suffix KubeMQSDK.
        /// KubeMQAddress will be parsed from Config or environment parameter.
        /// </summary>
        public Sender(): this(null, null, null) {}

        /// <summary>    
        /// Initialize a new Sender with the requested ILogger.
        /// KubeMQAddress will be parsed from Config or environment parameter.
        /// </summary>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Sender(ILogger plogger, string authToken = null): this(null, plogger, authToken) {}

        /// <summary>
        /// Initialize a new Sender under the requested KubeMQ Server Address.
        /// Logger will write to default output with suffix KubeMQSDK.
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Sender(string KubeMQAddress, string authToken = null): this(KubeMQAddress, null, authToken) {}

        /// <summary>
        /// Initialize a new Sender under the requested KubeMQ Server Address with the requested ILogger .
        /// </summary>
        /// <param name="KubeMQAddress">KubeMQ server address.</param>
        /// <param name="plogger">Microsoft.Extensions.Logging Ilogger.</param>
        /// <param name="authToken">Set KubeMQ JWT Auth token to be used for KubeMQ connection.</param>
        public Sender(string KubeMQAddress, ILogger plogger, string authToken = null)
        {
            logger = Tools.Logger.InitLogger(plogger, "Sender");

            _kubemqAddress = KubeMQAddress;
            this.addAuthToken(authToken);
        }
        #endregion

        /// <summary>
        /// Publish a multisubscribers event using the KubeMQ.
        /// </summary>
        /// <param name="notification">KubeMQ:Event Class.</param>
        /// <returns>KubeMQ.SDK.csharp.PubSub.Result that contain info regarding event status.</returns>
        public Result SendEvent(Event notification) {
            try {
                InnerEvent innerEvent = notification.ToInnerEvent();

                KubeMQGrpc.Result innerResult = GetKubeMQClient().SendEvent(innerEvent, Metadata);

                if (innerResult == null) {
                    return null;
                }

                Result result = new Result(innerResult);

                return result;
            } catch (RpcException ex) {
                logger.LogError(ex, "Exception in SendEvent");

                throw new RpcException(ex.Status);
            } catch (Exception ex) {
                logger.LogError(ex, "Exception in SendEvent");

                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Ping check Kubemq response using "low level" sender.
        /// </summary>
        /// <returns>ping status of kubemq.</returns>
        public PingResult Ping() {
            logger.LogTrace($"sender sent ping successfully to address {this.ServerAddress}");
            PingResult rec = GetKubeMQClient().Ping(new Empty());
            return rec;

        }

        internal async Task StreamEventWithoutResponse(Event notification) {
            try {
                notification.ReturnResult = false;
                InnerEvent innerEvent = notification.ToInnerEvent();

                await GetKubeMQClient().SendEventsStream(Metadata).RequestStream.WriteAsync(innerEvent);
            } catch (RpcException ex) {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new RpcException(ex.Status);
            } catch (Exception ex) {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// publish a constant stream of events.
        /// </summary>
        /// <param name="notification">KubeMQ:Event Class.</param>
        /// <param name="resultDelegate"></param>
        /// <returns>A task that represents the event request that was sent using the StreamEvent .</returns>
        public async Task StreamEvent(Event notification, ReceiveResultDelegate resultDelegate) {
            if (!notification.ReturnResult) {
                await StreamEventWithoutResponse(notification);
                return;
            }

            // implement bi-di streams 'SendEventStream (stream Event) returns (stream Result)'
            try {
                InnerEvent innerEvent = notification.ToInnerEvent();

                // Send Event via GRPC RequestStream
                await GetKubeMQClient().SendEventsStream(Metadata).RequestStream.WriteAsync(innerEvent);

                // Listen for Async Response (Result)
                using(var call = GetKubeMQClient().SendEventsStream(Metadata)) {
                    // Wait for Response..
                    await call.ResponseStream.MoveNext(CancellationToken.None);

                    // Received a Response
                    KubeMQGrpc.Result response = call.ResponseStream.Current;

                    // add response to queue
                    _RecivedResults.Post(response);
                    LogResponse(response);
                }

                // send result (response) to end-user 
                var resultTask = Task.Run((Func < Task > )(async() => {
                    while (true) {
                        // await for response from queue
                        KubeMQGrpc.Result response = await _RecivedResults.ReceiveAsync();

                        // Convert KubeMQ.Grpc.Result to outer Result
                        Result result = new Result(response);

                        // Activate end-user Receive-Result-Delegate
                        resultDelegate(result);
                    }
                }));
            } catch (RpcException ex) {
                logger.LogError(ex, "RPC Exception in StreamEvent");

                throw new RpcException(ex.Status);
            } catch (Exception ex) {
                logger.LogError(ex, "Exception in StreamEvent");

                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Close a publish constant stream of events.
        /// </summary>
        /// <returns>A task that represents the closing request of the stream events .</returns>
        public async Task ClosesEventStreamAsync() {
            await GetKubeMQClient().SendEventsStream(Metadata).RequestStream.CompleteAsync();
        }

        private void LogResponse(KubeMQGrpc.Result response) {
            logger.LogInformation($"Sender received 'Result': EventID:'{response.EventID}', Sent:'{response.Sent}', Error:'{response.Error}'");
        }

    }
}