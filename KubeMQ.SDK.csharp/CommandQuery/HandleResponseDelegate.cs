namespace KubeMQ.SDK.csharp.CommandQuery
{
    /// <summary>
    /// Represents a delegate that receive KubeMQ.SDK.csharp.RequestReply.Response.
    /// </summary>
    /// <param name="response"></param>
    public delegate void HandleResponseDelegate(Response response);
}
