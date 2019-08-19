using KubeMQ.SDK.csharp.Queue;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Queue_test
{
    [TestClass]
    public class QueueLoad_Tests
    {
        [TestMethod]
        public void Init1000Queues_pass()
        {
            for (int i = 0; i < 50; i++)
            {
                Queue  queue = new Queue("Init1000Queues_pass", "test", "localhost:50000");
             //   queue.SendQueueMessage(new Message { Metadata = "", Body = new byte[0] });
                var res = queue.ReceiveQueueMessages(1);
            }
        }
    }
}
