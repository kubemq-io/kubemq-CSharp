using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using KubeMQ.Client;
using KubeMQ.Grpc;
namespace KubeMQ.Events
{
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

        public Task Subscribe(EventsSubscribeRequest request, IEventsReceiveCallback handler)
        {
            return Subscribe(request, handler, new CancellationToken());
        }

        
        public async Task Subscribe(EventsSubscribeRequest request, IEventsReceiveCallback handler,
            CancellationToken cancellationToken )
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
            CancellationToken cancellationToken )
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
 