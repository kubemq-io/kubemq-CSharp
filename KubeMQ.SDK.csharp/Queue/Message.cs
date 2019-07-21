using System.Collections.Generic;

namespace KubeMQ.SDK.csharp.Queue
{
    public class Message {
        private static int _id = 0;
        public string MessageID { get ; set; }
        public string Metadata { get; set; }
        public IEnumerable < (string, string) > Tags { get; set; }
        public byte[] Body { get; set; }      
        public Message () {

        }
        public Message (byte[] body, string metadata, string messageID = null, IEnumerable < (string, string) > tags = null) {
            MessageID = string.IsNullOrEmpty (messageID) ? Tools.IDGenerator.ReqID.Getid() : messageID;
            Metadata = metadata;
            Tags = tags;
            Body = body;
        }


      

    }
}