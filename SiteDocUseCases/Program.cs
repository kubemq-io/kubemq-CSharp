using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocSiteUseCases {

    class Program {

        //JWT Token shared a secret key with kubemq.
        static string tempAuthToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.JxDcFP-RSB4PPVOnbV362jJC9YApD5BuxBDAwf_TCQc";

        static void Main () {

            Ack_All_Messages_In_a_Queue ();
            Send_Message_to_a_Queue ();
            Send_Message_to_a_Queue_with_Expiration ();
            Send_Message_to_a_Queue_with_Delay ();
            Send_Message_to_a_Queue_with_Deadletter_Queue ();
            Send_Batch_Messages ();
            Receive_Messages_from_a_Queue ();
            Peek_Messages_from_a_Queue ();

            Transactional_Queue_Ack ();
            Transactional_Queue_Reject();
            Transactional_Queue_Extend_Visibility();
            Transactional_Queue_Resend_to_New_Queue();
            Transactional_Queue_Resend_Modified_Message();

           
            Receiving_Events(); 
            Sending_Events_Single_Event();
            Sending_Events_Single_Event();
            Sending_Events_Stream_Events();

            Receiving_Events_Store();
            Sending_Events_Store_Single_Event_to_Store();
            Sending_Events_Store_Stream_Events_Store();

            Commands_Receiving_Commands_Requests();
            Commands_Sending_Command_Request();
            Commands_Sending_Command_Request_async();

            Queries_Receiving_Query_Requests();
            Queries_Sending_Query_Request();
            Queries_Sending_Query_Request_async();

            Console.ReadLine ();
        }

        private static void Send_Message_to_a_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);
            try {

                var resSend = queue.SendQueueMessage (new KubeMQ.SDK.csharp.Queue.Message {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("some-simple_queue-queue-message"),
                        Metadata = "someMeta"
                });
                if (resSend.IsError) {
                    Console.WriteLine ($"Message enqueue error, error:{resSend.Error}");
                }
            } catch (Exception ex) {
                Console.WriteLine ($"Message enqueue error, error:{ex.Message}");
            }
        }
        private static void Send_Message_to_a_Queue_with_Expiration () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);

            var resSend = queue.SendQueueMessage (new KubeMQ.SDK.csharp.Queue.Message {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("some-simple_queue-queue-message"),
                    Metadata = "emptyMeta",
                    Policy = new KubeMQ.Grpc.QueueMessagePolicy {
                        ExpirationSeconds = 20
                    }
            });
            if (resSend.IsError) {
                Console.WriteLine ($"Message enqueue error, error:{resSend.Error}");
            }
        }
        private static void Send_Message_to_a_Queue_with_Delay () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);

            var resSend = queue.SendQueueMessage (new KubeMQ.SDK.csharp.Queue.Message {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("some-simple_queue-queue-message"),
                    Metadata = "emptyMeta",
                    Policy = new KubeMQ.Grpc.QueueMessagePolicy {
                        DelaySeconds = 5
                    }
            });
            if (resSend.IsError) {
                Console.WriteLine ($"Message enqueue error, error:{resSend.Error}");
            }
        }
        private static void Send_Message_to_a_Queue_with_Deadletter_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);

            var resSend = queue.SendQueueMessage (new KubeMQ.SDK.csharp.Queue.Message {
                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("some-simple_queue-queue-message"),
                    Metadata = "emptyMeta",
                    Policy = new KubeMQ.Grpc.QueueMessagePolicy {
                        MaxReceiveCount = 3,
                            MaxReceiveQueue = "DeadLetterQueue"
                    }
            });
            if (resSend.IsError) {
                Console.WriteLine ($"Message enqueue error, error:{resSend.Error}");
            }
        }
        private static void Send_Batch_Messages () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);
            var batch = new List<KubeMQ.SDK.csharp.Queue.Message> ();
            for (int i = 0; i < 10; i++) {
                batch.Add (new KubeMQ.SDK.csharp.Queue.Message {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ($"Batch Message {i}"),
                        Metadata = "emptyMeta",
                });
            }
            var resBatch = queue.SendQueueMessagesBatch (batch);
            if (resBatch.HaveErrors) {
                Console.WriteLine ($"Message sent batch has errors");
            }
            foreach (var item in resBatch.Results) {
                if (item.IsError) {
                    Console.WriteLine ($"Message enqueue error, MessageID:{item.MessageID}, error:{item.Error}");
                } else {
                    Console.WriteLine ($"Send to Queue Result: MessageID:{item.MessageID}, Sent At:{ KubeMQ.SDK.csharp.Tools.Converter.FromUnixTime(item.SentAt)}");
                }
            }
        }

        private static void Receive_Messages_from_a_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken) {
                WaitTimeSecondsQueueMessages = 1
            };
            var resRec = queue.ReceiveQueueMessages (10);
            if (resRec.IsError) {
                Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                return;
            }
            Console.WriteLine ($"Received {resRec.MessagesReceived} Messages:");
            foreach (var item in resRec.Messages) {
                Console.WriteLine ($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
            }
        }
        private static void Peek_Messages_from_a_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken) {
                WaitTimeSecondsQueueMessages = 1
            };
            var resPeak = queue.PeekQueueMessage (10);
            if (resPeak.IsError) {
                Console.WriteLine ($"Message peak error, error:{resPeak.Error}");
                return;
            }
            Console.WriteLine ($"Peaked {resPeak.MessagesReceived} Messages:");
            foreach (var item in resPeak.Messages) {
                Console.WriteLine ($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}");
            }
        }
        private static void Ack_All_Messages_In_a_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null,
                tempAuthToken);
            try {
                var resAck = queue.AckAllQueueMessages ();
                if (resAck.IsError) {
                    Console.WriteLine ($"AckAllQueueMessagesResponse error, error:{resAck.Error}");
                    return;
                }
                Console.WriteLine ($"Ack All Messages:{resAck.AffectedMessages} completed");
            } catch (System.Exception ex) {
                Console.WriteLine ($"AckAllQueueMessagesResponse error, error:{ex.Message}");
            }

        }

        private static void Transactional_Queue_Ack () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null,tempAuthToken);
            var transaction = queue.CreateTransaction ();
            // get message from the queue with visibility of 10 seconds and wait timeout of 10 seconds
            KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
            try {
                resRec = transaction.Receive (10, 10);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
                Console.WriteLine ($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");

            } catch (System.Exception ex) {
                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }
            Console.WriteLine ("Doing some work.....");
            Thread.Sleep (1000);
            Console.WriteLine ("Done, ack the message");

            try {
                var resAck = transaction.AckMessage (resRec.Message.Attributes.Sequence);
                if (resAck.IsError) {
                    Console.WriteLine ($"Ack message error:{resAck.Error}");
                    return;
                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Ack message error:{ex.Message}");
                return;
            }

            Console.WriteLine ("Checking for next message");
            try {
                resRec = transaction.Receive (10, 1);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }

        }
        private static void Transactional_Queue_Reject () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);
            var transaction = queue.CreateTransaction ();
            // get message from the queue with visibility of 10 seconds and wait timeout of 10 seconds
            KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
            try {
                resRec = transaction.Receive (10, 10);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
                Console.WriteLine ($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");

            } catch (System.Exception ex) {
                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }
            Console.WriteLine ("Reject message");
            try {
                var resRej = transaction.RejectMessage (resRec.Message.Attributes.Sequence);
                if (resRej.IsError) {

                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message reject error, error:{ex.Message}");
                return;
            }

        }
        private static void Transactional_Queue_Extend_Visibility () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null , tempAuthToken);
            var transaction = queue.CreateTransaction ();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
            try {
                resRec = transaction.Receive (5, 10);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
                Console.WriteLine ($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");

            } catch (System.Exception ex) {
                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }

            Console.WriteLine ("Work for 1 seconds");
            Thread.Sleep (1000);
            Console.WriteLine ("Need more time to process, extend visibility for more 3 seconds");
            try {
                var resExt = transaction.ExtendVisibility (3);
                if (resExt.IsError) {
                    Console.WriteLine ($"Message ExtendVisibility error, error:{resExt.Error}");
                    return;
                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message ExtendVisibility error, error:{ex.Message}");
            }

            Console.WriteLine ("Approved. work for 2.5 seconds");
            Thread.Sleep (2500);
            Console.WriteLine ("Work done... ack the message");
            try {
                var resAck = transaction.AckMessage (resRec.Message.Attributes.Sequence);
                if (resAck.IsError) {
                    Console.WriteLine ($"Ack message error:{resAck.Error}");
                }
                Console.WriteLine ("Ack done");
            } catch (System.Exception ex) {

                Console.WriteLine ($"Ack message error:{ex.Message}");
            }

        }
        private static void Transactional_Queue_Resend_to_New_Queue () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);
            var transaction = queue.CreateTransaction ();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
            try {
                resRec = transaction.Receive (5, 10);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
                Console.WriteLine ($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }

            Console.WriteLine ("Resend to new queue");
            try {
                var resResend = transaction.Resend ("new-queue");
                if (resResend.IsError) {
                    Console.WriteLine ($"Message Resend error, error:{resResend.Error}");
                    return;
                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message Resend error, error:{ex.Message}");
                return;
            }

            Console.WriteLine ("Done");
        }
        private static void Transactional_Queue_Resend_Modified_Message () {
            var queue = new KubeMQ.SDK.csharp.Queue.Queue ("QueueName", "ClientID", "localhost:50000", null, tempAuthToken);
            var transaction = queue.CreateTransaction ();
            // get message from the queue with visibility of 5 seconds and wait timeout of 10 seconds
            KubeMQ.SDK.csharp.Queue.Stream.TransactionMessagesResponse resRec;
            try {
                resRec = transaction.Receive (3, 5);
                if (resRec.IsError) {
                    Console.WriteLine ($"Message dequeue error, error:{resRec.Error}");
                    return;
                }
            } catch (System.Exception ex) {

                Console.WriteLine ($"Message dequeue error, error:{ex.Message}");
                return;
            }

            Console.WriteLine ($"MessageID: {resRec.Message.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(resRec.Message.Body)}");
            var modMsg = resRec.Message;
            modMsg.Queue = "receiverB";
            modMsg.Metadata = "new metadata";
            try {
                var resMod = transaction.Modify (modMsg);
                if (resMod.IsError) {
                    Console.WriteLine ($"Message Modify error, error:{resMod.Error}");
                    return;
                }
            } catch (System.Exception ex) {
                Console.WriteLine ($"Message Modify error, error:{ex.Message}");
                return;
            }
        }

        private static void Sending_Events_Single_Event () {
            string ChannelName = "testing_event_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";


            var channel = new KubeMQ.SDK.csharp.Events.Channel (new KubeMQ.SDK.csharp.Events.ChannelParameters {
                ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken = tempAuthToken
            });

            try {
                var result = channel.SendEvent (new KubeMQ.SDK.csharp.Events.Event () {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending single event")
                });
                if (!result.Sent) {
                    Console.WriteLine ($"Could not send single message:{result.Error}");
                }
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static void Sending_Events_Stream_Events () {
            string ChannelName = "testing_event_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";
            var channel = new KubeMQ.SDK.csharp.Events.Channel (new KubeMQ.SDK.csharp.Events.ChannelParameters {
                ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken = tempAuthToken
                    
            });

            try {
                _ = channel.StreamEvent (new KubeMQ.SDK.csharp.Events.Event {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending stream event")
                });

            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }

        }
        private static void Receiving_Events () {
            string ChannelName = "testing_event_channel",
             ClientID = "hello-world-subscriber",
             KubeMQServerAddress = "localhost:50000";

            var subscriber = new KubeMQ.SDK.csharp.Events.Subscriber (KubeMQServerAddress,tempAuthToken);
            try {
                subscriber.SubscribeToEvents (new KubeMQ.SDK.csharp.Subscription.SubscribeRequest {
                        Channel = ChannelName,
                            SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Events,
                            ClientID = ClientID

                    }, (eventReceive) => {

                        Console.WriteLine ($"Event Received: EventID:{eventReceive.EventID} Channel:{eventReceive.Channel} Metadata:{eventReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(eventReceive.Body)} ");
                    },
                    (errorHandler) => {
                        Console.WriteLine (errorHandler.Message);
                    });
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }

        private static void Sending_Events_Store_Single_Event_to_Store () {
            string ChannelName = "testing_event_channel_store",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.Events.Channel (new KubeMQ.SDK.csharp.Events.ChannelParameters {
                ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    Store = true,
                    AuthToken = tempAuthToken
            });

            try {
                var result = channel.SendEvent (new KubeMQ.SDK.csharp.Events.Event () {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending single event store"),
                        EventID = $"event-Store-!",
                        Metadata = "some-metadata"
                });
                if (!result.Sent) {
                    Console.WriteLine ($"Could not send single message:{result.Error}");
                }
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }

        }
        private static void Sending_Events_Store_Stream_Events_Store () {
            string ChannelName = "testing_event_channel_store",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.Events.Channel (new KubeMQ.SDK.csharp.Events.ChannelParameters {
                ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    Store = true,
                    AuthToken = tempAuthToken
                    
            });

            try {

                _ = channel.StreamEvent (new KubeMQ.SDK.csharp.Events.Event {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - stream event store")
                });
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static void Receiving_Events_Store () {
            string ChannelName = "testing_event_channel_store",
             ClientID = "hello-world-subscriber",
             KubeMQServerAddress = "localhost:50000";

            var subscriber = new KubeMQ.SDK.csharp.Events.Subscriber (KubeMQServerAddress, tempAuthToken);
            try {
                subscriber.SubscribeToEvents (new KubeMQ.SDK.csharp.Subscription.SubscribeRequest {
                        Channel = ChannelName,
                            SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.EventsStore,
                            ClientID = ClientID,
                            EventsStoreType = KubeMQ.SDK.csharp.Subscription.EventsStoreType.StartFromFirst,
                            EventsStoreTypeValue = 0

                    }, (eventReceive) => {

                        Console.WriteLine ($"Event Received: EventID:{eventReceive.EventID} Channel:{eventReceive.Channel} Metadata:{eventReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(eventReceive.Body)} ");
                    },
                    (errorHandler) => {
                        Console.WriteLine (errorHandler.Message);
                    });
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }

        private static void Queries_Sending_Query_Request () {
            string ChannelName = "testing_query_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel (new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters {
                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query,
                    Timeout = 10000,
                    ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken = tempAuthToken

            });
            try {
                var result = channel.SendRequest (new KubeMQ.SDK.csharp.CommandQuery.Request {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending a query, please reply")

                });

                if (!result.Executed) {
                    Console.WriteLine ($"Response error:{result.Error}");
                    return;
                }
                Console.WriteLine ($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}");
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static async void Queries_Sending_Query_Request_async () {
            string ChannelName = "testing_query_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel (new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters {
                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query,
                    Timeout = 1000,
                    ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken = tempAuthToken
            });
            try {
                var result = await channel.SendRequestAsync (new KubeMQ.SDK.csharp.CommandQuery.Request {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending a query, please reply")
                });

                if (!result.Executed) {
                    Console.WriteLine ($"Response error:{result.Error}");
                    return;
                }
                Console.WriteLine ($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}");
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static void Queries_Receiving_Query_Requests () {
            string ChannelName = "testing_query_channel",
             ClientID = "hello-world-subscriber",
             KubeMQServerAddress = "localhost:50000";

            KubeMQ.SDK.csharp.CommandQuery.Responder responder = new KubeMQ.SDK.csharp.CommandQuery.Responder (KubeMQServerAddress, tempAuthToken);
            try {
                responder.SubscribeToRequests (new KubeMQ.SDK.csharp.Subscription.SubscribeRequest () {
                    Channel = ChannelName,
                        SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Queries,
                        ClientID = ClientID
                }, (queryReceive) => {
                    Console.WriteLine ($"Command Received: Id:{queryReceive.RequestID} Channel:{queryReceive.Channel} Metadata:{queryReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(queryReceive.Body)} ");
                    return new KubeMQ.SDK.csharp.CommandQuery.Response (queryReceive) {
                        Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("got your query, you are good to go"),
                            CacheHit = false,
                            Error = "None",
                            ClientID = ClientID,
                            Executed = true,
                            Metadata = "this is a response",
                            Timestamp = DateTime.UtcNow
                    };

                }, (errorHandler) => {
                    Console.WriteLine (errorHandler.Message);
                });
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }

        private static void Commands_Receiving_Commands_Requests () {
            string ChannelName = "testing_command_channel",
             ClientID = "hello-world-subscriber",
             KubeMQServerAddress = "localhost:50000";

            KubeMQ.SDK.csharp.CommandQuery.Responder responder = new KubeMQ.SDK.csharp.CommandQuery.Responder (KubeMQServerAddress,tempAuthToken);
            try {
                responder.SubscribeToRequests (new KubeMQ.SDK.csharp.Subscription.SubscribeRequest () {
                    Channel = ChannelName,
                        SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Commands,
                        ClientID = ClientID
                }, (commandReceive) => {
                    Console.WriteLine ($"Command Received: Id:{commandReceive.RequestID} Channel:{commandReceive.Channel} Metadata:{commandReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(commandReceive.Body)} ");
                    return new KubeMQ.SDK.csharp.CommandQuery.Response (commandReceive) {
                        Body = new byte[0],
                            CacheHit = false,
                            Error = "None",
                            ClientID = ClientID,
                            Executed = true,
                            Metadata = string.Empty,
                            Timestamp = DateTime.UtcNow,
                    };

                }, (errorHandler) => {
                    Console.WriteLine (errorHandler.Message);
                });
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static void Commands_Sending_Command_Request () {
            string ChannelName = "testing_command_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel (new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters {
                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Command,
                    Timeout = 1000,
                    ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken =tempAuthToken
            });
            try {
                var result = channel.SendRequest (new KubeMQ.SDK.csharp.CommandQuery.Request {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending a command, please reply")
                });

                if (!result.Executed) {
                    Console.WriteLine ($"Response error:{result.Error}");
                    return;
                }
                Console.WriteLine ($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}");
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
        private static async void Commands_Sending_Command_Request_async () {
            string ChannelName = "testing_command_channel",
             ClientID = "hello-world-sender",
             KubeMQServerAddress = "localhost:50000";

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel (new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters {
                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Command,
                    Timeout = 1000,
                    ChannelName = ChannelName,
                    ClientID = ClientID,
                    KubeMQAddress = KubeMQServerAddress,
                    AuthToken = tempAuthToken
            });
            try {
                var result = await channel.SendRequestAsync (new KubeMQ.SDK.csharp.CommandQuery.Request {
                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray ("hello kubemq - sending a command, please reply")
                });

                if (!result.Executed) {
                    Console.WriteLine ($"Response error:{result.Error}");
                    return;
                }
                Console.WriteLine ($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}");
            } catch (Exception ex) {
                Console.WriteLine (ex.Message);
            }
        }
    }
}