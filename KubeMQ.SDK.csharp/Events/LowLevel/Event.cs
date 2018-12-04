using System.Threading;
using KubeMQ.SDK.csharp.Tools;
using InnerEvent = KubeMQ.Grpc.Event;


namespace KubeMQ.SDK.csharp.Events.LowLevel
{
    public class Event
    {
        #region Properties
        private static int _id = 0;
        /// <summary>
        /// Represents The channel name to send to using the KubeMQ .
        /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// Represents text as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents The content of the KubeMQ.SDK.csharp.PubSub.LowLevel.Event.
        /// </summary>
        public byte[] Body { get; set; }
        /// <summary>
        /// Represents a Event identifier.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Represents the sender ID that the events will be send under.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// Represents if the events should be send to persistence.
        /// </summary>
        public bool Store { get; set; }

        public bool ReturnResult { get; set; }

        #endregion

        public Event() { }

        public Event(string channel, string metadata, byte[] body, string eventID, string clientID, bool store)
        {
            Channel = channel;
            Metadata = metadata;
            Body = body;
            EventID = eventID;
            ClientID = clientID;
            Store = store;
        }

        internal Event(InnerEvent innerEvent)
        {
            Channel = innerEvent.Channel;
            Metadata = innerEvent.Metadata;
            Body = innerEvent.Body.ToByteArray();

            EventID = string.IsNullOrEmpty(innerEvent.EventID) ? GetNextId().ToString() : innerEvent.EventID;
            ClientID = innerEvent.ClientID;
            Store = innerEvent.Store;
        }


        internal InnerEvent ToInnerEvent()
        {
            return new InnerEvent()
            {
                Channel = this.Channel,
                Metadata = this.Metadata ?? string.Empty,
                Body = Converter.ToByteString(this.Body),

                EventID = string.IsNullOrEmpty(this.EventID) ? GetNextId().ToString() : EventID,
                ClientID = this.ClientID,
                Store = this.Store
            };
        }

        /// <summary>
        /// Get an unique thread safety ID between 1 to 65535
        /// </summary>
        /// <returns></returns>
        private int GetNextId()
        {
            //return Interlocked.Increment(ref _id);

            int temp, temp2;

            do
            {
                temp = _id;
                temp2 = temp == ushort.MaxValue ? 1 : temp + 1;
            }
            while (Interlocked.CompareExchange(ref _id, temp2, temp) != temp);
            return _id;
        }
    }
}
