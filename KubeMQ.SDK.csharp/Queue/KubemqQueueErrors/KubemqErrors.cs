using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace KubeMQ.SDK.csharp.Queue.KubemqQueueErrors
{
    public enum KubemqQueueErrors
    {
        ErrGeneralError = 1,
        ErrInvalidQueueName = 120,
        ErrRegisterQueueSubscription = 121,
        ErrInvalidMaxMessages = 122,
        ErrAckQueueMsg = 123,
        ErrNoCurrentMsgToAck = 124,
        ErrInvalidAckSeq = 125,
        ErrNoCurrentMsgToSend = 126,
        ErrInvalidVisibility = 127,
        ErrSubscriptionIsActive = 128,
        ErrVisibilityExpired = 129,
        ErrSendingQueueMessage = 130,
        ErrInvalidQueueMessage = 131,
        ErrInvalidStreamRequestType = 132,
        ErrInvalidExpiration = 133,
        ErrInvalidMaxReceiveCount = 134,
        ErrInvalidDelay = 135,
        ErrInvalidWaitTimeout = 136,
        ErrNoActiveMessageToReject = 137,
        ErrNoNewMessageQueue = 138,
    }

    internal class KubemqQueueErrorConverter
    {
        internal static KubemqQueueErrors GetQueueError(string errorMsg)
        {
            switch (errorMsg)
            {
                case string a when a.Contains("120"):
                    return KubemqQueueErrors.ErrInvalidQueueName;
                case string a when a.Contains("121"):
                    return KubemqQueueErrors.ErrRegisterQueueSubscription;
                case string a when a.Contains("122"):
                    return KubemqQueueErrors.ErrInvalidMaxMessages;
                case string a when a.Contains("123"):
                    return KubemqQueueErrors.ErrAckQueueMsg;
                case string a when a.Contains("124"):
                    return KubemqQueueErrors.ErrNoCurrentMsgToAck;
                case string a when a.Contains("125"):
                    return KubemqQueueErrors.ErrInvalidAckSeq;
                case string a when a.Contains("126"):
                    return KubemqQueueErrors.ErrNoCurrentMsgToSend;
                case string a when a.Contains("127"):
                    return KubemqQueueErrors.ErrInvalidVisibility;
                case string a when a.Contains("128"):
                    return KubemqQueueErrors.ErrSubscriptionIsActive;
                case string a when a.Contains("129"):
                    return KubemqQueueErrors.ErrVisibilityExpired;
                case string a when a.Contains("130"):
                    return KubemqQueueErrors.ErrSendingQueueMessage;
                case string a when a.Contains("131"):
                    return KubemqQueueErrors.ErrInvalidQueueMessage;
                case string a when a.Contains("132"):
                    return KubemqQueueErrors.ErrInvalidStreamRequestType;
                case string a when a.Contains("133"):
                    return KubemqQueueErrors.ErrInvalidExpiration;
                case string a when a.Contains("134"):
                    return KubemqQueueErrors.ErrInvalidMaxReceiveCount;
                case string a when a.Contains("135"):
                    return KubemqQueueErrors.ErrInvalidDelay;
                case string a when a.Contains("136"):
                    return KubemqQueueErrors.ErrInvalidWaitTimeout;
                case string a when a.Contains("137"):
                    return KubemqQueueErrors.ErrNoActiveMessageToReject;
                case string a when a.Contains("138"):
                    return KubemqQueueErrors.ErrNoNewMessageQueue;
                case null:
                    return KubemqQueueErrors.ErrGeneralError;
            }
            return KubemqQueueErrors.ErrGeneralError;
        }
    }
    


}


