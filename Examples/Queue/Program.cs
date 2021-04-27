using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Queue;
using KubeMQ.SDK.csharp.Queue.Stream;

namespace Queue
{
    class Program
    {
        static int tasks = 10;
        static int send = 500;
        static string address = "localhost:50000";
        static int rounds = 10;
        static string queue = "f";
        static int ackDelay = 0;

        static KubeMQ.SDK.csharp.Queue.Queue
            globalReceiver = new KubeMQ.SDK.csharp.Queue.Queue("", "queue-receiver", address);

        static private async Task GetMessagesInTransaction(int id)
        {
            await Task.Run(() =>
            {
                var receiveCounter = 0;
                KubeMQ.SDK.csharp.Queue.Queue localReceiver = new KubeMQ.SDK.csharp.Queue.Queue($"{queue}.{id + 1}", $"local-queue-receiver-{id+1}", address);
                while (receiveCounter < send)
                {
                    var transaction = localReceiver.CreateTransaction();
                    KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
                    try
                    {
                        resRec = transaction.Receive( 10, 15);
                        if (resRec.IsError)
                        {
                            continue;
                        }
                        else
                        {
                            try
                            {
                                Thread.Sleep(ackDelay);
                                var ackRes = transaction.AckMessage(resRec.Message.Attributes.Sequence);
                                if (ackRes.IsError)
                                {
                                    Console.WriteLine($"Error in ack Message, error:{ackRes.Error}");
                                    continue;
                                }

                                receiveCounter++;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                          
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine($"Message Receive error, error:{ex.Message}");
                    }
                    finally
                    {
                        transaction.Close();
                    }
                };
            });
        }

    static private  async Task GetMessagesInPull(int id)
        {
            await Task.Run(() =>
            {
                try
                {
                    var msg = globalReceiver.Pull($"{queue}.{id+1}", send,2);
                    if (msg.IsError)
                    {
                        Console.WriteLine($"message dequeue error, error:{msg.Error}");
                    }
                    {
                        Console.WriteLine($"{msg.Messages.Count()} messages received");    
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            });
        }
        private static async Task RunTasks(int concurrent,bool isPull)
        {
            Task[] taskArray = new Task[concurrent];
            for (int i = 0; i < taskArray.Length; i++)
            {
                if (isPull)
                {
                    taskArray[i] = GetMessagesInPull(i);    
                }
                else
                {
                    taskArray[i] = GetMessagesInTransaction(i);
                }
                
            }

            
            await Task.WhenAll(taskArray);
        }
        static void Main(string[] args)

        {
            var senders = new KubeMQ.SDK.csharp.Queue.Queue("", "queue-stream-tester", address);

            var counter = 0;
            var roundsDone = false;
            do
            {
                counter++;
                if (rounds > 0 && counter >= rounds)
                {
                    roundsDone = true;
                }
                else
                {
                    roundsDone = false;
                }

                 for (int i = 0; i < tasks; i++)
                {
                    var channel = $"{queue}.{i+1}";
                    List<Message> msgs = new List<Message>();
                    for (int t = 0; t < send; t++)
                    {
                        msgs.Add(new KubeMQ.SDK.csharp.Queue.Message
                        {
                            MessageID = i.ToString(),
                            Queue =channel,
                            Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray($"im Message {t}"),
                            Metadata = "some-metadata",
                            Tags = new Dictionary<string, string>()/* ("Action", $"Batch_{testGui}_{i}")*/ 
                            {
                                {"Action",$"Batch_{t}"}
                            }
                        });
                    }
                    var resBatch = senders.Batch(msgs);
                    if (!resBatch.HaveErrors) continue;
                    Console.WriteLine($"message sent batch has errors");
                    System.Environment.Exit(1);
                }

                Console.WriteLine($"sending {send*tasks} messages completed");    
                Stopwatch watch = new Stopwatch();
                watch.Start();
                RunTasks(tasks,false).Wait();
                watch.Stop();
                Console.WriteLine($"Receiveing {send*tasks} messages takes " + watch.ElapsedMilliseconds + " milliseconds");
                Thread.Sleep(1000);
            } while (!roundsDone);
        }
    }
}