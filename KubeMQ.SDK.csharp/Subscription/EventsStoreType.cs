
namespace KubeMQ.SDK.csharp.Subscription
{
    public enum EventsStoreType
    {
        Undefined = 0,
        StartNewOnly = 1,
        StartFromFirst = 2,
        StartFromLast = 3,
        StartAtSequence = 4,
        StartAtTime = 5,
        StartAtTimeDelta = 6,
        StartFromLastDelivered = 7
    }
}
