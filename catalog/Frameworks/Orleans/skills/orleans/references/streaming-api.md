# Streaming, Broadcast Channels, and Observers API

Detailed streaming and communication patterns from official Orleans documentation.

## Orleans Streams

Streams are virtual, always exist, never fail, identified by `StreamId` (namespace + key). Decouple data generation from processing in time and space.

### Configuration

```csharp
// Silo — memory streams (dev only)
siloBuilder.AddMemoryStreams("StreamProvider")
    .AddMemoryGrainStorage("PubSubStore");

// Silo — Azure Event Hubs
siloBuilder.AddEventHubStreams("EventHubProvider", options =>
{
    options.ConfigureEventHub(eh => eh.Configure(o =>
    {
        o.ConnectionString = connectionString;
        o.ConsumerGroup = "$Default";
        o.Path = "my-hub";
    }));
    options.UseAzureTableCheckpointer(c => c.ConfigureTableServiceClient(storageConnection));
});

// Client
clientBuilder.AddMemoryStreams("StreamProvider");
```

### Producing Events

```csharp
var streamProvider = this.GetStreamProvider("StreamProvider");
var streamId = StreamId.Create("SensorData", this.GetPrimaryKeyString());
var stream = streamProvider.GetStream<SensorReading>(streamId);
await stream.OnNextAsync(new SensorReading { Value = 42.0 });
```

### Consuming Events — Implicit Subscription

```csharp
[ImplicitStreamSubscription("SensorData")]
public class AggregatorGrain : Grain, IAggregatorGrain
{
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("StreamProvider");
        var stream = streamProvider.GetStream<SensorReading>(
            StreamId.Create("SensorData", this.GetPrimaryKeyString()));

        await stream.SubscribeAsync(
            (reading, token) =>
            {
                // Process reading
                return Task.CompletedTask;
            });
        await base.OnActivateAsync(ct);
    }
}
```

### Consuming Events — Explicit Subscription

```csharp
var stream = streamProvider.GetStream<Order>(StreamId.Create("Orders", "incoming"));

StreamSubscriptionHandle<Order> handle = await stream.SubscribeAsync(
    onNextAsync: (item, token) => ProcessAsync(item),
    onErrorAsync: ex => HandleError(ex),
    onCompletedAsync: () => Task.CompletedTask,
    token: lastKnownToken); // resume from position

// Unsubscribe
await handle.UnsubscribeAsync();
```

### Delivery Semantics

| Provider | Delivery | Ordering |
|---|---|---|
| Memory | Best-effort | FIFO per stream |
| Azure Queue | At-least-once | FIFO per queue |
| Event Hubs | At-least-once | Per partition |
| Broadcast Channel | At-most-once | No guarantee |

Rewindable streams support subscribing from arbitrary point using `StreamSequenceToken`.

### IAsyncEnumerable (Orleans 7+)

Request-response scoped streaming, not pub-sub:

```csharp
// Interface
public interface IDataGrain : IGrainWithStringKey
{
    IAsyncEnumerable<DataItem> GetItems(
        int count,
        [EnumeratorCancellation] CancellationToken ct = default);
}

// Implementation
public class DataGrain : Grain, IDataGrain
{
    public async IAsyncEnumerable<DataItem> GetItems(
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
        {
            yield return await FetchItem(i);
        }
    }
}

// Consumer
await foreach (var item in grain.GetItems(100).WithCancellation(cts.Token))
{
    Process(item);
}
```

Default batch size: 100. Configure with `.WithBatchSize(50)`.

## Broadcast Channels

Fire-and-forget broadcast to all implicitly subscribed grains. No persistence, no delivery guarantees.

### Configuration

```csharp
siloBuilder.AddBroadcastChannel("announcements");
```

### Consumer

```csharp
[ImplicitChannelSubscription]
public class NotificationGrain : Grain, INotificationGrain, IOnBroadcastChannelSubscribed
{
    public Task OnSubscribed(IBroadcastChannelSubscription subscription)
    {
        subscription.Attach<Announcement>(
            item => OnAnnouncement(item),
            ex => OnError(ex));
        return Task.CompletedTask;
    }

    private Task OnAnnouncement(Announcement item)
    {
        // Process broadcast message
        return Task.CompletedTask;
    }
}
```

### Producer

```csharp
var provider = client.GetBroadcastChannelProvider("announcements");
var channelId = ChannelId.Create("system", Guid.Empty);
var writer = provider.GetChannelWriter<Announcement>(channelId);
await writer.Publish(new Announcement { Message = "System update" });
```

## Observers

One-way async push notifications from grains to clients or other grains.

### Observer Interface

```csharp
public interface IChatObserver : IGrainObserver
{
    [OneWay] // fire-and-forget
    Task OnMessage(string user, string message);

    Task OnUserJoined(string user);
}
```

### Server-Side (ObserverManager)

```csharp
public class ChatRoomGrain : Grain, IChatRoomGrain
{
    private readonly ObserverManager<IChatObserver> _observers;

    public ChatRoomGrain(ILogger<ChatRoomGrain> logger)
    {
        _observers = new ObserverManager<IChatObserver>(
            TimeSpan.FromMinutes(5), logger);
    }

    public Task Subscribe(IChatObserver observer)
    {
        _observers.Subscribe(observer, observer);
        return Task.CompletedTask;
    }

    public Task Unsubscribe(IChatObserver observer)
    {
        _observers.Unsubscribe(observer);
        return Task.CompletedTask;
    }

    public Task SendMessage(string user, string message)
    {
        _observers.Notify(o => o.OnMessage(user, message));
        return Task.CompletedTask;
    }
}
```

