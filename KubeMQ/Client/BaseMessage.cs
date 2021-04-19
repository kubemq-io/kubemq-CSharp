using System;
using System.Collections.Generic;

namespace KubeMQ.Client
{
    public class BaseMessage
    {
        private string _id = "";
        private string _metadata = "";
        private string _channel = "";
        private string _clientId = "";
        private byte[] _body = null;
        private Dictionary<string, string> _tags = null;

        public string Channel
        {
            get => _channel;
            set => _channel = value;
        }

        public string ClientId
        {
            get => _clientId;
            set => _clientId = value;
        }

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public string Metadata
        {
            get => _metadata;
            set => _metadata = value;
        }

        public byte[] Body
        {
            get => _body;
            set => _body = value;
        }

        public Dictionary<string, string> Tags
        {
            get => _tags;
            set => _tags = value;
        }

        public BaseMessage()
        {
            
        }
        public BaseMessage WithId (string id)
        {
            var newBaseMessage = this;
            newBaseMessage._id = id;
            return newBaseMessage;
        }
        public BaseMessage WithChannel (string channel)
        {
            var newBaseMessage = this;
            newBaseMessage._channel = channel;
            return newBaseMessage;
        }
        public BaseMessage WithClientId (string clientId)
        {
            var newBaseMessage = this;
            newBaseMessage._clientId = clientId;
            return newBaseMessage;
        }
        
        public BaseMessage WithMetadata (string metadata)
        {
            var newBaseMessage = this;
            newBaseMessage._metadata = metadata;
            return newBaseMessage;
        }
        
        public BaseMessage WithBody (byte[] body)
        {
            var newBaseMessage = this;
            newBaseMessage._body = body;
            return newBaseMessage;
        }
        public BaseMessage WithTags (Dictionary<string, string> tags)
        {
            var newBaseMessage = this;
            newBaseMessage._tags = tags;
            return newBaseMessage;
        }
    }
}