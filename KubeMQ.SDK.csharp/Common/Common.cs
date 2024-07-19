using System;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Results;
using static KubeMQ.Grpc.kubemq;
using Result = KubeMQ.SDK.csharp.Results.Result;


namespace KubeMQ.SDK.csharp.Common
{
    public static class Common
{
    private static readonly string RequestChannel = "kubemq.cluster.internal.requests";
    public static async Task<Result> CreateDeleteChannel(kubemqClient client, string clientId,
        string channelName, string channelType, bool isCreate)
    {
        var request = CreateRequest(clientId, channelType, channelName);
        request.Metadata = isCreate ? "create-channel" : "delete-channel";
        return await ExecuteRequest(client, request);
    }

    private static Request CreateRequest(string clientId, string channelType, string channelName)
    {
        return new Request
        {
            RequestID = Guid.NewGuid().ToString(),
            ClientID = clientId,
            RequestTypeData = Request.Types.RequestType.Query,
            Channel = RequestChannel,
            Timeout = 10000,
            Tags =  { { "channel_type", channelType },
            { "channel", channelName },
            { "client_id", clientId }} ,
        };
    }

    private static async Task<Result> ExecuteRequest(kubemqClient client, Request request)
    {
        try
        {
            Response response = await client.SendRequestAsync(request);
            if (!string.IsNullOrEmpty(response.Error))
            {
                return new Result(response.Error);
            }
            else
            {
                return new Result();
            }
        }
        catch (Exception e)
        {
            return new Result(e);

        }
    }

    private static async Task<Response> List(kubemqClient client, string clientId, string search, string channelType)
    {
        var request = new Request
        {
            RequestID = Guid.NewGuid().ToString(),
            RequestTypeData = Request.Types.RequestType.Query,
            Channel = RequestChannel,
            ClientID = clientId,
            Timeout = 10000,
            Metadata = ("list-channels"),
            Tags = { { "channel_type", channelType }, { "search", search }, { "client_id", clientId } }
        };
        return await client.SendRequestAsync(request);
    }

    public static async Task<ListCqAsyncResult> ListCqChannels(kubemqClient client, string clientId, string search, string channelType)
    {
        return HandleListErrors<ListCqAsyncResult>(await List(client, clientId, search, channelType));
    }

    public static async Task<ListPubSubAsyncResult> ListPubSubChannels(kubemqClient client, string clientId, string search, string channelType)
    {
        return HandleListErrors<ListPubSubAsyncResult>(await List(client, clientId, search, channelType));
    }

    public static async Task<ListQueuesAsyncResult> ListQueuesChannels(kubemqClient client, string clientId, string search, string channelType)
    {
        return HandleListErrors<ListQueuesAsyncResult>(await List(client, clientId, search, channelType));
    }

    private static dynamic HandleListErrors<T>(Response response)
    {
        if (!string.IsNullOrEmpty(response.Error))
        {
            return Activator.CreateInstance(typeof(T), new object[] { null, false, response.Error });
        }
        else
        {
            return Activator.CreateInstance(typeof(T), new object[] { response.Body.ToByteArray(), true, "" });
        }
    }
}
}

