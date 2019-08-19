using System;

namespace PubSub_Subscribe_to_a_Channel
{
    class Program
    {
        static void Main(string[] args)
        {

            var ChannelName = "testing_event_channel";
            var ClientID = "hello-world-subscriber";
            var KubeMQServerAddress = "localhost:50000";
     
            var  subscriber = new KubeMQ.SDK.csharp.Events.Subscriber(KubeMQServerAddress);
            try
            {
                subscriber.SubscribeToEvents(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest
                {
                    Channel = ChannelName,
                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Events,
                    ClientID = ClientID

                }, (eventReceive) =>
                {
           
                    Console.WriteLine($"Event Received: EventID:{eventReceive.EventID} Channel:{eventReceive.Channel} Metadata:{eventReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(eventReceive.Body)} ");
                },
                (errorHandler) =>                 
                {
                    Console.WriteLine(errorHandler.Message);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine("press any key to close PubSub_Subscribe_to_a_Channel");
            Console.ReadLine();
        }  
        
    }
}
