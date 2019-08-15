using System;

namespace RPC_Subscribe_to_a_Channel
{
    class Program
    {
        static void Main(string[] args)
        {

            var ChannelName = "testing_event_channel";
            var ClientID = "hello-world-subscriber";
            var KubeMQServerAddress = "localhost:50000";



            KubeMQ.SDK.csharp.CommandQuery.Responder responder = new KubeMQ.SDK.csharp.CommandQuery.Responder(KubeMQServerAddress);
            try
            {
                responder.SubscribeToRequests(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest()
                {
                    Channel = ChannelName,
                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Commands,
                    ClientID = ClientID
                }, (commandReceive) => {
                    Console.WriteLine($"Command Received: Id:{commandReceive.RequestID} Channel:{commandReceive.Channel} Metadata:{commandReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(commandReceive.Body)} ");
                    return new KubeMQ.SDK.csharp.CommandQuery.Response(commandReceive)
                    {
                        Body = new byte[0],
                        CacheHit = false,
                        Error = "None",
                        ClientID = ClientID,
                        Executed = true,
                        Metadata = string.Empty,
                        Timestamp = DateTime.UtcNow,
                    };

                }, (errorHandler) =>
                {
                    Console.WriteLine(errorHandler.Message);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine("press any key to close RPC_Subscribe_to_a_Channel");
            Console.ReadLine();
        }
    }
}