### Client-Side

```csharp
// Create observer
var observerInstance = new ChatObserver();
var observerRef = grainFactory.CreateObjectReference<IChatObserver>(observerInstance);

// Subscribe
var chatRoom = client.GetGrain<IChatRoomGrain>("lobby");
await chatRoom.Subscribe(observerRef);

// Cleanup — must call to avoid memory leaks (held as WeakReference)
grainFactory.DeleteObjectReference<IChatObserver>(observerRef);
```

### Grain-as-Observer

```csharp
public class MonitorGrain : Grain, IMonitorGrain, IChatObserver
{
    public async Task StartMonitoring(string room)
    {
        var chatRoom = GrainFactory.GetGrain<IChatRoomGrain>(room);
        await chatRoom.Subscribe(this.AsReference<IChatObserver>());
    }

    public Task OnMessage(string user, string message)
    {
        // Handle notification
        return Task.CompletedTask;
    }
}
```

### Observer Execution Model

- Non-reentrant, single-threaded per observer reference
- `[Reentrant]` / `[AlwaysInterleave]` attributes don't affect observers
- `CancellationToken` support added in Orleans 9.0

## Stream Providers

### Azure Event Hubs

NuGet: `Microsoft.Orleans.Streaming.EventHubs`. Real-time ingestion, rewindable, supports replay from arbitrary point.

```csharp
siloBuilder.AddEventHubStreams("EventHubProvider", options =>
{
    options.ConfigureEventHub(eh => eh.Configure(o =>
    {
        o.ConnectionString = connectionString;
        o.ConsumerGroup = "$Default";
        o.Path = "my-hub";
    }));
    options.UseAzureTableCheckpointer(c =>
        c.ConfigureTableServiceClient(storageConnection));
});
```

### Azure Queue (AQ)

NuGet: `Microsoft.Orleans.Streaming.AzureStorage`. Uses pulling agents inside silos. Not rewindable. Does not guarantee FIFO on failures.

```csharp
siloBuilder.AddAzureQueueStreams("AQProvider", optionsBuilder =>
    optionsBuilder.ConfigureAzureQueue(options =>
        options.Configure(opt =>
            opt.QueueServiceClient = new QueueServiceClient(endpoint, credential))));
```

### Aspire Streaming Integration

```csharp
// Azure Queue with Aspire
var storage = builder.AddAzureStorage("storage");
var queues = storage.AddQueues("streaming");
var orleans = builder.AddOrleans("cluster")
    .WithStreaming("AQProvider", queues);

// In-memory streaming
orleans.WithMemoryStreaming("MemoryProvider");

// Broadcast channels
orleans.WithBroadcastChannel("BroadcastChannel");
```

### Pub-Sub Management

Managed by `PubSubRendezvousGrain`, persisted via `"PubSubStore"`:

```csharp
siloBuilder.AddAzureTableGrainStorage("PubSubStore",
    options => options.TableServiceClient = new TableServiceClient(endpoint, credential));
```

## Streams Programming API Details

### Core Interfaces

```csharp
public interface IAsyncObserver<in T>
{
    Task OnNextAsync(T item, StreamSequenceToken token = null);
    Task OnCompletedAsync();
    Task OnErrorAsync(Exception ex);
}

public interface IAsyncObservable<T>
{
    Task<StreamSubscriptionHandle<T>> SubscribeAsync(IAsyncObserver<T> observer);
}
```

### Failure Recovery

Consumer must re-attach processing logic in `OnActivateAsync`:

```csharp
public override async Task OnActivateAsync(CancellationToken ct)
{
    var stream = this.GetStreamProvider("SP").GetStream<string>(streamId);
    var handles = await stream.GetAllSubscriptionHandles();
    foreach (var handle in handles)
        await handle.ResumeAsync(this);
}
```

### Implicit Subscriptions with IStreamSubscriptionObserver

```csharp
[ImplicitStreamSubscription("MyNamespace")]
public class MyGrain : Grain, IMyGrain, IStreamSubscriptionObserver
{
    public async Task OnSubscribed(IStreamSubscriptionHandleFactory handleFactory)
    {
        var handle = handleFactory.Create<string>();
        await handle.ResumeAsync(this);
    }
}
```

### Key Characteristics

- Subscriptions are per-grain (not per-activation) — durable across activations
- Multiple producers and consumers per stream
- Subscribing X times delivers event X times
- Rewindable streams (Event Hubs) support replay from `StreamSequenceToken`

### Why Orleans Streams?

Existing stream systems (Kafka, Storm, Spark) are for uniform dataflow graphs. Orleans Streams target:
1. **Flexible processing logic** — imperative, functional, Rx, stateful, with side effects
2. **Dynamic topologies** — add/remove nodes at runtime
3. **Fine-grained granularity** — each stream link is an independent entity
4. **Distribution** — scalable, elastic, reliable

Typical use: per-user streams with different processing logic per user, subscriptions changing dynamically.

## Streams vs Broadcast vs Observers vs IAsyncEnumerable

| Feature | Streams | Broadcast | Observers | IAsyncEnumerable |
|---|---|---|---|---|
| Persistence | Provider-dependent | No | No | No |
| Delivery | At-least-once (varies) | At-most-once | Best-effort | Request-scoped |
| Subscription | Implicit + explicit | Implicit only | Manual | N/A |
| Pattern | Pub/sub | Fan-out | Push notify | Pull streaming |
| Survives restart | Yes (with provider) | No | No | No |
| Use case | Event pipelines | Announcements | Client updates | Paginated data |
