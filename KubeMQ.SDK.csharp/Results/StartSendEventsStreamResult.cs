using System;

namespace KubeMQ.SDK.csharp.Results
{
    /// <summary>
    /// Represents the result of a StartSendEventsStream.
    /// </summary>
    public class StartSendEventsStreamResult:Result
    {
        public StartSendEventsStreamResult() : base()
        {
        }

        public StartSendEventsStreamResult(string errorMessage) : base(errorMessage)
        {
        }

        public StartSendEventsStreamResult(Exception e) : base(e)
        {
        }
    }
    
}