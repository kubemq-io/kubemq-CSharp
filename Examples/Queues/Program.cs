using System.Text;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Config;
using KubeMQ.SDK.csharp.Queues;
using KubeMQ.SDK.csharp.Results;
using Result = KubeMQ.SDK.csharp.Results.Result;

namespace Queues
{
    class Program
    {
        static async Task<QueuesClient> CreateQueuesClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id");
            QueuesClient client = new QueuesClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
        static async Task  CreateQueue()
        {
            QueuesClient client = await CreateQueuesClient();
            Result result = await client.Create("q1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not create queue channel, error:{result.ErrorMessage}");
            }
            Console.WriteLine("Queues Channel Created");
            await client.Close();
        }
        
        static async Task ListQueues()
        {
            QueuesClient client = await CreateQueuesClient();
            ListQueuesAsyncResult listResult = await client.List();
            if (!listResult.IsSuccess)
            {
                Console.WriteLine($"Could not list queues channels, error:{listResult.ErrorMessage}");
                return;
            }
            foreach (var channel in listResult.Channels)
            {
                Console.WriteLine($"{channel}");
            }
            await client.Close();
        }
        
        
        static async Task DeleteQueue()
        {
            QueuesClient client = await CreateQueuesClient();
            Result result = await client.Delete("q1");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Could not delete queues channel, error:{result.ErrorMessage}");
                return;
            }
            Console.WriteLine("Queues Channel Deleted");
            await client.Close();
        }
        
        static async Task SendQueueMessage()
        {
            QueuesClient client = await CreateQueuesClient();
            Console.WriteLine("Sending queue message");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="send_receive_queue",
                Body = "hello kubemq - sending an queue message"u8.ToArray(),
                Tags = new Dictionary<string, string>()
                    {
                        {"key1", "value1"},
                        {"key2", "value2"} 
                    },
                
                Policy = new QueueMessagePolicy()
                {
                    DelaySeconds = 1,
                    ExpirationSeconds = 10,
                }
            };
            SendResponse sendResult = await client.Send(msg);
            if (sendResult.Error != null)
            {
                Console.WriteLine($"Could not send queue message, error:{sendResult.Error}");
                return;
            }
            Thread.Sleep(1000);
            Console.WriteLine("Polling queue message");
            PollRequest pollRequest = new PollRequest()
            {
                Queue = "send_receive_queue",
                WaitTimeout = 1000,
                MaxItems = 1,
                
            };
            PollResponse response = await client.Poll(pollRequest);
            if (response.Error != null)
            {
                Console.WriteLine($"Could not poll queue message, error:{response.Error}");
                return;
            }
            
            // Acknowledge all messages
            // response.AckAll();
            //
            // // Reject all messages
            // response.RejectAll();
            //
            // // Requeue all messages
            // response.ReQueueAll("requeue");
            
            foreach (var receiveMsg in response.Messages)
            {
                Console.WriteLine(Encoding.UTF8.GetString(receiveMsg.Body));
                // Acknowledge the message
                receiveMsg.Ack();
                
                // Reject the message
                 //receiveMsg.Reject();
                
                // Requeue the message
                //receiveMsg.ReQueue("requeue");
            }
            
            await client.Close();
        }
        
        static async Task SendQueueMessageWithAutoAck()
        {
            QueuesClient client = await CreateQueuesClient();
            Console.WriteLine("Sending queue message");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="send_receive_queue_auto_ack",
                Body = "hello kubemq - sending an queue message"u8.ToArray(),
            };
            SendResponse sendResult = await client.Send(msg);
            if (sendResult.Error != null)
            {
                Console.WriteLine($"Could not send queue message, error:{sendResult.Error}");
                return;
            }
            Thread.Sleep(1000);
            Console.WriteLine("Polling queue message");
            PollRequest pollRequest = new PollRequest()
            {
                Queue = "send_receive_queue",
                WaitTimeout = 1000,
                MaxItems = 1,
                AutoAck = true,
            };
            PollResponse response = await client.Poll(pollRequest);
            if (response.Error != null)
            {
                Console.WriteLine($"Could not poll queue message, error:{response.Error}");
                return;
            }
            foreach (var receiveMsg in response.Messages)
            {
                Console.WriteLine(Encoding.UTF8.GetString(receiveMsg.Body));
            }
            await client.Close();
        }
        
        static async Task SendQueueMessageWithDeadLetterQueue()
        {
            QueuesClient client = await CreateQueuesClient();
            Console.WriteLine("Sending queue message");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="send_receive_queue_dlq",
                Body = "Message with Deadletter Queue"u8.ToArray(),
                Policy = new QueueMessagePolicy()
                {
                    MaxReceiveCount = 3,
                    MaxReceiveQueue = "dlq",
                }
            };
            SendResponse sendResult = await client.Send(msg);
            if (sendResult.Error != null)
            {
                Console.WriteLine($"Could not send queue message, error:{sendResult.Error}");
                return;
            }
            Thread.Sleep(1000);
            Console.WriteLine("Polling queue message and reject it, break when no message to poll");
            for (int i = 0; i < 10; i++)
            {
                PollRequest pollRequest = new PollRequest()
                {
                    Queue = "send_receive_queue_dlq",
                    WaitTimeout = 1000,
                    MaxItems = 1,
                };
                PollResponse response = await client.Poll(pollRequest);
                if (response.Error != null)
                {
                    Console.WriteLine($"Could not poll queue message, error:{response.Error}");
                    return;
                }
                if (response.Messages.Count == 0)
                {
                    break;
                }
                foreach (var receiveMsg in response.Messages)
                {
                    Console.WriteLine($"Message received: {Encoding.UTF8.GetString(receiveMsg.Body)}, Receiving count: {receiveMsg.Attributes.ReceiveCount}, rejecting message");
                    // Reject the message
                    receiveMsg.Reject();
                }
            }
            Console.WriteLine("Polling dlq queue for rejected messages");
            PollRequest dlqPollRequest = new PollRequest()
            {
                Queue = "dlq",
                WaitTimeout = 1000,
                MaxItems = 1,
            };
            PollResponse dlqResponse = await client.Poll(dlqPollRequest);
            if (dlqResponse.Error != null)
            {
                Console.WriteLine($"Could not poll dlq queue message, error:{dlqResponse.Error}");
                return;
            }
            foreach (var receiveMsg in dlqResponse.Messages)
            {
                Console.WriteLine($"Message received from dlq: {Encoding.UTF8.GetString(receiveMsg.Body)}");
                receiveMsg.Ack();
            }
            client.Close();
        }
        
        static async Task SendQueueMessageWithVisibility()
        {
            QueuesClient client = await CreateQueuesClient();
            Console.WriteLine("Sending queue message");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="send_receive_visibility",
                Body = "Message with visbility"u8.ToArray(),
                
            };
            SendResponse sendResult = await client.Send(msg);
            if (sendResult.Error != null)
            {
                Console.WriteLine($"Could not send queue message, error:{sendResult.Error}");
                return;
            }
            Thread.Sleep(1000);
            Console.WriteLine("Polling queue message with visibility");
            PollRequest pollRequest = new PollRequest()
            {
                Queue = "send_receive_visibility",
                WaitTimeout = 1000,
                MaxItems = 1,
                VisibilitySeconds = 3,
            };
        
            PollResponse response = await client.Poll(pollRequest);
            if (response.Error != null)
            {
                Console.WriteLine($"Could not poll queue message, error:{response.Error}");
                return;
            }
            foreach (var receiveMsg in response.Messages)
            {
                Console.WriteLine($"Message received, doing some work");
                Thread.Sleep(2000);
                Console.WriteLine($"Message processed, need more time to ack, extending visibility by 5 seconds");
                receiveMsg.ExtendVisibility(5);
                Console.WriteLine($"Do some more work for 2 seconds");
                Thread.Sleep(2000);
                Console.WriteLine($"Ack the message");
                receiveMsg.Ack();
            }
            await client.Close();
        }
        static async Task SendQueueMessageWithVisibilityExpiration()
        {
            QueuesClient client = await CreateQueuesClient();
            Console.WriteLine("Sending queue message");
            Message msg= new Message()
            {
                MessageID = "1",
                Queue ="send_receive_visibility",
                Body = "Message with visbility"u8.ToArray(),
                
            };
            SendResponse sendResult = await client.Send(msg);
            if (sendResult.Error != null)
            {
                Console.WriteLine($"Could not send queue message, error:{sendResult.Error}");
                return;
            }
            Thread.Sleep(1000);
            Console.WriteLine("Polling queue message with visibility");
            PollRequest pollRequest = new PollRequest()
            {
                Queue = "send_receive_visibility",
                WaitTimeout = 1000,
                MaxItems = 1,
                VisibilitySeconds = 3
            };
        
            PollResponse response = await client.Poll(pollRequest);
            if (response.Error != null)
            {
                Console.WriteLine($"Could not poll queue message, error:{response.Error}");
                return;
            }
            foreach (var receiveMsg in response.Messages)
            {
                Console.WriteLine($"Message received, doing some work for 4 seconds");
                Thread.Sleep(4000);
                receiveMsg.ExtendVisibility(4);
            }
            await client.Close();
        }
        static async Task Main(string[] args)
        {
            await CreateQueue();
            await ListQueues();
            await DeleteQueue();
            await SendQueueMessage();
            await SendQueueMessageWithAutoAck();
            await SendQueueMessageWithDeadLetterQueue();
            await SendQueueMessageWithVisibility();
            await SendQueueMessageWithVisibilityExpiration();
        }
    }
}