// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: kubemq.proto
#pragma warning disable 1591
#region Designer generated code

using System;
using System.Threading;
using System.Threading.Tasks;
using grpc = global::Grpc.Core;

namespace KubeMQ.Grpc {
  public static partial class kubemq
  {
    static readonly string __ServiceName = "kubemq.kubemq";

    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Event> __Marshaller_Event = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Event.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Result> __Marshaller_Result = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Result.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Subscribe> __Marshaller_Subscribe = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Subscribe.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.EventReceive> __Marshaller_EventReceive = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.EventReceive.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Request> __Marshaller_Request = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Request.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Response> __Marshaller_Response = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Response.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.Empty> __Marshaller_Empty = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.Empty.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.QueueMessage> __Marshaller_QueueMessage = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.QueueMessage.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.SendQueueMessageResult> __Marshaller_SendQueueMessageResult = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.SendQueueMessageResult.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.QueueMessagesBatchRequest> __Marshaller_QueueMessagesBatchRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.QueueMessagesBatchRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.QueueMessagesBatchResponse> __Marshaller_QueueMessagesBatchResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.QueueMessagesBatchResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.ReceiveQueueMessagesRequest> __Marshaller_ReceiveQueueMessagesRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.ReceiveQueueMessagesRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.ReceiveQueueMessagesResponse> __Marshaller_ReceiveQueueMessagesResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.ReceiveQueueMessagesResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.StreamQueueMessagesRequest> __Marshaller_StreamQueueMessagesRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.StreamQueueMessagesRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.StreamQueueMessagesResponse> __Marshaller_StreamQueueMessagesResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.StreamQueueMessagesResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.PeakQueueMessageRequest> __Marshaller_PeakQueueMessageRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.PeakQueueMessageRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.PeakQueueMessageResponse> __Marshaller_PeakQueueMessageResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.PeakQueueMessageResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.AckAllQueueMessagesRequest> __Marshaller_AckAllQueueMessagesRequest = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.AckAllQueueMessagesRequest.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.AckAllQueueMessagesResponse> __Marshaller_AckAllQueueMessagesResponse = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.AckAllQueueMessagesResponse.Parser.ParseFrom);
    static readonly grpc::Marshaller<global::KubeMQ.Grpc.PingResult> __Marshaller_PingResult = grpc::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::KubeMQ.Grpc.PingResult.Parser.ParseFrom);

    static readonly grpc::Method<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result> __Method_SendEvent = new grpc::Method<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SendEvent",
        __Marshaller_Event,
        __Marshaller_Result);

    static readonly grpc::Method<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result> __Method_SendEventsStream = new grpc::Method<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result>(
        grpc::MethodType.DuplexStreaming,
        __ServiceName,
        "SendEventsStream",
        __Marshaller_Event,
        __Marshaller_Result);

    static readonly grpc::Method<global::KubeMQ.Grpc.Subscribe, global::KubeMQ.Grpc.EventReceive> __Method_SubscribeToEvents = new grpc::Method<global::KubeMQ.Grpc.Subscribe, global::KubeMQ.Grpc.EventReceive>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "SubscribeToEvents",
        __Marshaller_Subscribe,
        __Marshaller_EventReceive);

    static readonly grpc::Method<global::KubeMQ.Grpc.Subscribe, global::KubeMQ.Grpc.Request> __Method_SubscribeToRequests = new grpc::Method<global::KubeMQ.Grpc.Subscribe, global::KubeMQ.Grpc.Request>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "SubscribeToRequests",
        __Marshaller_Subscribe,
        __Marshaller_Request);

    static readonly grpc::Method<global::KubeMQ.Grpc.Request, global::KubeMQ.Grpc.Response> __Method_SendRequest = new grpc::Method<global::KubeMQ.Grpc.Request, global::KubeMQ.Grpc.Response>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SendRequest",
        __Marshaller_Request,
        __Marshaller_Response);

    static readonly grpc::Method<global::KubeMQ.Grpc.Response, global::KubeMQ.Grpc.Empty> __Method_SendResponse = new grpc::Method<global::KubeMQ.Grpc.Response, global::KubeMQ.Grpc.Empty>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SendResponse",
        __Marshaller_Response,
        __Marshaller_Empty);

    static readonly grpc::Method<global::KubeMQ.Grpc.QueueMessage, global::KubeMQ.Grpc.SendQueueMessageResult> __Method_SendQueueMessage = new grpc::Method<global::KubeMQ.Grpc.QueueMessage, global::KubeMQ.Grpc.SendQueueMessageResult>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SendQueueMessage",
        __Marshaller_QueueMessage,
        __Marshaller_SendQueueMessageResult);

    static readonly grpc::Method<global::KubeMQ.Grpc.QueueMessagesBatchRequest, global::KubeMQ.Grpc.QueueMessagesBatchResponse> __Method_SendQueueMessagesBatch = new grpc::Method<global::KubeMQ.Grpc.QueueMessagesBatchRequest, global::KubeMQ.Grpc.QueueMessagesBatchResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SendQueueMessagesBatch",
        __Marshaller_QueueMessagesBatchRequest,
        __Marshaller_QueueMessagesBatchResponse);

    static readonly grpc::Method<global::KubeMQ.Grpc.ReceiveQueueMessagesRequest, global::KubeMQ.Grpc.ReceiveQueueMessagesResponse> __Method_ReceiveQueueMessages = new grpc::Method<global::KubeMQ.Grpc.ReceiveQueueMessagesRequest, global::KubeMQ.Grpc.ReceiveQueueMessagesResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "ReceiveQueueMessages",
        __Marshaller_ReceiveQueueMessagesRequest,
        __Marshaller_ReceiveQueueMessagesResponse);

    static readonly grpc::Method<global::KubeMQ.Grpc.StreamQueueMessagesRequest, global::KubeMQ.Grpc.StreamQueueMessagesResponse> __Method_StreamQueueMessage = new grpc::Method<global::KubeMQ.Grpc.StreamQueueMessagesRequest, global::KubeMQ.Grpc.StreamQueueMessagesResponse>(
        grpc::MethodType.DuplexStreaming,
        __ServiceName,
        "StreamQueueMessage",
        __Marshaller_StreamQueueMessagesRequest,
        __Marshaller_StreamQueueMessagesResponse);

    static readonly grpc::Method<global::KubeMQ.Grpc.PeakQueueMessageRequest, global::KubeMQ.Grpc.PeakQueueMessageResponse> __Method_PeakQueueMessage = new grpc::Method<global::KubeMQ.Grpc.PeakQueueMessageRequest, global::KubeMQ.Grpc.PeakQueueMessageResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "PeakQueueMessage",
        __Marshaller_PeakQueueMessageRequest,
        __Marshaller_PeakQueueMessageResponse);

    static readonly grpc::Method<global::KubeMQ.Grpc.AckAllQueueMessagesRequest, global::KubeMQ.Grpc.AckAllQueueMessagesResponse> __Method_AckAllQueueMessages = new grpc::Method<global::KubeMQ.Grpc.AckAllQueueMessagesRequest, global::KubeMQ.Grpc.AckAllQueueMessagesResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "AckAllQueueMessages",
        __Marshaller_AckAllQueueMessagesRequest,
        __Marshaller_AckAllQueueMessagesResponse);

    static readonly grpc::Method<global::KubeMQ.Grpc.Empty, global::KubeMQ.Grpc.PingResult> __Method_Ping = new grpc::Method<global::KubeMQ.Grpc.Empty, global::KubeMQ.Grpc.PingResult>(
        grpc::MethodType.Unary,
        __ServiceName,
        "Ping",
        __Marshaller_Empty,
        __Marshaller_PingResult);

    /// <summary>Service descriptor</summary>
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::KubeMQ.Grpc.KubemqReflection.Descriptor.Services[0]; }
    }

    /// <summary>Base class for server-side implementations of kubemq</summary>
    public abstract partial class kubemqBase
    {
      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.Result> SendEvent(global::KubeMQ.Grpc.Event request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task SendEventsStream(grpc::IAsyncStreamReader<global::KubeMQ.Grpc.Event> requestStream, grpc::IServerStreamWriter<global::KubeMQ.Grpc.Result> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task SubscribeToEvents(global::KubeMQ.Grpc.Subscribe request, grpc::IServerStreamWriter<global::KubeMQ.Grpc.EventReceive> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task SubscribeToRequests(global::KubeMQ.Grpc.Subscribe request, grpc::IServerStreamWriter<global::KubeMQ.Grpc.Request> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.Response> SendRequest(global::KubeMQ.Grpc.Request request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.Empty> SendResponse(global::KubeMQ.Grpc.Response request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      /// <summary>
      ///Snend dfsf
      /// </summary>
      /// <param name="request">The request received from the client.</param>
      /// <param name="context">The context of the server-side call handler being invoked.</param>
      /// <returns>The response to send back to the client (wrapped by a task).</returns>
      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.SendQueueMessageResult> SendQueueMessage(global::KubeMQ.Grpc.QueueMessage request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.QueueMessagesBatchResponse> SendQueueMessagesBatch(global::KubeMQ.Grpc.QueueMessagesBatchRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.ReceiveQueueMessagesResponse> ReceiveQueueMessages(global::KubeMQ.Grpc.ReceiveQueueMessagesRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task StreamQueueMessage(grpc::IAsyncStreamReader<global::KubeMQ.Grpc.StreamQueueMessagesRequest> requestStream, grpc::IServerStreamWriter<global::KubeMQ.Grpc.StreamQueueMessagesResponse> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.PeakQueueMessageResponse> PeakQueueMessage(global::KubeMQ.Grpc.PeakQueueMessageRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.AckAllQueueMessagesResponse> AckAllQueueMessages(global::KubeMQ.Grpc.AckAllQueueMessagesRequest request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      public virtual global::System.Threading.Tasks.Task<global::KubeMQ.Grpc.PingResult> Ping(global::KubeMQ.Grpc.Empty request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

    }

    /// <summary>Client for kubemq</summary>
    public partial class kubemqClient : grpc::ClientBase<kubemqClient>
    {
      /// <summary>Creates a new client for kubemq</summary>
      /// <param name="channel">The channel to use to make remote calls.</param>
      public kubemqClient(grpc::Channel channel) : base(channel)
      {
      }
      /// <summary>Creates a new client for kubemq that uses a custom <c>CallInvoker</c>.</summary>
      /// <param name="callInvoker">The callInvoker to use to make remote calls.</param>
      public kubemqClient(grpc::CallInvoker callInvoker) : base(callInvoker)
      {
      }
      /// <summary>Protected parameterless constructor to allow creation of test doubles.</summary>
      protected kubemqClient() : base()
      {
      }
      /// <summary>Protected constructor to allow creation of configured clients.</summary>
      /// <param name="configuration">The client configuration.</param>
      protected kubemqClient(ClientBaseConfiguration configuration) : base(configuration)
      {
      }

      public virtual global::KubeMQ.Grpc.Result SendEvent(global::KubeMQ.Grpc.Event request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendEvent(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.Result SendEvent(global::KubeMQ.Grpc.Event request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SendEvent, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Result> SendEventAsync(global::KubeMQ.Grpc.Event request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendEventAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Result> SendEventAsync(global::KubeMQ.Grpc.Event request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SendEvent, null, options, request);
      }
      public virtual grpc::AsyncDuplexStreamingCall<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result> SendEventsStream(grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendEventsStream(new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncDuplexStreamingCall<global::KubeMQ.Grpc.Event, global::KubeMQ.Grpc.Result> SendEventsStream(grpc::CallOptions options)
      {
        return CallInvoker.AsyncDuplexStreamingCall(__Method_SendEventsStream, null, options);
      }
      public virtual grpc::AsyncServerStreamingCall<global::KubeMQ.Grpc.EventReceive> SubscribeToEvents(global::KubeMQ.Grpc.Subscribe request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SubscribeToEvents(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncServerStreamingCall<global::KubeMQ.Grpc.EventReceive> SubscribeToEvents(global::KubeMQ.Grpc.Subscribe request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncServerStreamingCall(__Method_SubscribeToEvents, null, options, request);
      }
      public virtual grpc::AsyncServerStreamingCall<global::KubeMQ.Grpc.Request> SubscribeToRequests(global::KubeMQ.Grpc.Subscribe request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SubscribeToRequests(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncServerStreamingCall<global::KubeMQ.Grpc.Request> SubscribeToRequests(global::KubeMQ.Grpc.Subscribe request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncServerStreamingCall(__Method_SubscribeToRequests, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.Response SendRequest(global::KubeMQ.Grpc.Request request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendRequest(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.Response SendRequest(global::KubeMQ.Grpc.Request request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SendRequest, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Response> SendRequestAsync(global::KubeMQ.Grpc.Request request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendRequestAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Response> SendRequestAsync(global::KubeMQ.Grpc.Request request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SendRequest, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.Empty SendResponse(global::KubeMQ.Grpc.Response request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendResponse(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.Empty SendResponse(global::KubeMQ.Grpc.Response request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SendResponse, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Empty> SendResponseAsync(global::KubeMQ.Grpc.Response request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendResponseAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.Empty> SendResponseAsync(global::KubeMQ.Grpc.Response request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SendResponse, null, options, request);
      }
      /// <summary>
      ///Snend dfsf
      /// </summary>
      /// <param name="request">The request to send to the server.</param>
      /// <param name="headers">The initial metadata to send with the call. This parameter is optional.</param>
      /// <param name="deadline">An optional deadline for the call. The call will be cancelled if deadline is hit.</param>
      /// <param name="cancellationToken">An optional token for canceling the call.</param>
      /// <returns>The response received from the server.</returns>
      public virtual global::KubeMQ.Grpc.SendQueueMessageResult SendQueueMessage(global::KubeMQ.Grpc.QueueMessage request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendQueueMessage(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      /// <summary>
      ///Snend dfsf
      /// </summary>
      /// <param name="request">The request to send to the server.</param>
      /// <param name="options">The options for the call.</param>
      /// <returns>The response received from the server.</returns>
      public virtual global::KubeMQ.Grpc.SendQueueMessageResult SendQueueMessage(global::KubeMQ.Grpc.QueueMessage request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SendQueueMessage, null, options, request);
      }
      /// <summary>
      ///Snend dfsf
      /// </summary>
      /// <param name="request">The request to send to the server.</param>
      /// <param name="headers">The initial metadata to send with the call. This parameter is optional.</param>
      /// <param name="deadline">An optional deadline for the call. The call will be cancelled if deadline is hit.</param>
      /// <param name="cancellationToken">An optional token for canceling the call.</param>
      /// <returns>The call object.</returns>
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.SendQueueMessageResult> SendQueueMessageAsync(global::KubeMQ.Grpc.QueueMessage request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendQueueMessageAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      /// <summary>
      ///Snend dfsf
      /// </summary>
      /// <param name="request">The request to send to the server.</param>
      /// <param name="options">The options for the call.</param>
      /// <returns>The call object.</returns>
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.SendQueueMessageResult> SendQueueMessageAsync(global::KubeMQ.Grpc.QueueMessage request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SendQueueMessage, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.QueueMessagesBatchResponse SendQueueMessagesBatch(global::KubeMQ.Grpc.QueueMessagesBatchRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendQueueMessagesBatch(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.QueueMessagesBatchResponse SendQueueMessagesBatch(global::KubeMQ.Grpc.QueueMessagesBatchRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SendQueueMessagesBatch, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.QueueMessagesBatchResponse> SendQueueMessagesBatchAsync(global::KubeMQ.Grpc.QueueMessagesBatchRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return SendQueueMessagesBatchAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.QueueMessagesBatchResponse> SendQueueMessagesBatchAsync(global::KubeMQ.Grpc.QueueMessagesBatchRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SendQueueMessagesBatch, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.ReceiveQueueMessagesResponse ReceiveQueueMessages(global::KubeMQ.Grpc.ReceiveQueueMessagesRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return ReceiveQueueMessages(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.ReceiveQueueMessagesResponse ReceiveQueueMessages(global::KubeMQ.Grpc.ReceiveQueueMessagesRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_ReceiveQueueMessages, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.ReceiveQueueMessagesResponse> ReceiveQueueMessagesAsync(global::KubeMQ.Grpc.ReceiveQueueMessagesRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return ReceiveQueueMessagesAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.ReceiveQueueMessagesResponse> ReceiveQueueMessagesAsync(global::KubeMQ.Grpc.ReceiveQueueMessagesRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_ReceiveQueueMessages, null, options, request);
      }
      public virtual grpc::AsyncDuplexStreamingCall<global::KubeMQ.Grpc.StreamQueueMessagesRequest, global::KubeMQ.Grpc.StreamQueueMessagesResponse> StreamQueueMessage(grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return StreamQueueMessage(new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncDuplexStreamingCall<global::KubeMQ.Grpc.StreamQueueMessagesRequest, global::KubeMQ.Grpc.StreamQueueMessagesResponse> StreamQueueMessage(grpc::CallOptions options)
      {
        return CallInvoker.AsyncDuplexStreamingCall(__Method_StreamQueueMessage, null, options);
      }
      public virtual global::KubeMQ.Grpc.PeakQueueMessageResponse PeakQueueMessage(global::KubeMQ.Grpc.PeakQueueMessageRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return PeakQueueMessage(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.PeakQueueMessageResponse PeakQueueMessage(global::KubeMQ.Grpc.PeakQueueMessageRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_PeakQueueMessage, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.PeakQueueMessageResponse> PeakQueueMessageAsync(global::KubeMQ.Grpc.PeakQueueMessageRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return PeakQueueMessageAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.PeakQueueMessageResponse> PeakQueueMessageAsync(global::KubeMQ.Grpc.PeakQueueMessageRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_PeakQueueMessage, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.AckAllQueueMessagesResponse AckAllQueueMessages(global::KubeMQ.Grpc.AckAllQueueMessagesRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return AckAllQueueMessages(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.AckAllQueueMessagesResponse AckAllQueueMessages(global::KubeMQ.Grpc.AckAllQueueMessagesRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_AckAllQueueMessages, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.AckAllQueueMessagesResponse> AckAllQueueMessagesAsync(global::KubeMQ.Grpc.AckAllQueueMessagesRequest request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return AckAllQueueMessagesAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.AckAllQueueMessagesResponse> AckAllQueueMessagesAsync(global::KubeMQ.Grpc.AckAllQueueMessagesRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_AckAllQueueMessages, null, options, request);
      }
      public virtual global::KubeMQ.Grpc.PingResult Ping(global::KubeMQ.Grpc.Empty request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return Ping(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual global::KubeMQ.Grpc.PingResult Ping(global::KubeMQ.Grpc.Empty request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_Ping, null, options, request);
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.PingResult> PingAsync(global::KubeMQ.Grpc.Empty request, grpc::Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default(CancellationToken))
      {
        return PingAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      public virtual grpc::AsyncUnaryCall<global::KubeMQ.Grpc.PingResult> PingAsync(global::KubeMQ.Grpc.Empty request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_Ping, null, options, request);
      }
      /// <summary>Creates a new instance of client from given <c>ClientBaseConfiguration</c>.</summary>
      protected override kubemqClient NewInstance(ClientBaseConfiguration configuration)
      {
        return new kubemqClient(configuration);
      }
    }

    /// <summary>Creates service definition that can be registered with a server</summary>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    public static grpc::ServerServiceDefinition BindService(kubemqBase serviceImpl)
    {
      return grpc::ServerServiceDefinition.CreateBuilder()
          .AddMethod(__Method_SendEvent, serviceImpl.SendEvent)
          .AddMethod(__Method_SendEventsStream, serviceImpl.SendEventsStream)
          .AddMethod(__Method_SubscribeToEvents, serviceImpl.SubscribeToEvents)
          .AddMethod(__Method_SubscribeToRequests, serviceImpl.SubscribeToRequests)
          .AddMethod(__Method_SendRequest, serviceImpl.SendRequest)
          .AddMethod(__Method_SendResponse, serviceImpl.SendResponse)
          .AddMethod(__Method_SendQueueMessage, serviceImpl.SendQueueMessage)
          .AddMethod(__Method_SendQueueMessagesBatch, serviceImpl.SendQueueMessagesBatch)
          .AddMethod(__Method_ReceiveQueueMessages, serviceImpl.ReceiveQueueMessages)
          .AddMethod(__Method_StreamQueueMessage, serviceImpl.StreamQueueMessage)
          .AddMethod(__Method_PeakQueueMessage, serviceImpl.PeakQueueMessage)
          .AddMethod(__Method_AckAllQueueMessages, serviceImpl.AckAllQueueMessages)
          .AddMethod(__Method_Ping, serviceImpl.Ping).Build();
    }

  }
}
#endregion
