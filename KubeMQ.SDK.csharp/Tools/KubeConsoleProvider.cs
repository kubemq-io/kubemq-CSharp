using System;
using Microsoft.Extensions.Logging;

namespace KubeMQ.SDK.csharp.Tools
{
    public class KubeConsoleProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new KubeConsoleLogger();
        }

        public void Dispose()
        {
            
        }
    }

}
