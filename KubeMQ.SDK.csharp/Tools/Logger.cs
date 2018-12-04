using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Tools
{
    class Logger
    {
        public static Microsoft.Extensions.Logging.ILogger InitLogger(Microsoft.Extensions.Logging.ILogger plogger, string pPrefix = null)
        {
            if (plogger == null)
            {
                ILoggerFactory loggerFactory = new LoggerFactory();

                loggerFactory.AddProvider(new KubeConsoleProvider());
                return loggerFactory.CreateLogger(pPrefix?? "KubeMQSDK");
            }
            else
            {
               return plogger;
            }
        }
    }
}
