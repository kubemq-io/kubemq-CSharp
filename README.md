# .NET
.NET client SDK for KubeMQ. Simple interface to work with the KubeMQ server.

## General SDK description
The SDK implements all communication patterns available through the KubeMQ server:
- Events
- EventStore
- Command
- Query
- Queue

## Installation:
'''
  Install-Package KubeMQ.SDK.csharp -Version 1.0.8
'''
### Framework Support

- .NET Framework 4.6.1
- .NET Framework 4.7.1
- .NET Standard 2.0


## Configuration
The only required configuration setting is the KubeMQ server address.

Configuration can be set by using one of the following:
- Environment Variable
- `appsettings.json` file
- `app.Config` or `Web.config` file
- Within the code


### Configuration via Environment Variable
Set `KubeMQServerAddress` to the KubeMQ Server Address


### Configuration via appsettings.json
Add the following to your appsettings.json:
```json
{
  "KubeMQ": {
    "serverAddress": "{YourServerAddress}:{YourServerPort}"
  }
}
```


### Configuration via app.Config
Simply add the following to your app.config:
```xml
<configuration>  
   <configSections>  
    <section name="KubeMQ" type="System.Configuration.NameValueSectionHandler"/>      
  </configSections>  
    
  <KubeMQ>  
    <add key="serverAddress" value="{YourServerAddress}:{YourServerPort}"/>
  </KubeMQ>  
</configuration>
```

## Main Concepts

- Metadata: The metadata allows us to pass additional information with the event. Can be in any form that can be presented as a string, i.e., struct, JSON, XML and many more.
- Body: The actual content of the event. Can be in any form that is serializable into a byte array, i.e., string, struct, JSON, XML, Collection, binary file and many more.
- ClientID: Displayed in logs, tracing, and KubeMQ dashboard(When using Events Store, it must be unique).
- Tags: Set of Key value pair that help categorize the message


### Event/EventStore/Command/Query

- Channel: Represents the endpoint target. One-to-one or one-to-many. Real-Time Multicast.
- Group: Optional parameter when subscribing to a channel. A set of subscribers can define the same group so that only one of the subscribers within the group will receive a specific event. Used mainly for load balancing. Subscribing without the group parameter ensures receiving all the channel messages. (When using Grouping all the programs that are assigned to the group need to have to same channel name)
- Event Store: The Event Store represents a persistence store, should be used when need to store data on a volume.


### Queue

- Queue: Represents a unique FIFO queue name, used in queue pattern.
- Transaction: Represents an Rpc stream for single message transaction.


### Event/EventStore/Command/Query SubscribeRequest Object:

A struct that is used to initialize SubscribeToEvents/SubscribeToRequest, the SubscribeRequest contains the following:

- SubscribeType - Mandatory - Enum that represents the subscription type:
- Events - if there is no need for Persistence.
- EventsStore - If you want to receive Events from persistence. See Main concepts.
- Command - Should be used when a response is not needed.
- Query - Should be used when a response is needed.
- ClientID - Mandatory - See Main concepts
- Channel - Mandatory - See Main concepts
- Group - Optional - See Main concepts
- EventsStoreType - Mandatory - set the type event store to subscribe to Main concepts.

## Queue

KubeMQ supports distributed durable FIFO based queues with the following core features:

- Exactly One Delivery - Only one message guarantee will deliver to the subscriber
- Single and Batch Messages Send and Receive - Single and multiple messages in one call
- RPC and Stream Flow - RPC flow allows an insert and pulls messages in one call. Stream flow allows single message consuming in a transactional way
- Message Policy - Each message can be configured with expiration and delay timers. Also, each message can specify a dead-letter queue for un-processed messages attempts
- Long Polling - Consumers can wait until a message available in the queue to consume
- Peak Messages - Consumers can peek into a queue without removing them from the queue
- Ack All Queue Messages - Any client can mark all the messages in a queue as discarded and will not be available anymore to consume
- Visibility timers - Consumers can pull a message from the queue and set a timer which will cause the message not be visible to other consumers. This timer can be extended as needed.
- Resend Messages - Consumers can send back a message they pulled to a new queue or send a modified message to the same queue for further processing.

### QueueMessageAttributes.(proto struct)
- Timestamp - when the message arrived to queue.
- Sequence - the message order in the queue.
- MD5OfBody - An MD5 digest non-URL-encoded message body string.
- ReceiveCount - how many recieved.
- ReRouted - if the message was ReRouted from another point.
- ReRoutedFromQueue - from where the message was ReRouted
- ExpirationAt - Expiration time of the message.
- DelayedTo -if the message was Delayed.

