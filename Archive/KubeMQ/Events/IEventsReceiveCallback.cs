namespace KubeMQ.Events
{
    public interface IEventsReceiveCallback
    {
        void ReceiveEventsMessage(EventsReceiveMessage message );
        void ReceiveEventsError(string error );
    }
}