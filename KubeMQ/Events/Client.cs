using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KubeMQ.Client;
using KubeMQ.Grpc;
namespace KubeMQ.Events
{
    public class Client : BaseClient
    {
        public Client(ClientOptions clientOptions) : base(clientOptions)
        {
        }

        public Task Send(EventsMessage message)
        {
            return Send(message, new CancellationToken());
        }

        public async Task Send(EventsMessage message, CancellationToken cancellationToken )
        {
            this.ClientOptions.PopulateClientId(message.ClientId);
            try
            {
                await this.Client.SendEventAsync(message.ToEvent(), this.ClientOptions.GetGrpcMetadata(), null,
                    cancellationToken);
                
            }
            catch (RpcException ex) {
                throw new RpcException(ex.Status);
            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }
        }
    }
}