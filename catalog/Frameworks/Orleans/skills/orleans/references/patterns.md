# Orleans Patterns

Detailed patterns for building robust Orleans applications.

---

## Grain Patterns

### Stateless Worker Grain

Use for stateless operations that can be parallelized across silos.

```csharp
[StatelessWorker(maxLocalWorkers: 4)]
public class ImageProcessorGrain : Grain, IImageProcessorGrain
{
    public Task<byte[]> ResizeImage(byte[] imageData, int width, int height)
    {
        // CPU-bound work distributed across workers
        return Task.FromResult(ImageLib.Resize(imageData, width, height));
    }
}
```

**When to use:**
- CPU-bound stateless operations
- Request distribution across cluster
- No per-identity state needed

### Singleton Grain

Ensure only one instance exists in the cluster.

```csharp
public interface ILeaderboardGrain : IGrainWithIntegerKey
{
    Task<List<LeaderboardEntry>> GetTop(int count);
    Task Submit(string playerId, int score);
}

// Usage: Always use key 0 by convention
var leaderboard = grainFactory.GetGrain<ILeaderboardGrain>(0);
```

**When to use:**
- Global coordination
- Cluster-wide configuration
- Rate limiting across cluster

### Observer Pattern

Push notifications from grains to clients.

```csharp
// Observer interface
public interface IGameObserver : IGrainObserver
{
    void OnGameStateChanged(GameState state);
    void OnPlayerJoined(string playerId);
}

// Observable grain
public class GameGrain : Grain, IGameGrain
{
    private readonly ObserverManager<IGameObserver> _observers;

    public GameGrain()
    {
        _observers = new ObserverManager<IGameObserver>(
            TimeSpan.FromMinutes(5), // Expiration
            this.GetLogger<GameGrain>());
    }

    public Task Subscribe(IGameObserver observer)
    {
        _observers.Subscribe(observer, observer);
        return Task.CompletedTask;
    }

    public Task Unsubscribe(IGameObserver observer)
    {
        _observers.Unsubscribe(observer);
        return Task.CompletedTask;
    }

    private void NotifyStateChange(GameState state)
    {
        _observers.Notify(o => o.OnGameStateChanged(state));
    }
}
```

### Grain Call Filter

Intercept grain calls for cross-cutting concerns.

```csharp
public class LoggingGrainCallFilter : IIncomingGrainCallFilter
{
    private readonly ILogger<LoggingGrainCallFilter> _logger;

    public LoggingGrainCallFilter(ILogger<LoggingGrainCallFilter> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var grainType = context.Grain.GetType().Name;
        var methodName = context.ImplementationMethod.Name;

        _logger.LogInformation("Entering {Grain}.{Method}", grainType, methodName);

        var sw = Stopwatch.StartNew();
        try
        {
            await context.Invoke();
            _logger.LogInformation("{Grain}.{Method} completed in {Elapsed}ms",
                grainType, methodName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Grain}.{Method} failed after {Elapsed}ms",
                grainType, methodName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

// Registration
silo.AddIncomingGrainCallFilter<LoggingGrainCallFilter>();
```

---

## Persistence Patterns

### Multiple Named States

Store different aspects of grain state separately.

```csharp
public class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerProfile> _profile;
    private readonly IPersistentState<PlayerInventory> _inventory;
    private readonly IPersistentState<PlayerProgress> _progress;

    public PlayerGrain(
        [PersistentState("profile", "profiles")]
        IPersistentState<PlayerProfile> profile,
        [PersistentState("inventory", "items")]
        IPersistentState<PlayerInventory> inventory,
        [PersistentState("progress", "progress")]
        IPersistentState<PlayerProgress> progress)
    {
        _profile = profile;
        _inventory = inventory;
        _progress = progress;
    }

    public async Task UpdateProfile(string displayName)
    {
        _profile.State.DisplayName = displayName;
        await _profile.WriteStateAsync();  // Only profile is persisted
    }
}
```

### Conditional Persistence

Write state only when needed.

```csharp
public class CounterGrain : Grain, ICounterGrain
{
    private readonly IPersistentState<CounterState> _state;
    private int _transientCount;
    private const int PersistThreshold = 100;

    public async Task Increment()
    {
        _state.State.Count++;
        _transientCount++;

        // Batch persistence for performance
        if (_transientCount >= PersistThreshold)
        {
            await _state.WriteStateAsync();
            _transientCount = 0;
        }
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason, CancellationToken ct)
    {
        // Always persist on deactivation
        if (_transientCount > 0)
        {
            await _state.WriteStateAsync();
        }
        await base.OnDeactivateAsync(reason, ct);
    }
}
```

