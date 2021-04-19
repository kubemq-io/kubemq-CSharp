using KubeMQ.Client;
using KubeMQ.Grpc;
using KubeMQ.Utils;

namespace KubeMQ.Events
{
    public class EventsMessage :BaseMessage
    {
        public Event ToEvent ()
        {
            return  new Event
            {
                EventID = this.Id,
                ClientID = this.ClientId,
                Channel = this.Channel,
                Metadata = this.Metadata,
                Body = Convertors.FromByteArray(this.Body),
                Store = false,
                Tags = { Convertors.ToMapFields(this.Tags)}
            };

        }
    }
    
}