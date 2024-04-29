using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.SDK.csharp.Config;

namespace KubeMQ.SDK.csharp.Transport
{
    public class CustomInterceptor : Interceptor
    {
        private readonly Connection opts;

        public CustomInterceptor(Connection opts)
        {
            this.opts = opts;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)        {
            if (!string.IsNullOrEmpty(opts.AuthToken))
            {
                var headers = new Metadata
                {
                    { "authorization", opts.AuthToken }
                };
                context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(headers));
            }
            return continuation(request, context);
        }


        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            if (!string.IsNullOrEmpty(opts.AuthToken))
            {
                context.RequestHeaders.Add("authorization", opts.AuthToken);
            }
            return await continuation(requestStream, context);
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            if (!string.IsNullOrEmpty(opts.AuthToken))
            {
                context.RequestHeaders.Add("authorization", opts.AuthToken);
            }
            await continuation(request, responseStream, context);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            if (!string.IsNullOrEmpty(opts.AuthToken))
            {
                context.RequestHeaders.Add("authorization", opts.AuthToken);
            }
            await continuation(requestStream, responseStream, context);
        }
    }
}