
namespace KubeMQ.SDK.csharp.Events
{
    public class Event
    {
        #region Properties
        /// <summary>
        /// Represents a Event identifier.
        /// </summary>
        public string EventID { get; set; }
        /// <summary>
        /// Represents text as System.String.
        /// </summary>
        public string Metadata { get; set; }
        /// <summary>
        /// Represents the content of the KubeMQ.SDK.csharp.Events.Event.
        /// </summary>
        public byte[] Body { get; set; }
        #endregion

        public Event() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="metadata"></param>
        /// <param name="body"></param>
        public Event(string eventID, string metadata, byte[] body)
        {
            EventID = eventID;
            Metadata = metadata;
            Body = body;
        }
    }
}
