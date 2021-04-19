using System;
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

        public void Validate()
        {
            base.Validate();
            if (string.IsNullOrEmpty(ClientId))
            {
                throw new ArgumentNullException( nameof(ClientId),"message must have a clientId value");
            }
        }
    }
    
}