### Event Sourcing Pattern

Store events instead of current state.

```csharp
[GenerateSerializer]
public abstract record AccountEvent(DateTime Timestamp);

[GenerateSerializer]
public record DepositEvent(DateTime Timestamp, decimal Amount)
    : AccountEvent(Timestamp);

[GenerateSerializer]
public record WithdrawEvent(DateTime Timestamp, decimal Amount)
    : AccountEvent(Timestamp);

[GenerateSerializer]
public class AccountEventLog
{
    [Id(0)] public List<AccountEvent> Events { get; set; } = [];
}

public class AccountGrain : Grain, IAccountGrain
{
    private readonly IPersistentState<AccountEventLog> _log;
    private decimal _balance; // Computed from events

    public AccountGrain(
        [PersistentState("events", "eventStore")]
        IPersistentState<AccountEventLog> log)
    {
        _log = log;
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        // Rebuild state from events
        _balance = _log.State.Events.Aggregate(0m, (bal, evt) => evt switch
        {
            DepositEvent d => bal + d.Amount,
            WithdrawEvent w => bal - w.Amount,
            _ => bal
        });
        return base.OnActivateAsync(ct);
    }

    public async Task Deposit(decimal amount)
    {
        var evt = new DepositEvent(DateTime.UtcNow, amount);
        _log.State.Events.Add(evt);
        _balance += amount;
        await _log.WriteStateAsync();
    }
}
```

### Snapshotting

Combine event sourcing with periodic snapshots.

```csharp
[GenerateSerializer]
public class AccountSnapshot
{
    [Id(0)] public decimal Balance { get; set; }
    [Id(1)] public int LastEventIndex { get; set; }
    [Id(2)] public DateTime SnapshotTime { get; set; }
}

public class AccountGrain : Grain, IAccountGrain
{
    private readonly IPersistentState<AccountEventLog> _log;
    private readonly IPersistentState<AccountSnapshot> _snapshot;
    private decimal _balance;
    private const int SnapshotInterval = 100;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // Start from snapshot
        _balance = _snapshot.State.Balance;

        // Apply events since snapshot
        var newEvents = _log.State.Events
            .Skip(_snapshot.State.LastEventIndex);

        foreach (var evt in newEvents)
        {
            ApplyEvent(evt);
        }

        await base.OnActivateAsync(ct);
    }

    private async Task AppendEvent(AccountEvent evt)
    {
        _log.State.Events.Add(evt);
        ApplyEvent(evt);
        await _log.WriteStateAsync();

        // Create snapshot periodically
        if (_log.State.Events.Count % SnapshotInterval == 0)
        {
            _snapshot.State = new AccountSnapshot
            {
                Balance = _balance,
                LastEventIndex = _log.State.Events.Count,
                SnapshotTime = DateTime.UtcNow
            };
            await _snapshot.WriteStateAsync();
        }
    }
}
```

---

## Streaming Patterns

### Basic Stream Producer

```csharp
public class SensorGrain : Grain, ISensorGrain
{
    private IAsyncStream<SensorReading>? _stream;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("StreamProvider");
        _stream = streamProvider.GetStream<SensorReading>(
            StreamId.Create("Sensors", this.GetPrimaryKeyString()));
        return base.OnActivateAsync(ct);
    }

    public async Task ReportReading(double value)
    {
        var reading = new SensorReading
        {
            SensorId = this.GetPrimaryKeyString(),
            Value = value,
            Timestamp = DateTime.UtcNow
        };
        await _stream!.OnNextAsync(reading);
    }
}
```

### Implicit Stream Subscription

Automatic subscription based on grain identity.

```csharp
[ImplicitStreamSubscription("Sensors")]
public class SensorAggregatorGrain : Grain, ISensorAggregatorGrain
{
    private double _lastValue;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("StreamProvider");
        var stream = streamProvider.GetStream<SensorReading>(
            StreamId.Create("Sensors", this.GetPrimaryKeyString()));

        await stream.SubscribeAsync(OnReading);
        await base.OnActivateAsync(ct);
    }

    private Task OnReading(SensorReading reading, StreamSequenceToken? token)
    {
        _lastValue = reading.Value;
        // Process reading
        return Task.CompletedTask;
    }
}
```

### Stream Fan-Out

Broadcast to multiple consumers.

