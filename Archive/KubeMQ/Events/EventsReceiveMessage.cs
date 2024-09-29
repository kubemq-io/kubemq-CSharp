using KubeMQ.Client;
using KubeMQ.Grpc;
using KubeMQ.Utils;

namespace KubeMQ.Events
{
    public class EventsReceiveMessage :BaseMessage
    {
        public EventsReceiveMessage(EventReceive message)
        {
            this.Id = message.EventID;
            this.Channel = message.Channel;
            this.Body = Convertors.ToByteArray(message.Body);
            this.Metadata = message.Metadata;
            this.Tags = Convertors.FromMapFields(message.Tags);
        }
        
    }
    
}