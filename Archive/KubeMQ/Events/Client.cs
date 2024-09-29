using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Client;

namespace KubeMQ.Events
{
    public delegate void ReceiveEventsMessage(EventsReceiveMessage message);
    public delegate void ReceiveEventsError(string error );
    public class Client : BaseClient
    {
        public Client(ClientOptions clientOptions) : base(clientOptions)
        {
           
        }

        public void  Send(EventsMessage message)
        {
            Send(message, new CancellationToken());
        }

        public void Send(EventsMessage message, CancellationToken cancellationToken )
        {
            message.ClientId = this.ClientOptions.PopulateClientId(message.ClientId);
            message.Validate();
            try
            {
                 this.Client.SendEvent(message.ToEvent(), this.ClientOptions.GetGrpcMetadata(), null,
                    cancellationToken);
                
            }
            catch (RpcException ex) {
                throw new RpcException(ex.Status);
            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }
        }

        public async Task Subscribe(EventsSubscribeRequest request, ReceiveEventsMessage messageHandler , ReceiveEventsError errorHandler, CancellationToken cancellationToken= new CancellationToken())
        {
            request.ClientId = this.ClientOptions.PopulateClientId(request.ClientId);
            request.Validate();
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler), "request must have a non-null message delegate");
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(messageHandler), "request must have a non-null error delegate");
            
            while (true) {
                try
                {
                    using (var call = Client.SubscribeToEvents(request.ToSubscribeRequest(),
                        ClientOptions.GetGrpcMetadata(),
                        null, cancellationToken))
                    {
                        while (await call.ResponseStream.MoveNext())
                        {
                            EventsReceiveMessage message = new EventsReceiveMessage(call.ResponseStream.Current);
                            messageHandler(message);
                        }
                    }
                }
                catch (RpcException rpcx)
                {
                    if (rpcx.StatusCode == StatusCode.Cancelled)
                    {
                        break;
                    }
                    else
                    {
                        errorHandler(rpcx.Message);
                    }
                }
                catch (Exception e)
                {
                    errorHandler(e.Message);
                }

                await Task.Delay(ClientOptions.ReconnectInterval);
            }
        }
        public async Task Subscribe(EventsSubscribeRequest[] requests, ReceiveEventsMessage messageHandler , ReceiveEventsError errorHandler , CancellationToken cancellationToken = new CancellationToken())
        {
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler), "request must have a non-null message delegate");
            if (errorHandler == null)
                throw new ArgumentNullException(nameof(messageHandler), "request must have a non-null error delegate");
            Task[] taskArray = new Task[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                taskArray[i] = Subscribe(requests[i], messageHandler,errorHandler, cancellationToken);
                
            }
            await Task.WhenAll(taskArray);
        }
       

        public async Task Subscribe(EventsSubscribeRequest request, IEventsReceiveCallback handler,
            CancellationToken cancellationToken=new CancellationToken())
        {
            request.ClientId = this.ClientOptions.PopulateClientId(request.ClientId);
            request.Validate();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler), "request must have a non-null handler");
            while (true) {
                try
                {
                    using (var call = Client.SubscribeToEvents(request.ToSubscribeRequest(),
                        ClientOptions.GetGrpcMetadata(),
                        null, cancellationToken))
                    {
                        while (await call.ResponseStream.MoveNext())
                        {
                            EventsReceiveMessage message = new EventsReceiveMessage(call.ResponseStream.Current);
                            handler.ReceiveEventsMessage(message);
                        }
                    }
                }
                catch (RpcException rpcx)
                {
                    if (rpcx.StatusCode == StatusCode.Cancelled)
                    {
                        break;
                    }
                    else
                    {
                        handler.ReceiveEventsError(rpcx.Message);
                    }
                }
                catch (Exception e)
                {
                    handler.ReceiveEventsError(e.Message);
                }

                await Task.Delay(ClientOptions.ReconnectInterval);
            }
            }

      

        public async Task Subscribe(EventsSubscribeRequest[] requests, IEventsReceiveCallback handler,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler), "request must have a non-null handler");
            Task[] taskArray = new Task[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                taskArray[i] = Subscribe(requests[i], handler, cancellationToken);
                
            }
            await Task.WhenAll(taskArray);
        }
    }
}
 