```
  message QueueMessageAttributes {
      int64               Timestamp                   =1;
      uint64              Sequence                    =2;
      string              MD5OfBody                   =3;
      int32               ReceiveCount                =4;
      bool                ReRouted                    =5;
      string              ReRoutedFromQueue           =6;
      int64               ExpirationAt                =7;
      int64               DelayedTo                   =8;

  }
```

### Send Message to a Queue

```csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");



            var resSend = queue.SendQueueMessage(new KubeMQ.SDK.csharp.Queue.Message 

            { 

                Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("some-simple_queue-queue-message"), 

                Metadata = "someMeta" 

            }); 

            if (resSend.IsError) 

            { 

                Console.WriteLine($"Message enqueue error, error:{resSend.Error}"); 

            }            
```

### Send Message to a Queue with Expiration

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");



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
```

### Send Message to a Queue with Delay

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");



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
```

### Send Message to a Queue with Dead-letter Queue

``` csharp
  var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");



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

```

### Send Batch Messages

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

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

```

### Receive Messages from a Queue
``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

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
```

### Peak Messages from a Queue

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            queue.WaitTimeSecondsQueueMessages = 1; 

            var resPeak = queue.PeakQueueMessage(10); 

            if (resPeak.IsError) 

            { 

                Console.WriteLine($"Message peek error, error:{resPeak.Error}"); 

                return; 

            } 

            Console.WriteLine($"Peaked {resPeak.MessagesReceived} Messages:"); 

            foreach (var item in resPeak.Messages) 

            { 

                Console.WriteLine($"MessageID: {item.MessageID}, Body:{KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(item.Body)}"); 

            } 
```

### Ack All Messages In a Queue

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var resAck = queue.AckAllQueueMessagesResponse(); 

            if (resAck.IsError) 

            { 

                Console.WriteLine($"AckAllQueueMessagesResponse error, error:{resAck.Error}"); 

                return; 

            } 

            Console.WriteLine($"Ack All Messages:{resAck.AffectedMessages} completed"); 
```

### Transactional Queue - Ack
``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var transaction = queue.CreateTransaction(); 

            // get a message from the queue with visibility of 10 seconds and wait timeout of 10 seconds 

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

```

### Transactional Queue - Reject

``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var transaction = queue.CreateTransaction(); 

            // get a message from the queue with visibility of 10 seconds and wait timeout of 10 seconds 

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
```

### Transactional Queue - Extend Visibility
``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var transaction = queue.CreateTransaction(); 

            // get a message from the queue with visibility of 5 seconds and wait timeout of 10 seconds 

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

