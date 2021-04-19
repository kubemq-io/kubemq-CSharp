
using System;

namespace PubSub_Publish_to_a_Channel
{
    class Program
    {
        static void Main(string[] args)
        {
            var ChannelName = "testing_event_channel";
            var ClientID = "hello-world-sender";
            var KubeMQServerAddress = "localhost:50000";


            var channel = new KubeMQ.SDK.csharp.Events.Channel(new KubeMQ.SDK.csharp.Events.ChannelParameters
            {
                ChannelName = ChannelName,
                ClientID = ClientID,
                KubeMQAddress = KubeMQServerAddress
            });

            try
            {
                var result = channel.SendEvent(new KubeMQ.SDK.csharp.Events.Event()
                {                  
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending multisubscribers event")
                });
                if (!result.Sent)
                {
                    Console.WriteLine($"Could not send multisubscribers message:{result.Error}");                 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);          
            }
        }
    }
}
