using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KubeMQ.SDK.csharp.Tools
{
    class IDGenerator
    {
        public class ReqID
        {

            static int _id;
            public static string Getid()
            {

                //return Interlocked.Increment(ref _id);

                int temp, temp2;

                do
                {
                    temp = _id;
                    temp2 = temp == ushort.MaxValue ? 1 : temp + 1;
                }
                while (Interlocked.CompareExchange(ref _id, temp2, temp) != temp);

                return _id.ToString();
            }

        }
    }
}