```csharp
public class NotificationGrain : Grain, INotificationGrain
{
    public async Task BroadcastNotification(Notification notification)
    {
        var streamProvider = this.GetStreamProvider("Notifications");

        // Broadcast to topic stream
        var globalStream = streamProvider.GetStream<Notification>(
            StreamId.Create("Notifications", "global"));
        await globalStream.OnNextAsync(notification);

        // Also send to user-specific streams
        foreach (var userId in notification.TargetUsers)
        {
            var userStream = streamProvider.GetStream<Notification>(
                StreamId.Create("Notifications", userId));
            await userStream.OnNextAsync(notification);
        }
    }
}
```

### Reliable Stream Consumption

Handle failures and resume from last position.

```csharp
public class OrderProcessorGrain : Grain, IOrderProcessorGrain
{
    private StreamSubscriptionHandle<Order>? _subscription;
    private readonly IPersistentState<StreamPosition> _position;

    public OrderProcessorGrain(
        [PersistentState("streamPos", "positions")]
        IPersistentState<StreamPosition> position)
    {
        _position = position;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("Orders");
        var stream = streamProvider.GetStream<Order>(
            StreamId.Create("Orders", "incoming"));

        // Resume from last known position
        _subscription = await stream.SubscribeAsync(
            OnOrderReceived,
            OnError,
            token: _position.State.LastToken);

        await base.OnActivateAsync(ct);
    }

    private async Task OnOrderReceived(Order order, StreamSequenceToken? token)
    {
        await ProcessOrder(order);

        // Persist position after successful processing
        _position.State.LastToken = token;
        await _position.WriteStateAsync();
    }
}
```

---

## Coordination Patterns

### Distributed Lock

Coordinate exclusive access across grains.

```csharp
public interface ILockGrain : IGrainWithStringKey
{
    Task<bool> TryAcquire(string owner, TimeSpan timeout);
    Task Release(string owner);
}

public class LockGrain : Grain, ILockGrain
{
    private string? _currentOwner;
    private DateTime _expiresAt;

    public Task<bool> TryAcquire(string owner, TimeSpan timeout)
    {
        var now = DateTime.UtcNow;

        // Check if lock is available or expired
        if (_currentOwner == null || now >= _expiresAt)
        {
            _currentOwner = owner;
            _expiresAt = now + timeout;
            return Task.FromResult(true);
        }

        // Already owned
        if (_currentOwner == owner)
        {
            _expiresAt = now + timeout;  // Extend
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task Release(string owner)
    {
        if (_currentOwner == owner)
        {
            _currentOwner = null;
        }
        return Task.CompletedTask;
    }
}
```

### Saga Pattern

Coordinate multi-grain transactions with compensation.

```csharp
public interface IOrderSagaGrain : IGrainWithGuidKey
{
    Task<SagaResult> Execute(OrderRequest request);
}

public class OrderSagaGrain : Grain, IOrderSagaGrain
{
    private readonly IPersistentState<SagaState> _state;

    public async Task<SagaResult> Execute(OrderRequest request)
    {
        _state.State.Status = SagaStatus.Started;
        _state.State.Request = request;
        await _state.WriteStateAsync();

        try
        {
            // Step 1: Reserve inventory
            var inventory = GrainFactory.GetGrain<IInventoryGrain>(request.ProductId);
            await inventory.Reserve(request.Quantity);
            _state.State.InventoryReserved = true;
            await _state.WriteStateAsync();

            // Step 2: Charge payment
            var payment = GrainFactory.GetGrain<IPaymentGrain>(request.CustomerId);
            await payment.Charge(request.Amount);
            _state.State.PaymentCharged = true;
            await _state.WriteStateAsync();

            // Step 3: Create order
            var order = GrainFactory.GetGrain<IOrderGrain>(this.GetPrimaryKey());
            await order.Create(request);

            _state.State.Status = SagaStatus.Completed;
            await _state.WriteStateAsync();

            return SagaResult.Success();
        }
        catch (Exception ex)
        {
            await Compensate();
            return SagaResult.Failed(ex.Message);
        }
    }

    private async Task Compensate()
    {
        _state.State.Status = SagaStatus.Compensating;
        await _state.WriteStateAsync();

        if (_state.State.PaymentCharged)
        {
            var payment = GrainFactory.GetGrain<IPaymentGrain>(
                _state.State.Request!.CustomerId);
            await payment.Refund(_state.State.Request.Amount);
        }

        if (_state.State.InventoryReserved)
        {
            var inventory = GrainFactory.GetGrain<IInventoryGrain>(
                _state.State.Request!.ProductId);
            await inventory.CancelReservation(_state.State.Request.Quantity);
        }

        _state.State.Status = SagaStatus.Compensated;
        await _state.WriteStateAsync();
    }
}
```

### Scatter-Gather

Parallel queries with result aggregation.

