using CommonExample;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.CommandQuery;
using KubeMQ.SDK.csharp.Subscription;
using KubeMQ.SDK.csharp.Tools;

namespace CommandQueryResponder
{
    public class CommandQueryResponder : BaseExample
    {
        private Responder responder;
        public CommandQueryResponder(): base("CommandQueryResponder")
        {
            SubcribeToRequest();
        }

        private void SubcribeToRequest()
        {
            responder = new Responder(logger);
            try
            {
                CreateSubscribeToQueries();
                CreateSubscribeToCommands();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Responder SubscribeToRequests EXCEPTION:{ex.Message}");
            }
            Console.ReadKey();
        }

        private void CreateSubscribeToQueries()
        {
            SubscribeRequest subscribeRequest = CreateSubscribeRequest(SubscribeType.Queries);
            responder.SubscribeToRequests(subscribeRequest, HandleIncomingRequests);
        }

        private void CreateSubscribeToCommands()
        {
            SubscribeRequest subscribeRequest = CreateSubscribeRequest(SubscribeType.Commands);
            responder.SubscribeToRequests(subscribeRequest, HandleIncomingRequests);
        }

        private Response HandleIncomingRequests(RequestReceive request)
        {
            if (request != null)
            {
                string strMsg = string.Empty;
                object body = KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(request.Body);

                logger.LogInformation($"Subscriber Received request: Metadata:'{request.Metadata}', Channel:'{request.Channel}', Body:'{strMsg}'");
            }
            Response response = new Response(request)
            {
                Body = Converter.ToByteArray("OK"),
                CacheHit = false,
                Error = "None",
                ClientID = this.ClientID,
                Executed = true,
                Metadata = "OK",
                Timestamp = DateTime.UtcNow,
            };
            return response;
        }
    }
}