```

### Transactional Queue - Resend to New Queue
``` csharp
  var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var transaction = queue.CreateTransaction(); 

            // get a message from the queue with visibility of 5 seconds and wait timeout of 10 seconds 

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
```

### Transactional Queue - Resend Modified Message
``` csharp
var queue = new KubeMQ.SDK.csharp.Queue.Queue("QueueName", "ClientID", "localhost:50000");

            var transaction = queue.CreateTransaction(); 

            // get a message from the queue with visibility of 5 seconds and wait timeout of 10 seconds 

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
```


## Event

Employing several variations of point to point Event communication style patterns. Allows to connect a sender to one or a group of subscribers

- Subscribe to events
- Send stream
- Send single event

#### The KubeMQ.SDK.csharp.Events.LowLevel.Event object:

Struct used to send and receive Events using the Event pattern. Contains the following fields (See Main concepts for more details on each field):

- Channel
- Metadata
- Body
- EventID - set internally
- Store - Boolean, set if the event should be sent to storage.
- ClientID

### Subscribe

This method allows subscribing to events. Both single and stream of events. Pass a delegate (callback) that will handle the incoming event(s). The implementation uses await and do not block the continuation of the code execution.

Parameters:

- SubscribeRequest - Mandatory-See SubscribeRequest.
- Handler - Mandatory. Delegate (callback) that will handle the incoming events.
- ErrorHandler  - Mandatory. Delegate (callback) that will handle the incoming exceptions.
- CancellationToken – Non-mandatory CancellationToken to cancel the Subscription.

``` csharp
            var ChannelName = "testing_event_channel"; 

            var ClientID = "hello-world-subscriber"; 

            var KubeMQServerAddress = "localhost:50000"; 

      

            var  subscriber = new KubeMQ.SDK.csharp.Events.Subscriber(KubeMQServerAddress); 

            try 

            { 

                subscriber.SubscribeToEvents(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest 

                { 

                    Channel = ChannelName, 

                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Events, 

                    ClientID = ClientID 

 

                }, (eventReceive) => 

                { 

            

                    Console.WriteLine($"Event Received: EventID:{eventReceive.EventID} Channel:{eventReceive.Channel} Metadata:{eventReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(eventReceive.Body)} "); 

                }, 

                (errorHandler) =>                  

                { 

                    Console.WriteLine(errorHandler.Message); 

                }); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```
### Send single

This method allows for sending a single event.



#### KubeMQ.SDK.csharp.Events.LowLevel.Event - Mandatory. The actual Event that will be sent.

Initialize Sender with server address from code (also can be initialized using config file):
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 
            var channel = new KubeMQ.SDK.csharp.Events.Channel(new KubeMQ.SDK.csharp.Events.ChannelParameters 

            { 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 
            try 

            { 

                var result = channel.SendEvent(new KubeMQ.SDK.csharp.Events.Event() 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending single event") 

                }); 

                if (!result.Sent) 

                { 

                    Console.WriteLine($"Could not send single message:{result.Error}"); 

                } 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

                           } 
```
### Send stream

This method allows for sending a stream of events. Use cases: sending a file in multiple packets; frequent high rate of events.

Initialize Sender with server address from code (also can be initialized using config file):
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

            var channel = new KubeMQ.SDK.csharp.Events.Channel(new KubeMQ.SDK.csharp.Events.ChannelParameters 

            { 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

 

            try 

            { 

                _ = channel.StreamEvent(new KubeMQ.SDK.csharp.Events.Event 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending stream event") 

                }); 

 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```

## Event Store

Employing persistent point to point Event communication style patterns.

The 'EventsStoreType' object:

To receive events from persistence, the subscriber needs to assign one of seven EventsStoreType and the value for EventsStoreTypeValue.

- EventsStoreTypeUndefined - 0 - Should be set when there is no need for eventsStore.(when using this type there is no need to set EventsStoreTypeValue)
- StartNewOnly - 1 - The subscriber will only receive new events (from the time he subscribed). (when using this type there is no need to set EventsStoreTypeValue)
- StartFromFirst - 2 - The subscriber will receive all events from the start of the queue and all future events as well. (when using this type there is no need to set EventsStoreTypeValue)
- StartFromLast - 3 - The subscriber will receive the last event in queue and all future events as well. (when using this type there is no need to set EventsStoreTypeValue)
- StartAtSequence - 4 - The subscriber will receive events from the chosen Sequence and all future events as well. (need to provide with long that of the wanted eventID)
- StartAtTime - 5 - The subscriber will receive events that were "Stored" from a specified DateTime and all future events as well. (need to provide with chosen time)
- StartAtTimeDelta - 6 - The subscriber will receive events that were "Stored" from the difference between DateTime.Now minus the delta was chosen. (need to provide with a long that represents the time delta to check within milliseconds)


### Subscribe
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-subscriber"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

            var subscriber = new KubeMQ.SDK.csharp.Events.Subscriber(KubeMQServerAddress); 

            try 

            { 

                subscriber.SubscribeToEvents(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest 

                { 

                    Channel = ChannelName, 

                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.EventsStore, 

                    ClientID = ClientID, 

                    EventsStoreType = KubeMQ.SDK.csharp.Subscription.EventsStoreType.StartFromFirst, 

                    EventsStoreTypeValue = 0 

 

                }, (eventReceive) => 

                { 

 

                    Console.WriteLine($"Event Received: EventID:{eventReceive.EventID} Channel:{eventReceive.Channel} Metadata:{eventReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(eventReceive.Body)} "); 

                }, 

                (errorHandler) => 

                { 

                    Console.WriteLine(errorHandler.Message); 

                }); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```

### Send Single
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

 

            var channel = new KubeMQ.SDK.csharp.Events.Channel(new KubeMQ.SDK.csharp.Events.ChannelParameters 

            { 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress, 

                Store = true 

            }); 

            for (int i = 0; i < 10; i++) 

            { 

                try 

                { 

                    var result = channel.SendEvent(new KubeMQ.SDK.csharp.Events.Event() 

                    { 

                        Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending single event store"), 

                        EventID = $"event-Store-{i}", 

                        Metadata = "some-metadata" 

                    }); 

                    if (!result.Sent) 

                    { 

                        Console.WriteLine($"Could not send single message:{result.Error}"); 

                    } 

                } 

                catch (Exception ex) 

                { 

                    Console.WriteLine(ex.Message); 

                } 

            } 
```

### Send Stream
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

            var channel = new KubeMQ.SDK.csharp.Events.Channel(new KubeMQ.SDK.csharp.Events.ChannelParameters 

            { 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

 

            try 

            { 

                for (int i = 0; i < 10; i++) 

                { 

                    _ = channel.StreamEvent(new KubeMQ.SDK.csharp.Events.Event 

                    { 

                        Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - stream event store") 

                    }); 

                } 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 

```

## Command

Request\Reply communication pattern.

- Subscribe to requests
- Send request

#### The KubeMQ.SDK.csharp.CommandQuery.LowLevel.Request object:

Struct used to send the request under the Request\Reply pattern. Contains the following fields (See Main concepts for more details on some field):

- RequestID - Optional. Used to match Request to Response. If omitted, it will be set internally.
- RequestType - Mandatory. Used to set if a response is expected or not.
- ClientID - Mandatory. Displayed in logs, tracing, and KubeMQ dashboard.
- Channel - Mandatory. The channel that the Responder subscribed on.
- Metadata - Mandatory.
- ReplyChannel - Read-only, set internally.
- Timeout - Mandatory. Max time for the response to return. Set per request. If exceeded an exception is thrown.

The Response object:

Struct used to send the response under the Request\Reply pattern.

The Response Constructors requires the corresponding 'Request' object.

Contains the following fields (See Main concepts for more details on some field):

- ClientID - Mandatory. Represents the sender ID the response was sent from.
- RequestID - Set internally, used to match Request to Response.
- Metadata - Optional.
- Body - Mandatory.
- Timestamp -Set Internally, an indication of the time the response was created.
- Executed - Boolean that represents the task the Responder was performed.
- Error - Mandatory - Represents if an error occurred while processing the request.

### Subscribe to Requests

This method allows subscribing to receive requests.

parameters:

SubscribeRequest - Mandatory - See SubscribeRequest.

Handler - Mandatory. Delegate (callback) that will handle the incoming requests.

Subscribe:
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-subscriber"; 

            var KubeMQServerAddress = "localhost:50000"; 
            KubeMQ.SDK.csharp.CommandQuery.Responder responder = new KubeMQ.SDK.csharp.CommandQuery.Responder(KubeMQServerAddress); 

            try 

            { 

                responder.SubscribeToRequests(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest() 

                { 

                    Channel = ChannelName, 

                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Commands, 

                    ClientID = ClientID 

                }, (commandReceive) => { 

                    Console.WriteLine($"Command Received: Id:{commandReceive.RequestID} Channel:{commandReceive.Channel} Metadata:{commandReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(commandReceive.Body)} "); 

                    return new KubeMQ.SDK.csharp.CommandQuery.Response(commandReceive) 

                    { 

                        Body = new byte[0], 

                        CacheHit = false, 

                        Error = "None", 

                        ClientID = ClientID, 

                        Executed = true, 

                        Metadata = string.Empty, 

                        Timestamp = DateTime.UtcNow, 

                    }; 

 

                }, (errorHandler) => 

                { 

                    Console.WriteLine(errorHandler.Message); 

                }); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```

### Send Request

The KubeMQ SDK comes with two similar methods to send a Request and wait for the Response.

SendRequestAsync returns the Response in a Task

SendRequest returns the Response to the Delegate (callback) supplied as a parameter.

#### Send Request Async

This method allows to send a request to the Responder; it awaits for the Response and returns it in a Task.

parameters:

KubeMQ.SDK.csharp.CommandQuery.LowLevel.Request - Mandatory. The Request object to send.

Initialize Initiator with server address from code (also can be initialized using config file):

Method: send request

This method allows to send a request to the Responder and returns the Response to the Delegate (callback) supplied as a parameter.

``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel(new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters 

            { 

                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query, 

                Timeout = 1000, 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

            try 

            { 

 

                var result = channel.SendRequest(new KubeMQ.SDK.csharp.CommandQuery.Request 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending a command, please reply") 

                }); 

 

                if (!result.Executed) 

                { 

                    Console.WriteLine($"Response error:{result.Error}"); 

                    return; 

                } 

                Console.WriteLine($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}"); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```

#### Send Request Async

``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel(new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters 

            { 

                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query, 

                Timeout = 1000, 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

            try 

            { 

 

                var result = await channel.SendRequestAsync(new KubeMQ.SDK.csharp.CommandQuery.Request 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending a command, please reply") 

                }); 

 

                if (!result.Executed) 

                { 

                    Console.WriteLine($"Response error:{result.Error}"); 

                    return; 

                } 

                Console.WriteLine($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}"); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```

## Query

Request\Reply communication pattern similar to Command. Allows caching the response at the KubeMQ

### Cache mechanism

KubeMQ server allows storing each response in a dedicated cache system. Each request can specify whether or not to use the cache. In case the cache is used, the KubeMQ server will try to return the response directly from the cache and reduce latency.

To use the cache mechanism, add the following parameters to each Request:

CacheKey - Unique key to store the response in the KubeMQ cache mechanism.

CacheTTL - Cache data Time to live in milliseconds per CacheKey.

In the Response object you will receive an indication whether it was returned from cache:

CacheHit - Indication if the response was returned from KubeMQ cache.


### Subscribe to Requests

``` csharp
Example:

  var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-subscriber"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

 

            KubeMQ.SDK.csharp.CommandQuery.Responder responder = new KubeMQ.SDK.csharp.CommandQuery.Responder(KubeMQServerAddress); 

            try 

            { 

                responder.SubscribeToRequests(new KubeMQ.SDK.csharp.Subscription.SubscribeRequest() 

                { 

                    Channel = ChannelName, 

                    SubscribeType = KubeMQ.SDK.csharp.Subscription.SubscribeType.Queries, 

                    ClientID = ClientID 

                }, (queryReceive) => { 

                    Console.WriteLine($"Command Received: Id:{queryReceive.RequestID} Channel:{queryReceive.Channel} Metadata:{queryReceive.Metadata} Body:{ KubeMQ.SDK.csharp.Tools.Converter.FromByteArray(queryReceive.Body)} "); 

                    return new KubeMQ.SDK.csharp.CommandQuery.Response(queryReceive) 

                    { 

                        Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("got your query, you are good to go"), 

                        CacheHit = false, 

                        Error = "None", 

                        ClientID = ClientID, 

                        Executed = true, 

                        Metadata = "this is a response", 

                        Timestamp = DateTime.UtcNow 

                    }; 

 

                }, (errorHandler) => 

                { 

                    Console.WriteLine(errorHandler.Message); 

                }); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```


### Send request
``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel(new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters 

            { 

                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query, 

                Timeout = 1000, 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

            try 

            { 

 

                var result = channel.SendRequest(new KubeMQ.SDK.csharp.CommandQuery.Request 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending a query, please reply") 

                }); 

 

                if (!result.Executed) 

                { 

                    Console.WriteLine($"Response error:{result.Error}"); 

                    return; 

                } 

                Console.WriteLine($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}"); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 
```
#### Send request async

``` csharp
var ChannelName = "testing_event_channel";

            var ClientID = "hello-world-sender"; 

            var KubeMQServerAddress = "localhost:50000"; 

 

            var channel = new KubeMQ.SDK.csharp.CommandQuery.Channel(new KubeMQ.SDK.csharp.CommandQuery.ChannelParameters 

            { 

                RequestsType = KubeMQ.SDK.csharp.CommandQuery.RequestType.Query, 

                Timeout = 1000, 

                ChannelName = ChannelName, 

                ClientID = ClientID, 

                KubeMQAddress = KubeMQServerAddress 

            }); 

            try 

            { 

 

                var result = await channel.SendRequestAsync(new KubeMQ.SDK.csharp.CommandQuery.Request 

                { 

                    Body = KubeMQ.SDK.csharp.Tools.Converter.ToByteArray("hello kubemq - sending a query, please reply") 

                }); 

 

                if (!result.Executed) 

                { 

                    Console.WriteLine($"Response error:{result.Error}"); 

                    return; 

                } 

                Console.WriteLine($"Response Received:{result.RequestID} ExecutedAt:{result.Timestamp}"); 

            } 

            catch (Exception ex) 

            { 

                Console.WriteLine(ex.Message); 

            } 

```

