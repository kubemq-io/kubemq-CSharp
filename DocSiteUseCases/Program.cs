using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocSiteUseCases
{
    class Program
    {
        static void Main(string[] args)
        {
            Ack_All_Messages_In_a_Queue();
        
                //Send_Message_to_a_Queue();
                //Send_Message_to_a_Queue_with_Expiration();
                //Send_Message_to_a_Queue_with_Delay();
                //Send_Message_to_a_Queue_with_Deadletter_Queue();

                Send_Batch_Messages();
                Send_Batch_Messages_no_exp();
                Thread.Sleep(1000);
                Receive_Messages_from_a_Queue();
               Thread.Sleep(1000);
           
            Peak_Messages_from_a_Queue();
          

            //Transactional_Queue_Ack();
            //Transactional_Queue_Reject();
            //Transactional_Queue_Extend_Visibility();
            //Transactional_Queue_Resend_to_New_Queue();
            //Transactional_Queue_Resend_Modified_Message();
        }

     
        private static void Send_Message_to_a_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var resSend = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("some-simple_queue-queue-message"),
                Metadata = "someMeta"
            });
            if (resSend.IsError)
            {
                Console.WriteLine($"Message enqueue error, error:{resSend.Error}");
            }           
        }
        private static void Send_Message_to_a_Queue_with_Expiration()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var resSend = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("some-simple_queue-queue-message"),
                Metadata = "emptyMeta",
                Policy = new KubeMQ.Grpc.QueueMessagePolicy
                {
                    ExpirationSeconds = 20
                }
            });
            if (resSend.IsError)
            {
                Console.WriteLine($"Message enqueue error, error:{resSend.Error}");
            }          
        }
        private static void Send_Message_to_a_Queue_with_Delay()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var resSend = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("some-simple_queue-queue-message"),
                Metadata = "emptyMeta",
                Policy = new KubeMQ.Grpc.QueueMessagePolicy
                {
                    DelaySeconds =5
                }
            });
            if (resSend.IsError)
            {
                Console.WriteLine($"Message enqueue error, error:{resSend.Error}");
            }         
        }
        private static void Send_Message_to_a_Queue_with_Deadletter_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var resSend = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message
            {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("some-simple_queue-queue-message"),
                Metadata = "emptyMeta",
                Policy = new KubeMQ.Grpc.QueueMessagePolicy
                {
                    MaxReceiveCount = 3,
                    MaxReceiveQueue = "DeadLetterQueue"
                }
            });
            if (resSend.IsError)
            {
                Console.WriteLine($"Message enqueue error, error:{resSend.Error}");
            }
        }
        private static void Send_Batch_Messages()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var batch = new List<KubeMQ.SDK.csharp.Queue.Message>();
            for (int i = 0; i < 10; i++)
            {
                batch.Add(new KubeMQ.SDK.csharp.Queue.Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"Batch Message {i}"),
                    Metadata = "emptyMeta",
                    Policy = new KubeMQ.Grpc.QueueMessagePolicy
                    {
                        ExpirationSeconds = 1
                    }
                });
            }
            var resBatch = queue.SendQueueMessagesBatch(batch);
            if (resBatch.HaveErrors)
            {
                Console.WriteLine($"Message sent batch has errors");
            }
            foreach (var item in resBatch.Results)
            {               
                if (item.IsError)
                {
                    Console.WriteLine($"Message enqueue error, MessageID:{item.MessageID}, error:{item.Error}");
                }
                else
                {
                   // Console.WriteLine($"Send to Queue Result: MessageID:{item.MessageID}, Sent At:{ KubeMQ.SDK.csharp.Tools.Converter.FromUnixTime(item.SentAt)}");
                }
            }
        }

        private static void Send_Batch_Messages_no_exp()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var batch = new List<KubeMQ.SDK.csharp.Queue.Message>();
            for (int i = 0; i < 10; i++)
            {
                batch.Add(new KubeMQ.SDK.csharp.Queue.Message
                {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"Batch Message {i}"),
                    Metadata = "emptyMeta",
                   
                });
            }
            var resBatch = queue.SendQueueMessagesBatch(batch);
            if (resBatch.HaveErrors)
            {
                Console.WriteLine($"Message sent batch has errors");
            }
            foreach (var item in resBatch.Results)
            {
                if (item.IsError)
                {
                    Console.WriteLine($"Message enqueue error, MessageID:{item.MessageID}, error:{item.Error}");
                }
                else
                {
                    // Console.WriteLine($"Send to Queue Result: MessageID:{item.MessageID}, Sent At:{ KubeMQ.SDK.csharp.Tools.Converter.FromUnixTime(item.SentAt)}");
                }
            }
        }
        private static void Receive_Messages_from_a_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            queue.WaitTimeSecondsQueueMessages = 1;
            var resRec = queue.ReceiveQueueMessages(10);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"Received {resRec.MessagesReceived} Messages:");
            foreach (var item in resRec.Messages)
            {
                Console.WriteLine($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
            }
        }
        private static void Peak_Messages_from_a_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            queue.WaitTimeSecondsQueueMessages = 1;
            var resPeak = queue.PeakQueueMessage(10);
            if (resPeak.IsError)
            {
                Console.WriteLine($"Message peak error, error:{resPeak.Error}");
                return;
            }
            Console.WriteLine($"Peaked {resPeak.MessagesReceived} Messages:");
            foreach (var item in resPeak.Messages)
            {
                Console.WriteLine($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
            }
        }
        private static void Ack_All_Messages_In_a_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var resAck = queue.AckAllQueueMessagesResponse();
            if (resAck.IsError)
            {
                Console.WriteLine($"AckAllQueueMessagesResponse error, error:{resAck.Error}");
                return;
            }
            Console.WriteLine($"Ack All Messages:{resAck.AffectedMessages} completed");
        }
        private static void Transactional_Queue_Ack()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var transaction = queue.CreateTransaction();
            // get message from the queue with visibility of 10 seconds and wait timeout of 10 seconds
            var resRec = transaction.Receive(10, 10);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            Console.WriteLine("Doing some work.....");
            Thread.Sleep(1000);
            Console.WriteLine("Done, ack the message");
            var resAck = transaction.AckMessage(resRec.Message.Attributes.Sequence);
            if (resAck.IsError)
            {
                Console.WriteLine($"Ack message error:{resAck.Error}");
            }
            Console.WriteLine("Checking for next message");
            resRec = transaction.Receive(10, 1);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }  
        }
        private static void Transactional_Queue_Reject()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var transaction = queue.CreateTransaction();
            // get message from the queue with visibility of 10 seconds and wait timeout of 10 seconds
            var resRec = transaction.Receive(10, 10);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            Console.WriteLine("Reject message");
            var resRej = transaction.RejectMessage(resRec.Message.Attributes.Sequence);
            if (resRej.IsError)
            {
                Console.WriteLine($"Message reject error, error:{resRej.Error}");
                return;
            }
        }
        private static void Transactional_Queue_Extend_Visibility()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var transaction = queue.CreateTransaction();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            var resRec = transaction.Receive(5, 10);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            Console.WriteLine("work for 1 seconds");
            Thread.Sleep(1000);
            Console.WriteLine("Need more time to process, extend visibility for more 3 seconds");
            var resExt = transaction.ExtendVisibility(3);
            if (resExt.IsError)
            {
                Console.WriteLine($"Message ExtendVisibility error, error:{resExt.Error}");
                return;
            }
            Console.WriteLine("Approved. work for 2.5 seconds");
            Thread.Sleep(2500);
            Console.WriteLine("Work done... ack the message");

            var resAck = transaction.AckMessage(resRec.Message.Attributes.Sequence);
            if (resAck.IsError)
            {
                Console.WriteLine($"Ack message error:{resAck.Error}");
            }
            Console.WriteLine("Ack done");
        }
        private static void Transactional_Queue_Resend_to_New_Queue()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var transaction = queue.CreateTransaction();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            var resRec = transaction.Receive(5, 10);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            Console.WriteLine("Resend to new queue");
            var resResend = transaction.Resend("new-queue");
            if (resResend.IsError)
            {
                Console.WriteLine($"Message Resend error, error:{resResend.Error}");
                return;
            }

            Console.WriteLine("Done");
        }
        private static void Transactional_Queue_Resend_Modified_Message()
        {
            KubeMQ.SDK.csharp.Queue.Queue queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");
            var transaction = queue.CreateTransaction();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            var resRec = transaction.Receive(3,5);
            if (resRec.IsError)
            {
                Console.WriteLine($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            var modMsg = resRec.Message;
            modMsg.Queue = "receiverB";
            modMsg.Metadata = "new metadata";

            var resMod = transaction.Modify(modMsg);
            if (resMod.IsError)
            {
                Console.WriteLine($"Message Modify error, error:{resMod.Error}");
                return;
            }
        }

       


 

     
 

    

    


      
     

       
    }
}
