using System;

namespace KubeMQ.SDK.csharp.Tools
{
    
    
    
    public class IDGenerator
    {
        public static string Getid()
        {
            return Guid.NewGuid().ToString();
    
        }
    }
}
