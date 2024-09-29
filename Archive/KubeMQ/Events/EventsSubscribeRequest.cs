using System;
using KubeMQ.Grpc;
namespace KubeMQ.Events
{
    public class EventsSubscribeRequest
    {
        private string _requestId = Guid.NewGuid().ToString();
        private string _channel = "";
        private string _clientId = "";
        private string _group = "";

        public string RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

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

        public string Group
        {
            get => _group;
            set => _group = value;
        }

        public EventsSubscribeRequest()
        {
        }
        public EventsSubscribeRequest(string channel , string @group = "", string @clientId = "")
        {
            _channel = !string.IsNullOrEmpty(channel) ? channel: throw new ArgumentNullException(nameof(channel));
            _group = @group;
            _clientId = @clientId;
        }

        public EventsSubscribeRequest(string channel)
        {
            _channel = !string.IsNullOrEmpty(channel) ? channel: throw new ArgumentNullException(nameof(channel));
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(_channel))
            {
                throw new ArgumentNullException( nameof(Channel),"request must have a channel value");
            }
        }

        public Subscribe ToSubscribeRequest()
        {
            return new Subscribe()
            {
                Channel = _channel,
                Group = _group,
                ClientID =  _clientId,
                SubscribeTypeData = Subscribe.Types.SubscribeType.Events,
            };
        }

     
    }
}