```csharp
public class SearchGrain : Grain, ISearchGrain
{
    public async Task<SearchResults> Search(SearchQuery query)
    {
        // Scatter: Query all index partitions in parallel
        var partitionCount = 10;
        var tasks = Enumerable.Range(0, partitionCount)
            .Select(i => GrainFactory
                .GetGrain<IIndexPartitionGrain>(i)
                .Search(query))
            .ToList();

        // Gather: Collect and merge results
        var partialResults = await Task.WhenAll(tasks);

        return new SearchResults
        {
            Items = partialResults
                .SelectMany(r => r.Items)
                .OrderByDescending(i => i.Score)
                .Take(query.Limit)
                .ToList(),
            TotalCount = partialResults.Sum(r => r.TotalCount)
        };
    }
}
```

---

## Performance Patterns

### Grain Pooling with Reentrant Calls

Allow interleaved calls for high throughput.

```csharp
[Reentrant]
public class CacheGrain : Grain, ICacheGrain
{
    private readonly Dictionary<string, CacheEntry> _cache = new();

    public Task<string?> Get(string key)
    {
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            return Task.FromResult<string?>(entry.Value);
        }
        return Task.FromResult<string?>(null);
    }

    public Task Set(string key, string value, TimeSpan ttl)
    {
        _cache[key] = new CacheEntry(value, DateTime.UtcNow + ttl);
        return Task.CompletedTask;
    }
}
```

### Read-Through Cache Pattern

```csharp
public class CachedDataGrain : Grain, ICachedDataGrain
{
    private readonly IPersistentState<CachedDataState> _state;
    private readonly IExternalDataService _dataService;

    public async Task<Data> GetData(string key)
    {
        // Check cache
        if (_state.State.Cache.TryGetValue(key, out var cached))
        {
            if (!cached.IsExpired)
            {
                return cached.Data;
            }
        }

        // Cache miss: fetch from external source
        var data = await _dataService.FetchAsync(key);

        // Update cache
        _state.State.Cache[key] = new CacheEntry<Data>(data, TimeSpan.FromMinutes(5));
        await _state.WriteStateAsync();

        return data;
    }
}
```

### Batch Processing

Collect requests and process in batches.

```csharp
public class BatchProcessorGrain : Grain, IBatchProcessorGrain
{
    private readonly List<WorkItem> _pending = [];
    private IDisposable? _timer;
    private const int BatchSize = 100;
    private const int FlushIntervalMs = 1000;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        _timer = RegisterGrainTimer(
            FlushBatch,
            default,
            TimeSpan.FromMilliseconds(FlushIntervalMs),
            TimeSpan.FromMilliseconds(FlushIntervalMs));

        return base.OnActivateAsync(ct);
    }

    public async Task Submit(WorkItem item)
    {
        _pending.Add(item);

        if (_pending.Count >= BatchSize)
        {
            await FlushBatch(default);
        }
    }

    private async Task FlushBatch(object _)
    {
        if (_pending.Count == 0) return;

        var batch = _pending.ToList();
        _pending.Clear();

        // Process batch efficiently
        await ProcessBatchAsync(batch);
    }
}
```

---

## Testing Patterns

### Unit Testing with TestCluster

```csharp
public class PlayerGrainTests : IClassFixture<TestClusterFixture>
{
    private readonly TestCluster _cluster;

    public PlayerGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task UpdateScore_IncrementsScore()
    {
        // Arrange
        var player = _cluster.GrainFactory.GetGrain<IPlayerGrain>("test-player");

        // Act
        await player.UpdateScore(100);
        var state = await player.GetState();

        // Assert
        Assert.Equal(100, state.Score);
    }
}

public class TestClusterFixture : IDisposable
{
    public TestCluster Cluster { get; }

    public TestClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose() => Cluster.StopAllSilos();
}

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("Default");
    }
}
```

### Mocking Grain Dependencies

```csharp
public class OrderGrainTests
{
    [Fact]
    public async Task CreateOrder_CallsInventoryGrain()
    {
        // Arrange
        var mockInventory = new Mock<IInventoryGrain>();
        var mockFactory = new Mock<IGrainFactory>();
        mockFactory
            .Setup(f => f.GetGrain<IInventoryGrain>(It.IsAny<string>(), null))
            .Returns(mockInventory.Object);

        var grain = new OrderGrain(mockFactory.Object);

        // Act
        await grain.CreateOrder(new OrderRequest { ProductId = "prod-1" });

        // Assert
        mockInventory.Verify(i => i.Reserve(It.IsAny<int>()), Times.Once);
    }
}
```
