# KubeMQ C# SDK

The **KubeMQ SDK for C#** enables C# developers to seamlessly communicate with the [KubeMQ](https://kubemq.io/) server, implementing various communication patterns such as Events, EventStore, Commands, Queries, and Queues.

<!-- TOC -->
* [KubeMQ C# SDK](#kubemq-c-sdk)
  * [Prerequisites](#prerequisites)
  * [Installation](#installation)
  * [Running Examples](#running-examples)
  * [SDK Overview](#sdk-overview)
  * [KubeMQ Client Configuration](#kubemq-client-configuration)
    * [Configuration Parameters](#configuration-parameters)
      * [TLS Configuration](#tls-configuration)
    * [Example Usage](#example-usage)
  * [Result Object](#result-object)
  * [PubSub Events Operations](#pubsub-events-operations)
    * [Create Channel](#create-channel)
      * [Request Parameters](#request-parameters)
      * [Response](#response)
      * [Example](#example)
    * [Delete Channel](#delete-channel)
      * [Request Parameters](#request-parameters-1)
      * [Response](#response-1)
      * [Example](#example-1)
    * [List Channels](#list-channels)
      * [Request Parameters](#request-parameters-2)
      * [Response](#response-2)
      * [Example](#example-2)
    * [Send & Subscribe Event](#send--subscribe-event)
      * [Send Request: `Event`](#send-request-event)
      * [Response](#response-3)
      * [Subscribe Request: `EventsSubscription`](#subscribe-request-eventssubscription)
      * [Callback: `EventMessageReceived` Class Detail](#callback-eventmessagereceived-class-detail)
      * [Example](#example-3)
  * [PubSub EventsStore Operations](#pubsub-eventsstore-operations)
    * [Create Channel](#create-channel-1)
      * [Request Parameters](#request-parameters-3)
      * [Response](#response-4)
      * [Example](#example-4)
    * [Delete Channel](#delete-channel-1)
      * [Request Parameters](#request-parameters-4)
      * [Response](#response-5)
      * [Example](#example-5)
    * [List Channels](#list-channels-1)
      * [Request Parameters](#request-parameters-5)
      * [Response](#response-6)
      * [Example](#example-6)
    * [Send & Subscribe EventStore](#send--subscribe-eventstore-)
      * [Request: `EventStore` Class Attributes](#request-eventstore-class-attributes)
    * [Subscribe To EventsStore Messages](#subscribe-to-eventsstore-messages)
      * [Request: `EventsStoreSubscription` Class Attributes](#request-eventsstoresubscription-class-attributes)
      * [StartAtType Options](#startattype-options)
      * [Example](#example-7)
  * [Commands & Queries – Commands Operations](#commands--queries--commands-operations)
    * [Create Channel](#create-channel-2)
      * [Request Parameters](#request-parameters-6)
      * [Response](#response-7)
      * [Example](#example-8)
    * [Delete Channel](#delete-channel-2)
      * [Request Parameters](#request-parameters-7)
      * [Response](#response-8)
      * [Example](#example-9)
    * [List Channels](#list-channels-2)
      * [Request Parameters](#request-parameters-8)
      * [Response](#response-9)
      * [Example](#example-10)
    * [Send Receive Response Command](#send-receive-response-command-)
      * [Request: `Command` Class Attributes](#request-command-class-attributes)
      * [Response: `CommandResponse` Class Attributes](#response-commandresponse-class-attributes)
    * [Subscribe To Commands](#subscribe-to-commands)
      * [Request: `CommandsSubscription` Class Attributes](#request-commandssubscription-class-attributes)
      * [Example](#example-11)
  * [Commands & Queries – Queries Operations](#commands--queries--queries-operations)
    * [Create Channel](#create-channel-3)
      * [Request Parameters](#request-parameters-9)
      * [Response](#response-10)
      * [Example](#example-12)
    * [Delete Channel](#delete-channel-3)
      * [Request Parameters](#request-parameters-10)
      * [Response](#response-11)
      * [Example](#example-13)
    * [List Channels](#list-channels-3)
      * [Request Parameters](#request-parameters-11)
      * [Response](#response-12)
      * [Example](#example-14)
    * [Send Receive Response Query](#send-receive-response-query)
      * [Request: `Query` Class Attributes](#request-query-class-attributes)
      * [Response: `QueryResponse` Class Attributes](#response-queryresponse-class-attributes)
    * [Subscribe To Commands](#subscribe-to-commands-1)
      * [Request: `QueriesSubscription` Class Attributes](#request-queriessubscription-class-attributes)
      * [Example](#example-15)
  * [Queues Operations](#queues-operations)
    * [Create Channel](#create-channel-4)
      * [Request Parameters](#request-parameters-12)
      * [Response](#response-13)
      * [Example](#example-16)
    * [Delete Channel](#delete-channel-4)
      * [Request Parameters](#request-parameters-13)
      * [Response](#response-14)
      * [Example](#example-17)
    * [List Channels](#list-channels-4)
      * [Request Parameters](#request-parameters-14)
      * [Response](#response-15)
      * [Example](#example-18)
    * [Send / Receive Queue Messages](#send--receive-queue-messages)
      * [Send Request: `QueueMessage`](#send-request-queuemessage)
      * [Policy Options](#policy-options)
    * [Send Queue Message](#send-queue-message)
      * [Request: `QueueMessage` Class Attributes](#request-queuemessage-class-attributes)
      * [Receive Request: `PollRequest`](#receive-request-pollrequest)
      * [Response: `PollResponse`](#response-pollresponse)
      * [Example #1](#example-1)
      * [Example #2](#example-2)
      * [Example #3](#example-3)
      * [Example #4](#example-4)
      * [Example #5](#example-5)
<!-- TOC -->
## Prerequisites

- .Net Core 5.0 or later
- .Net Framework 4.6.1 or later
- .Net Standard 2.0 or later
- KubeMQ server running locally or accessible over the network


## Installation

The KubeMQ SDK for C# is available as a NuGet package. You can install it using the following command:

```bash
dotnet add package KubeMQ.SDK.csharp
```

## Running Examples

The [examples](https://github.com/kubemq-io/kubemq-CSharp/tree/master/Examples) are standalone projects that showcase the usage of the SDK. To run the examples, ensure you have a running instance of KubeMQ. 

## SDK Overview

The SDK implements all communication patterns available through the KubeMQ server:
- PubSub
    - Events
    - EventStore
- Commands & Queries (CQ)
    - Commands
    - Queries
- Queues

## KubeMQ Client Configuration

All KubeMQ clients (PubSubClient, QueuesClient, and CQClient) share the same configuration parameters. To create any client instance, you need to use the respective builder with at least two mandatory parameters: `address` (KubeMQ server address) and `clientId`.

### Configuration Parameters

The table below describes all available configuration parameters:

| Name                     | Type      | Description                                             | Default Value     | Mandatory                 |
|--------------------------|-----------|---------------------------------------------------------|-------------------|---------------------------|
| Address                  | string    | The address of the KubeMQ server.                       | None              | Yes                       |
| ClientId                 | string    | The client ID used for authentication.                  | None              | Yes                       |
| AuthToken                | string    | The authorization token for secure communication.       | None              | No                        |
| Tls                      | TlsConfig | Enable or disable TLS for secure communication.         | false             | No                        |
| MaxSendSize              | int       | The maximum size of the messages to send (in bytes).    | 104857600 (100MB) | No                        |
| MaxReceiveSize           | int       | The maximum size of the messages to receive (in bytes). | 104857600 (100MB) | No                        |
| ReconnectIntervalSeconds | int       | The interval in seconds between reconnection attempts.  | 5                 | No                        |

#### TLS Configuration


| Name     | Type   | Description                                             | Default Value     | Mandatory                 |
|----------|--------|---------------------------------------------------------|-------------------|---------------------------|
| Enabled  | bool   | Enable or disable TLS for secure communication.         | false             | No                        |
| CertFile | string | The path to the TLS certificate file.                   | None              | No (Yes if `tls` is true) |
| KeyFile  | string | The path to the TLS key file.                           | None              | No (Yes if `tls` is true) |
| CaFile   | string | The path to the TLS CA file.                            | None              | No (Yes if `tls` is true) |

### Example Usage

Here's an example of how to create a client instance (using PubSubClient as an example):

```csharp
static async Task<CommandsClient> CreateCommandsClient()
        {
            Configuration cfg = new Configuration().
                SetAddress("localhost:50000").
                SetClientId("Some-client-id").
                SetAuthToken("some-auth-token").
                SetMaxReceiveSize(1024).
                SetMaxSendSize(1024).
                SetReconnectIntervalSeconds(10).
                SetTls( new TlsConfig().
                    SetEnabled(true).
                    SetCertFile("path to cert file").
                    SetKeyFile("path to key file").
                    SetCaFile("path to ca file"));
            CommandsClient client = new CommandsClient();
            Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
            if (!connectResult.IsSuccess)
            {
                Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
                throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            }
            return client;
        }
```
## Result Object
In many cases, the SDK methods return a `Result` object.
The `Result` object is a simple class that contains two attributes: `IsSuccess` and `ErrorMessage`. It is used to indicate the success or failure of an operation and to provide an error message in case of failure.

## PubSub Events Operations

### Create Channel

Create a new Events channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to create  | None          | Yes       |

#### Response

Return [Result](#result-object) object 

#### Example

```csharp
static async Task<EventsClient> CreateEventsClient()
    {
        Configuration cfg = new Configuration().
            SetAddress("localhost:50000").
            SetClientId("Some-client-id");
        EventsClient client = new EventsClient();
        Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
        if (!connectResult.IsSuccess)
        {
            Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
        }
        return client;
    }
static async Task CreateEventsChannel()
{
    EventsClient client =await CreateEventsClient();
    Result result = await client.Create("events_1");
    if (!result.IsSuccess)
    {
        Console.WriteLine($"Could not create events channel, error:{result.ErrorMessage}");
        return;
    }
    Console.WriteLine("Eventss Channel Created");
    await client.Close();
}
```

### Delete Channel

Delete an existing Events channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to delete  | None          | Yes       |


#### Response

Return [Result](#result-object) object

#### Example

```csharp
static async Task<EventsClient> CreateEventsClient()
    {
        Configuration cfg = new Configuration().
            SetAddress("localhost:50000").
            SetClientId("Some-client-id");
        EventsClient client = new EventsClient();
        Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
        if (!connectResult.IsSuccess)
        {
            Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
        }
        return client;
    }
static async Task DeleteEventsChannel()
    {
        EventsClient client =await CreateEventsClient();
        Result result = await client.Delete("events_1");
        if (!result.IsSuccess)
        {
            Console.WriteLine($"Could not delete events channel, error:{result.ErrorMessage}");
            return;
        }
        Console.WriteLine("Eventss Channel Deleted");
        await client.Close();
    }
```

### List Channels

Retrieve a list of Events channels.

#### Request Parameters

| Name         | Type   | Description                                | Default Value | Mandatory |
|--------------|--------|--------------------------------------------|---------------|-----------|
| searchQuery  | string | Search query to filter channels (optional) | None          | No        |

#### Response

Returns a `ListPubSubAsyncResult` where each `PubSubChannel` has the following attributes:

| Name         | Type        | Description                                                                                   |
|--------------|-------------|-----------------------------------------------------------------------------------------------|
| Name         | string      | The name of the Pub/Sub channel.                                                              |
| Type         | string      | The type of the Pub/Sub channel.                                                              |
| LastActivity | long        | The timestamp of the last activity on the channel, represented in milliseconds since epoch.   |
| IsActive     | boolean     | Indicates whether the channel is active or not.                                               |
| Incoming     | PubSubChannel | The statistics related to incoming messages for this channel.                                 |
| Outgoing     | PubSubChannel | The statistics related to outgoing messages for this channel.                                 |

#### Example

```csharp
static async Task<EventsClient> CreateEventsClient()
    {
        Configuration cfg = new Configuration().
            SetAddress("localhost:50000").
            SetClientId("Some-client-id");
        EventsClient client = new EventsClient();
        Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
        if (!connectResult.IsSuccess)
        {
            Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
        }
        return client;
    }
static async Task ListEventsChannels()
    {
        EventsClient client =await CreateEventsClient();
        ListPubSubAsyncResult listResult = await client.List();
        if (!listResult.IsSuccess)
        {
            Console.WriteLine($"Could not list events channels, error:{listResult.ErrorMessage}");
            return;
        }
        
        foreach (var channel in listResult.Channels)
        {
            Console.WriteLine($"{channel}");
        }
        await client.Close();
    }
```

### Send & Subscribe Event

Send and subscribe to event messages.

#### Send Request: `Event`

| Name     | Type                | Description                                                | Default Value    | Mandatory |
|----------|---------------------|------------------------------------------------------------|------------------|-----------|
| Id       | String              | Unique identifier for the event message.                   | None             | No        |
| Channel  | String              | The channel to which the event message is sent.            | None             | Yes       |
| Metadata | String              | Metadata associated with the event message.                | None             | No        |
| Body     | byte[]              | Body of the event message in bytes.                        | Empty byte array | No        |
| Tags     | Map<String, String> | Tags associated with the event message as key-value pairs. | Empty Map        | No        |

#### Response

Return [Result](#result-object) object


#### Subscribe Request: `EventsSubscription`

| Name                  | Type                           | Description                                                              | Default Value | Mandatory |
|-----------------------|--------------------------------|--------------------------------------------------------------------------|---------------|-----------|
| Channel               | String                         | The channel to subscribe to.                                             | None          | Yes       |
| Group                 | String                         | The group to subscribe with.                                             | None          | No        |
| ReceiveEventHandler   | delegate(EventMessageReceived) | Callback function to be called when an event message is received.        | None          | Yes       |
| ErrorHandler          | delegate(Exception)            | Callback function to be called when an error occurs.                     | None          | No        |

#### Callback: `EventMessageReceived` Class Detail

| Name         | Type                | Description                                             |
|--------------|---------------------|---------------------------------------------------------|
| Id           | string              | The unique identifier of the message.                   |
| FromClientId | string              | The ID of the client that sent the message.             |
| Timestamp    | long                | The timestamp when the message was received, in seconds |
| Channel      | string              | The channel to which the message belongs.               |
| Metadata     | string              | The metadata associated with the message.               |
| Body         | byte[]              | The body of the message.                                |
| Tags         | Map<string, string> | The tags associated with the message.                   |

#### Example

```csharp
static async Task<EventsClient> CreateEventsClient()
    {
        Configuration cfg = new Configuration().
            SetAddress("localhost:50000").
            SetClientId("Some-client-id");
        EventsClient client = new EventsClient();
        Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
        if (!connectResult.IsSuccess)
        {
            Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
            throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
        }
        return client;
    }
static async Task SendSubscribe()
    {
        EventsClient client =await CreateEventsClient();
        var subscription = new EventsSubscription()
            .SetChannel("e1")
            .SetGroup("")
            .SetOnReceiveEvent(receivedEvent =>
            {
                Console.WriteLine($"Event Received: Id:{receivedEvent.Id}, Body:{Encoding.UTF8.GetString(receivedEvent.Body)}");
            })
            .SetOnError(exception =>
            {
                Console.WriteLine($"Error: {exception.Message}");
            });
        Result subscribeResult =  client.Subscribe(subscription);
        if (!subscribeResult.IsSuccess)
        {
            Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
            return;
        }
        Thread.Sleep(1000);
        Event msg = new Event().SetChannel("e1").SetBody("hello kubemq - sending an event message"u8.ToArray());
        Result sendResult=  await client.Send(msg);
        if (!sendResult.IsSuccess)
        {
            Console.WriteLine($"Could not send an event to KubeMQ Server, error:{sendResult.ErrorMessage}");
            return;
        }
        await  client.Close ();
    }
```


## PubSub EventsStore Operations

### Create Channel

Create a new EventsStore channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to create  | None          | Yes       |

#### Response

Return [Result](#result-object) object

#### Example

```csharp
static async Task<EventsStoreClient> CreateEventsStoresClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      EventsStoreClient client = new EventsStoreClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task CreateEventsStoresChannel()
  {
      EventsStoreClient client =await CreateEventsStoresClient();
      Result result = await client.Create("events_store_1");
      if (!result.IsSuccess)
      {
          Console.WriteLine($"Could not create events-store channel, error:{result.ErrorMessage}");
          return;
      }
      Console.WriteLine("EventsStores Channel Created");
      await client.Close();
  }
```

### Delete Channel

Delete an existing EventsStore channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to delete  | None          | Yes       |


#### Response

Return [Result](#result-object) object


#### Example

```csharp
static async Task<EventsStoreClient> CreateEventsStoresClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      EventsStoreClient client = new EventsStoreClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task DeleteEventsStoresChannel()
    {
        EventsStoreClient client =await CreateEventsStoresClient();
        Result result = await client.Delete("events_store_1");
        if (!result.IsSuccess)
        {
            Console.WriteLine($"Could not delete events-store channel, error:{result.ErrorMessage}");
            return;
        }
        Console.WriteLine("EventsStores Channel Deleted");
        await client.Close();
    }
```

### List Channels

Retrieve a list of EventsStore channels.

#### Request Parameters

| Name         | Type   | Description                                | Default Value | Mandatory |
|--------------|--------|--------------------------------------------|---------------|-----------|
| searchQuery  | string | Search query to filter channels (optional) | None          | No        |

#### Response

Returns a `ListPubSubAsyncResult` where each `PubSubChannel` has the following attributes:

| Name         | Type        | Description                                                                                   |
|--------------|-------------|-----------------------------------------------------------------------------------------------|
| Name         | string      | The name of the Pub/Sub channel.                                                              |
| Type         | string      | The type of the Pub/Sub channel.                                                              |
| LastActivity | long        | The timestamp of the last activity on the channel, represented in milliseconds since epoch.   |
| IsActive     | boolean     | Indicates whether the channel is active or not.                                               |
| Incoming     | PubSubChannel | The statistics related to incoming messages for this channel.                                 |
| Outgoing     | PubSubChannel | The statistics related to outgoing messages for this channel.                                 |

#### Example

```csharp
static async Task<EventsStoreClient> CreateEventsStoresClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      EventsStoreClient client = new EventsStoreClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task ListEventsStoresChannels()
  {
      EventsStoreClient client =await CreateEventsStoresClient();
      ListPubSubAsyncResult listResult = await client.List();
      if (!listResult.IsSuccess)
      {
          Console.WriteLine($"Could not list events-store channels, error:{listResult.ErrorMessage}");
          return;
      }
      
      foreach (var channel in listResult.Channels)
      {
          Console.WriteLine($"{channel}");
      }
      await client.Close();
  }
```

###  Send & Subscribe EventStore 

Send and subscribe to event messages.

#### Request: `EventStore` Class Attributes

| Name     | Type                | Description                                                | Default Value    | Mandatory |
|----------|---------------------|------------------------------------------------------------|------------------|-----------|
| Id       | String              | Unique identifier for the event message.                   | None             | No        |
| Channel  | String              | The channel to which the event message is sent.            | None             | Yes       |
| Metadata | String              | Metadata associated with the event message.                | None             | No        |
| Body     | byte[]              | Body of the event message in bytes.                        | Empty byte array | No        |
| Tags     | Map<String, String> | Tags associated with the event message as key-value pairs. | Empty Map        | No        |

### Subscribe To EventsStore Messages

Subscribes to receive messages from an EventsStore channel.

#### Request: `EventsStoreSubscription` Class Attributes

| Name                   | Type                                | Description                                                           | Default Value | Mandatory   |
|------------------------|-------------------------------------|-----------------------------------------------------------------------|---------------|-------------|
| Channel                | string                              | The channel to subscribe to.                                          | None          | Yes         |
| Group                  | string                              | The group to subscribe with.                                          | None          | No          |
| ReceiveEventHandler | delegate(EventStore) | Callback function to be called when an event message is received.     | None          | Yes         |
| OnErrorCallback        | delegate(Exception)                 | Callback function to be called when an error occurs.                  | None          | No          |
| StartAt                | StartAtType                         | Type of EventsStore subscription (e.g., StartAtTime, StartAtSequence) | None          | Yes         |
| StartAtTimeValue       | long                                | Start time for EventsStore subscription (if applicable)               | None          | Conditional |
| StartAtSequenceValue   | long                                | Start sequence for EventsStore subscription (if applicable)           | None          | Conditional |


#### StartAtType Options

| Type              | Value | Description                                                        |
|-------------------|-------|--------------------------------------------------------------------|
| Undefined         | 0     | Default value, should be explicitly set to a valid type before use |
| StartNewOnly      | 1     | Start storing events from the point when the subscription is made  |
| StartFromFirst    | 2     | Start storing events from the first event available                |
| StartFromLast     | 3     | Start storing events from the last event available                 |
| StartAtSequence   | 4     | Start storing events from a specific sequence number               |
| StartAtTime       | 5     | Start storing events from a specific point in time                 |
| StartAtTimeDelta  | 6     | Start storing events from a specific time delta in seconds         |



#### Example

```csharp
static async Task<EventsStoreClient> CreateEventsStoresClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      EventsStoreClient client = new EventsStoreClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task SendSubscribe()
  {
      EventsStoreClient client =await CreateEventsStoresClient();
      var subscription = new EventsStoreSubscription()
          .SetChannel("es1")
          .SetGroup("")
          .SetStartAtType(StartAtType.StartAtTypeFromSequence)
          .SetStartAtSequence(1)
          .SetOnReceiveEvent(receivedEvent =>
          {
              Console.WriteLine($"Event Store Received: Id:{receivedEvent.Id}, Body:{Encoding.UTF8.GetString(receivedEvent.Body)}");
          })
          .SetOnError(exception =>
          {
              Console.WriteLine($"Error: {exception.Message}");
          });
      Result subscribeResult =  client.Subscribe(subscription);
      if (!subscribeResult.IsSuccess)
      {
          Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
          return;
      }
      Thread.Sleep(1000);
      EventStore msg = new EventStore().SetChannel("es1").SetBody("hello kubemq - sending an event store message"u8.ToArray());
      Result sendResult=  await client.Send(msg);
      if (!sendResult.IsSuccess)
      {
          Console.WriteLine($"Could not send an event to KubeMQ Server, error:{sendResult.ErrorMessage}");
          return;
      }
      await  client.Close ();
  }
```

## Commands & Queries – Commands Operations

### Create Channel

Create a new Command channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to create  | None          | Yes       |

#### Response

Return [Result](#result-object) object

#### Example

```csharp
static async Task<CommandsClient> CreateCommandsClient()
  {
      Configuration cfg = new Configuration().SetAddress("localhost:50000").SetClientId("Some-client-id");
          
      CommandsClient client = new CommandsClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
  
  static async Task CreateCommandsChannel()
  {
      CommandsClient client =await CreateCommandsClient();
      Result result = await client.Create("command_1");
      if (!result.IsSuccess)
      {
          Console.WriteLine($"Could not create commands channel, error:{result.ErrorMessage}");
          return;
      }
      Console.WriteLine("Commands Channel Created");
      await client.Close();
  }
```

### Delete Channel

Delete an existing Command channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to delete  | None          | Yes       |

#### Response

Return [Result](#result-object) object

#### Example

```csharp
static async Task<CommandsClient> CreateCommandsClient()
  {
      Configuration cfg = new Configuration().SetAddress("localhost:50000").SetClientId("Some-client-id");
          
      CommandsClient client = new CommandsClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task DeleteCommandsChannel()
  {
      CommandsClient client =await CreateCommandsClient();
      Result result = await client.Delete("command_1");
      if (!result.IsSuccess)
      {
          Console.WriteLine($"Could not delete commands channel, error:{result.ErrorMessage}");
          return;
      }
      Console.WriteLine("Commands Channel Deleted");
      await client.Close();
  }
```

### List Channels

Retrieve a list of Command channels.

#### Request Parameters

| Name         | Type   | Description                                | Default Value | Mandatory |
|--------------|--------|--------------------------------------------|---------------|-----------|
| searchstring | string | Search query to filter channels (optional) | None          | No        |

#### Response

Returns a `ListCqAsyncResult` where each `CQChannel` has the following attributes:

| Name          | Type      | Description                                         |
|---------------|-----------|-----------------------------------------------------|
| Name          | string    | The name of the channel.                            |
| Type          | string    | The type of the channel.                            |
| LastActivity  | long      | The timestamp of the last activity on the channel   |
| IsActive      | boolean   | Indicates whether the channel is currently active   |
| Incoming      | CQChannel | Statistics about incoming messages to the channel   |
| Outgoing      | CQChannel | Statistics about outgoing messages from the channel |

#### Example

```csharp
static async Task<CommandsClient> CreateCommandsClient()
  {
      Configuration cfg = new Configuration().SetAddress("localhost:50000").SetClientId("Some-client-id");
          
      CommandsClient client = new CommandsClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task ListCommandsChannels()
  {
      CommandsClient client =await CreateCommandsClient();
      ListCqAsyncResult listResult = await client.List();
      if (!listResult.IsSuccess)
      {
          Console.WriteLine($"Could not list commands channels, error:{listResult.ErrorMessage}");
          return;
      }
      
      foreach (var channel in listResult.Channels)
      {
          Console.WriteLine($"{channel}");
      }
      await client.Close();
  }
```

### Send Receive Response Command 

Send a command request to a Command channel.

#### Request: `Command` Class Attributes

| Name             | Type                | Description                                                                            | Default Value     | Mandatory |
|------------------|---------------------|----------------------------------------------------------------------------------------|-------------------|-----------|
| Id               | string              | The ID of the command message.                                                         | None              | Yes       |
| Channel          | string              | The channel through which the command message will be sent.                            | None          | Yes       |
| Metadata         | string              | Additional metadata associated with the command message.                               | None             | No        |
| Body             | byte[]              | The body of the command message as bytes.                                              | Empty byte array  | No        |
| Tags             | Map<string, string> | A dictionary of key-value pairs representing tags associated with the command message. | Empty Map | No |
| TimeoutInSeconds | int                 | The maximum time in seconds for waiting to response.                                   | None    | Yes       |

#### Response: `CommandResponse` Class Attributes

| Name            | Type                   | Description                                          |
|-----------------|------------------------|------------------------------------------------------|
| CommandReceived | CommandMessageReceived | The command message received in the response.        |
| ClientId        | string                 | The client ID associated with the command response.  |
| RequestId       | string                 | The unique request ID of the command response.       |
| IsExecuted      | boolean                | Indicates if the command has been executed.          |
| Timestamp       | Timestamp              | The timestamp when the command response was created. |
| Error           | string                 | The error message if there was an error.             |


### Subscribe To Commands

Subscribes to receive command messages from a Command channel.

#### Request: `CommandsSubscription` Class Attributes

| Name                   | Type                               | Description                                   | Default Value | Mandatory    |
|------------------------|------------------------------------|-----------------------------------------------|---------------|--------------|
| Channel                | string                             | The channel for the subscription.             | None          | Yes         |
| Group                  | string                             | The group associated with the subscription.   | None          | No          |
| ReceivedCommandHandler | delegate(CommandMessageReceived)   | Callback function for receiving commands.     | None          | Yes         |
| ErrorHandler           | delegate(Exception)                | Callback function for error handling.        | None          | No           |

#### Example

```csharp
static async Task<CommandsClient> CreateCommandsClient()
  {
      Configuration cfg = new Configuration().SetAddress("localhost:50000").SetClientId("Some-client-id");
          
      CommandsClient client = new CommandsClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task SendReceiveResponse()
  {
      CommandsClient client =await CreateCommandsClient();
      var subscription = new CommandsSubscription()
          .SetChannel("c1")
          .SetGroup("")
          .SetOnReceivedCommand(async receivedCommand =>
          {
              Console.WriteLine($"Command Received: Id:{receivedCommand.Id}, Body:{Encoding.UTF8.GetString(receivedCommand.Body)}");
              CommandResponse response = new CommandResponse()
                  .SetRequestId(receivedCommand.Id)
                  .SetCommandReceived(receivedCommand)
                  .SetIsExecuted(true);
              Result responseResult = await client.Response(response);
              if (!responseResult.IsSuccess)
              {
                  Console.WriteLine($"Error sending response to KubeMQ, error:{responseResult.ErrorMessage}");
              }
              Console.WriteLine($"Command Executed: Id:{receivedCommand.Id}");
  
          })
          .SetOnError(exception =>
          {
              Console.WriteLine($"Error: {exception.Message}");
          });
      Result subscribeResult =  client.Subscribe(subscription);
      if (!subscribeResult.IsSuccess)
      {
          Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
          return;
      }
      Thread.Sleep(1000);
      Command msg = new Command()
          .SetChannel("c1")
          .SetBody("hello kubemq - sending a command message"u8.ToArray())
          .SetTimeout(10);
      CommandResponse sendResult=  await client.Send(msg);
      Console.WriteLine($"Command Response: {sendResult}");
      await  client.Close ();
  }
```


## Commands & Queries – Queries Operations

### Create Channel

Create a new Query channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to create  | None          | Yes       |

#### Response

Return [Result](#result-object) object


#### Example

```csharp
static async Task<QueriesClient> CreateQueriesClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      QueriesClient client = new QueriesClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
  static async Task CreateQueriesChannel()
  {
      QueriesClient client =await CreateQueriesClient();
      Result result = await client.Create("query_1");
      if (!result.IsSuccess)
      {
          Console.WriteLine($"Could not create queries channel, error:{result.ErrorMessage}");
          return;
      }
      Console.WriteLine("Queries Channel Created");
      await client.Close();
  }
```

### Delete Channel

Delete an existing Query channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to delete  | None          | Yes       |


#### Response

Return [Result](#result-object) object

#### Example

```csharp
static async Task<QueriesClient> CreateQueriesClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      QueriesClient client = new QueriesClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task DeleteQueriesChannel()
  {
      QueriesClient client =await CreateQueriesClient();
      Result result = await client.Delete("query_1");
      if (!result.IsSuccess)
      {
          Console.WriteLine($"Could not delete queries channel, error:{result.ErrorMessage}");
          return;
      }
      Console.WriteLine("Queries Channel Deleted");
      await client.Close();
  }
```

### List Channels

Retrieve a list of Query channels.

#### Request Parameters

| Name         | Type   | Description                                | Default Value | Mandatory |
|--------------|--------|--------------------------------------------|---------------|-----------|
| searchstring | string | Search query to filter channels (optional) | None          | No        |

#### Response

Returns a `ListCqAsyncResult` where each `CQChannel` has the following attributes:

| Name          | Type      | Description                                         |
|---------------|-----------|-----------------------------------------------------|
| Name          | string    | The name of the channel.                            |
| Type          | string    | The type of the channel.                            |
| LastActivity  | long      | The timestamp of the last activity on the channel   |
| IsActive      | boolean   | Indicates whether the channel is currently active   |
| Incoming      | CQChannel | Statistics about incoming messages to the channel   |
| Outgoing      | CQChannel | Statistics about outgoing messages from the channel |


#### Example

```csharp
static async Task<QueriesClient> CreateQueriesClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      QueriesClient client = new QueriesClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task ListQueriesChannels()
  {
      QueriesClient client =await CreateQueriesClient();
      ListCqAsyncResult listResult = await client.List();
      if (!listResult.IsSuccess)
      {
          Console.WriteLine($"Could not list queries channels, error:{listResult.ErrorMessage}");
          return;
      }
      
      foreach (var channel in listResult.Channels)
      {
          Console.WriteLine($"{channel}");
      }
      await client.Close();
  }
```

### Send Receive Response Query

Send a query request to a Query channel.

#### Request: `Query` Class Attributes

| Name             | Type                | Description                                                                            | Default Value     | Mandatory |
|------------------|---------------------|----------------------------------------------------------------------------------------|-------------------|-----------|
| Id               | string              | The ID of the command message.                                                         | None              | Yes       |
| Channel          | string              | The channel through which the command message will be sent.                            | None          | Yes       |
| Metadata         | string              | Additional metadata associated with the command message.                               | None             | No        |
| Body             | byte[]              | The body of the command message as bytes.                                              | Empty byte array  | No        |
| Tags             | Map<string, string> | A dictionary of key-value pairs representing tags associated with the command message. | Empty Map | No |
| TimeoutInSeconds | int                 | The maximum time in seconds for waiting to response.                                   | None    | Yes       |

#### Response: `QueryResponse` Class Attributes

| Name            | Type                   | Description                                          |
|-----------------|------------------------|------------------------------------------------------|
| CommandReceived | CommandMessageReceived | The command message received in the response.        |
| ClientId        | string                 | The client ID associated with the command response.  |
| RequestId       | string                 | The unique request ID of the command response.       |
| IsExecuted      | boolean                | Indicates if the command has been executed.          |
| Timestamp       | Timestamp              | The timestamp when the command response was created. |
| Error           | string                 | The error message if there was an error.             |
| Metadata        | string                 | Additional metadata associated with the response.    |
| Body            | byte[]                 | The body of the query response as bytes.             |


### Subscribe To Commands

Subscribes to receive query messages from a Query channel.

#### Request: `QueriesSubscription` Class Attributes

| Name                   | Type                               | Description                                   | Default Value | Mandatory    |
|------------------------|------------------------------------|-----------------------------------------------|---------------|--------------|
| Channel                | string                             | The channel for the subscription.             | None          | Yes         |
| Group                  | string                             | The group associated with the subscription.   | None          | No          |
| ReceivedQueryHandler | delegate(QueryMessageReceived)     | Callback function for receiving queries.      | None          | Yes         |
| ErrorHandler           | delegate(Exception)                | Callback function for error handling.        | None          | No           |

#### Example

```csharp
static async Task<QueriesClient> CreateQueriesClient()
  {
      Configuration cfg = new Configuration().
          SetAddress("localhost:50000").
          SetClientId("Some-client-id");
      QueriesClient client = new QueriesClient();
      Result connectResult = await client.Connect(cfg,new CancellationTokenSource().Token);
      if (!connectResult.IsSuccess)
      {
          Console.WriteLine($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
          throw new Exception($"Could not connect to KubeMQ Server, error:{connectResult.ErrorMessage}");
      }
      return client;
  }
static async Task SendReceiveResponse()
{
    QueriesClient client =await CreateQueriesClient();
    var subscription = new QueriesSubscription()
        .SetChannel("q1")
        .SetGroup("")
        .SetOnReceivedQuery(async receivedQuery =>
        {
            Console.WriteLine($"Query Received: Id:{receivedQuery.Id}, Body:{Encoding.UTF8.GetString(receivedQuery.Body)}");
            QueryResponse response = new QueryResponse()
                .SetRequestId(receivedQuery.Id)
                .SetQueryReceived(receivedQuery)
                .SetIsExecuted(true)
                .SetBody(Encoding.UTF8.GetBytes("query response"));
            Result responseResult = await client.Response(response);
            if (!responseResult.IsSuccess)
            {
                Console.WriteLine($"Error sending response to KubeMQ, error:{responseResult.ErrorMessage}");
            }
            Console.WriteLine($"Query Executed: Id:{receivedQuery.Id}");

        })
        .SetOnError(exception =>
        {
            Console.WriteLine($"Error: {exception.Message}");
        });
      Result subscribeResult =  client.Subscribe(subscription);
      if (!subscribeResult.IsSuccess)
      {
          Console.WriteLine($"Could not subscribe to KubeMQ Server, error:{subscribeResult.ErrorMessage}");
          return;
      }
      Thread.Sleep(1000);
      Query msg = new Query()
          .SetChannel("q1")
          .SetBody("hello kubemq - sending a query message"u8.ToArray())
          .SetTimeout(10);
      QueryResponse sendResult=  await client.Send(msg);
      Console.WriteLine($"Query Response: {sendResult}");
      await  client.Close ();
}
```
## Queues Operations

### Create Channel

Create a new Queue channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to create  | None          | Yes       |

#### Response

Return [Result](#result-object) object

#### Example

```csharp
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
```

### Delete Channel

Delete an existing Queue channel.

#### Request Parameters

| Name        | Type   | Description                             | Default Value | Mandatory |
|-------------|--------|-----------------------------------------|---------------|-----------|
| channelName | string | Name of the channel you want to delete  | None          | Yes       |

#### Response

Return [Result](#result-object) object

#### Example

```csharp
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
```

### List Channels

Retrieve a list of Queue channels.

#### Request Parameters

| Name         | Type   | Description                                | Default Value | Mandatory |
|--------------|--------|--------------------------------------------|---------------|-----------|
| searchstring | string | Search query to filter channels (optional) | None          | No        |

#### Response

Returns a `ListQueuesAsyncResult` where each `QueuesChannel` has the following attributes:

| Name         | Type        | Description                                                                                   |
|--------------|-------------|-----------------------------------------------------------------------------------------------|
| Name         | string      | The name of the Pub/Sub channel.                                                              |
| Type         | string      | The type of the Pub/Sub channel.                                                              |
| LastActivity | long        | The timestamp of the last activity on the channel, represented in milliseconds since epoch.   |
| IsActive     | boolean     | Indicates whether the channel is active or not.                                               |
| Incoming     | PubSubChannel | The statistics related to incoming messages for this channel.                                 |
| Outgoing     | PubSubChannel | The statistics related to outgoing messages for this channel.                                 |


#### Example

```csharp
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
```

### Send / Receive Queue Messages

Send and receive messages from a Queue channel.

#### Send Request: `QueueMessage`

| Name                    | Type                | Description                                                                                         | Default Value   | Mandatory |
|-------------------------|---------------------|-----------------------------------------------------------------------------------------------------|-----------------|-----------|
| Id                      | String              | The unique identifier for the message.                                                              | None            | No        |
| Channel                 | String              | The channel of the message.                                                                         | None            | Yes       |
| Metadata                | String              | The metadata associated with the message.                                                           | None            | No        |
| Body                    | byte[]              | The body of the message.                                                                            | new byte[0]     | No        |
| Tags                    | Map<String, String> | The tags associated with the message.                                                               | new HashMap<>() | No        |
| Policy      | QueueMessagePolicy                 | The policy associated with the message.                                                               | None | No        |

#### Policy Options

| Name              | Type                | Description                                                                                         | Default Value   | Mandatory |
|-------------------|---------------------|-----------------------------------------------------------------------------------------------------|-----------------|-----------|
| DelaySeconds      | int              | The delay in seconds before the message becomes available in the queue.                            | None            | No        |
| ExpirationSeconds | int              | The expiration time in seconds for the message.                                                     | None            | No        |
| MaxReceiveCount   | int              | The number of receive attempts allowed for the message before it is moved to the dead letter queue. | None            | No        |
| MaxReceiveQueue   | String           | The dead letter queue where the message will be moved after reaching the maximum receive attempts. | None            | No        |

### Send Queue Message

Send a message to a Queue channel.

#### Request: `QueueMessage` Class Attributes

| Name                         | Type                | Description                                                                                 | Default Value | Mandatory |
|------------------------------|---------------------|---------------------------------------------------------------------------------------------|---------------|-----------|
| id                           | string              | The unique identifier for the message.                                                      | None          | No        |
| channel                      | string              | The channel of the message.                                                                 | None          | Yes       |
| metadata                     | string              | The metadata associated with the message.                                                   | None          | No        |
| body                         | byte[]              | The body of the message.                                                                    | new byte[0]   | No        |
| tags                         | Map<string, string> | The tags associated with the message.                                                       | new HashMap<>()| No        |
| delayInSeconds               | int                 | The delay in seconds before the message becomes available in the queue.                     | None          | No        |
| expirationInSeconds          | int                 | The expiration time in seconds for the message.                                             | None          | No        |
| attemptsBeforeDeadLetterQueue| int                 | The number of receive attempts allowed for the message before it is moved to the dead letter queue. | None | No |
| deadLetterQueue              | string              | The dead letter queue where the message will be moved after reaching the maximum receive attempts. | None | No |

#### Receive Request: `PollRequest`

| Name              | Type    | Description                                        | Default Value | Mandatory |
|-------------------|---------|----------------------------------------------------|---------------|-----------|
| Queue           | String  | The channel to poll messages from.                 | None          | Yes       |
| MaxItems          | int     | The maximum number of messages to poll.            | 1             | No        |
| WaitTimeout       | int     | The wait timeout in seconds for polling messages.  | 60            | No        |
| AutoAck           | boolean | Indicates if messages should be auto-acknowledged. | false         | No        |
| VisibilitySeconds | int     | Add a visibility timeout feature for messages.     | 0             | No        |

#### Response: `PollResponse`

| Name     | Type                       | Description                                             |
|----------|----------------------------|---------------------------------------------------------|
| Messages | List<QueueMessage> | The list of received queue messages.                    |


#### Example #1

```csharp
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
        
```

#### Example #2

```csharp
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
        
```

#### Example #3

```csharp
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

```

#### Example #4

```csharp
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

```

#### Example #5

```csharp
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

```