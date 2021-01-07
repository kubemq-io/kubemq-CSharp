using Microsoft.Extensions.Logging;
using System;
using KubeMQ.SDK.csharp.CommandQuery;
using KubeMQ.SDK.csharp.Events;
using KubeMQ.SDK.csharp.Subscription;
using KubeMQ.SDK.csharp.Tools;

namespace CommonExample
{
    public abstract class BaseExample
    {
        protected ILogger logger;
        protected string ChannelName { get; set; }
        protected string ClientID { get; set; }
        protected int Timeout { get; set; }
        public BaseExample(string _ClientID)
        {
            ClientID = _ClientID;
            Timeout = 111000;
            ChannelName = "MyTestChannelName";
            InitLogger();
        }

        protected void InitLogger()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            logger = loggerFactory.CreateLogger("PubsubSubscriber");

        }

        protected Event CreateNewEvent()
        {
            logger.LogDebug("Start Creating Event");
            Event @event = new Event()
            {
                Metadata = "EventMetaData",
                Body = Converter.ToByteArray($"Event Created on time {DateTime.UtcNow}"),
            };
            return @event;
        }

        protected KubeMQ.SDK.csharp.Events.LowLevel.Event CreateLowLevelEventWithoutStore()
        {
            logger.LogDebug("Start Creating Event");
            KubeMQ.SDK.csharp.Events.LowLevel.Event @event = new KubeMQ.SDK.csharp.Events.LowLevel.Event()
            {
                Metadata = "EventMetaData",
                Body = Converter.ToByteArray($"Event Created on time {DateTime.UtcNow}"),
                Store=false,
                Channel= ChannelName,
                ClientID=this.ClientID,
                ReturnResult=false,
                Tags=new System.Collections.Generic.Dictionary<string, string>()
                {
                    {"FirstTag","FirstValue" },
                    {"SecondTag","SecondValue" }
                }
            };
            return @event;
        }

        protected KubeMQ.SDK.csharp.Events.LowLevel.Event CreateLowLevelEventWithoutStoreUtf8()
        {
            logger.LogDebug("Start Creating Event");
            KubeMQ.SDK.csharp.Events.LowLevel.Event @event = new KubeMQ.SDK.csharp.Events.LowLevel.Event()
            {
                Metadata = "EventMetaData",
                Body = Converter.ToUTF8($"Event Created on time {DateTime.UtcNow}"),
                Store = false,
                Channel = ChannelName,
                ClientID = this.ClientID,
                ReturnResult = false,
                Tags = new System.Collections.Generic.Dictionary<string, string>()
                {
                    {"FirstTag","FirstValue" },
                    {"SecondTag","SecondValue" }
                }
            };
            return @event;
        }

        protected SubscribeRequest CreateSubscribeRequest(SubscribeType subscriptionType= SubscribeType.SubscribeTypeUndefined, 
            EventsStoreType eventsStoreType = EventsStoreType.Undefined,
            int TypeValue=0,string group="")
        {
            Random random = new Random();
            SubscribeRequest subscribeRequest = new SubscribeRequest()
            {
                Channel = ChannelName,
                ClientID = random.Next(9, 19999).ToString(),
                EventsStoreType = eventsStoreType,
                EventsStoreTypeValue = TypeValue,
                Group = group,
                SubscribeType = subscriptionType
            };
            return subscribeRequest;
        }
        protected KubeMQ.SDK.csharp.CommandQuery.LowLevel.Request CreateLowLevelRequest(RequestType requestType)
        {
            return new KubeMQ.SDK.csharp.CommandQuery.LowLevel.Request
            {
                Body = Converter.ToByteArray("Request"),
                Metadata = "MyMetaData",
                CacheKey="",
                CacheTTL=0,
                Channel=this.ChannelName,
                ClientID=this.ClientID,
                Timeout=this.Timeout,
                RequestType=requestType,
                Tags= new System.Collections.Generic.Dictionary<string, string>()
                {
                    {"FirstTag","FirstValue" },
                    {"SecondTag","SecondValue" }
                }
            };
        }
    }
}
