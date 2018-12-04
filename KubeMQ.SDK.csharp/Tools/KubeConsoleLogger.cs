using System;
using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Tools
{
    public class KubeConsoleLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {          
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }
            var message = formatter(state, exception);
            var exceptionString = exception != null ? $"Exception:{exception},": string.Empty;
            Console.WriteLine($"{DateTime.UtcNow.ToFileTime()}|[KubeMQ]|{logLevel}|{exceptionString}{message}");
        }
    }

}
