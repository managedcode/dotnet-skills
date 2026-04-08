# Orleans Anti-Patterns

Common mistakes when building Orleans applications and how to avoid them.

---

## Blocking Calls

### Anti-Pattern: Synchronous Blocking

```csharp
// BAD: Blocking call in async context
public class BadGrain : Grain, IBadGrain
{
    public Task<string> GetData()
    {
        var client = new HttpClient();
        var result = client.GetStringAsync("https://api.example.com/data").Result; // BLOCKS!
        return Task.FromResult(result);
    }
}
```

**Why it's bad:**
- Blocks the grain's single-threaded scheduler
- Can cause deadlocks with Orleans runtime
- Prevents other grain calls from executing
- Severely degrades cluster throughput

### Correct Approach

```csharp
// GOOD: Fully async
public class GoodGrain : Grain, IGoodGrain
{
    private readonly HttpClient _client;

    public GoodGrain(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> GetData()
    {
        return await _client.GetStringAsync("https://api.example.com/data");
    }
}
```

---

## Large Grain State

### Anti-Pattern: Unbounded State Growth

```csharp
// BAD: State grows without limit
[GenerateSerializer]
public class ChatRoomState
{
    [Id(0)] public List<ChatMessage> AllMessages { get; set; } = [];  // Grows forever!
}

public class ChatRoomGrain : Grain, IChatRoomGrain
{
    private readonly IPersistentState<ChatRoomState> _state;

    public async Task SendMessage(ChatMessage message)
    {
        _state.State.AllMessages.Add(message);  // Unbounded growth
        await _state.WriteStateAsync();  // Gets slower over time
    }
}
```

**Why it's bad:**
- Serialization time increases linearly
- Memory usage grows unbounded
- Activation time becomes very slow
- Storage costs increase

### Correct Approach

```csharp
// GOOD: Bounded state with external storage for history
[GenerateSerializer]
public class ChatRoomState
{
    [Id(0)] public List<ChatMessage> RecentMessages { get; set; } = [];
    [Id(1)] public int TotalMessageCount { get; set; }
    private const int MaxRecentMessages = 100;

    public void AddMessage(ChatMessage message)
    {
        RecentMessages.Add(message);
        TotalMessageCount++;

        if (RecentMessages.Count > MaxRecentMessages)
        {
            RecentMessages.RemoveAt(0);
        }
    }
}

public class ChatRoomGrain : Grain, IChatRoomGrain
{
    private readonly IPersistentState<ChatRoomState> _state;
    private readonly IMessageArchive _archive;  // External storage for old messages

    public async Task SendMessage(ChatMessage message)
    {
        _state.State.AddMessage(message);
        await _state.WriteStateAsync();

        // Archive to external storage asynchronously
        await _archive.StoreAsync(this.GetPrimaryKeyString(), message);
    }

    public async Task<List<ChatMessage>> GetHistory(int page, int pageSize)
    {
        return await _archive.GetPageAsync(this.GetPrimaryKeyString(), page, pageSize);
    }
}
```

---

## Chatty Grain Communication

### Anti-Pattern: Many Small Calls

```csharp
// BAD: Multiple round-trips per operation
public class OrderGrain : Grain, IOrderGrain
{
    public async Task<OrderSummary> GetOrderSummary()
    {
        var customer = GrainFactory.GetGrain<ICustomerGrain>(_customerId);
        var product = GrainFactory.GetGrain<IProductGrain>(_productId);
        var shipping = GrainFactory.GetGrain<IShippingGrain>(_shippingId);

        // Sequential calls - very slow!
        var customerName = await customer.GetName();
        var customerEmail = await customer.GetEmail();
        var customerAddress = await customer.GetAddress();
        var productName = await product.GetName();
        var productPrice = await product.GetPrice();
        var shippingStatus = await shipping.GetStatus();
        var shippingEta = await shipping.GetEta();

        return new OrderSummary { /* ... */ };
    }
}
```

**Why it's bad:**
- Each call incurs network latency
- Sequential execution multiplies delay
- High overhead for small payloads
- Poor cluster resource utilization

### Correct Approach

```csharp
// GOOD: Batch operations and parallel calls
public class OrderGrain : Grain, IOrderGrain
{
    public async Task<OrderSummary> GetOrderSummary()
    {
        var customer = GrainFactory.GetGrain<ICustomerGrain>(_customerId);
        var product = GrainFactory.GetGrain<IProductGrain>(_productId);
        var shipping = GrainFactory.GetGrain<IShippingGrain>(_shippingId);

        // Parallel calls with batched data retrieval
        var customerTask = customer.GetDetails();       // Returns all customer info
        var productTask = product.GetDetails();         // Returns all product info
        var shippingTask = shipping.GetStatus();        // Returns full status

        await Task.WhenAll(customerTask, productTask, shippingTask);

        return new OrderSummary
        {
            Customer = customerTask.Result,
            Product = productTask.Result,
            Shipping = shippingTask.Result
        };
    }
}
```

---

## Single Bottleneck Grain

### Anti-Pattern: Hot Grain

```csharp
// BAD: All operations go through one grain
public interface IGlobalCounterGrain : IGrainWithIntegerKey
{
    Task<long> IncrementAndGet();
}

// Usage everywhere:
var counter = grainFactory.GetGrain<IGlobalCounterGrain>(0);
await counter.IncrementAndGet();  // ALL requests hit this single grain
```

**Why it's bad:**
- Single grain handles all load
- No horizontal scaling possible
- Becomes the bottleneck for entire system
- Single point of failure

### Correct Approach

```csharp
// GOOD: Partitioned counters with aggregation
public interface IPartitionedCounterGrain : IGrainWithIntegerKey
{
    Task Increment();
    Task<long> GetLocalCount();
}

public interface ICounterAggregatorGrain : IGrainWithIntegerKey
{
    Task<long> GetTotalCount();
}

public class PartitionedCounterGrain : Grain, IPartitionedCounterGrain
{
    private long _count;

    public Task Increment()
    {
        _count++;
        return Task.CompletedTask;
    }

    public Task<long> GetLocalCount() => Task.FromResult(_count);
}

public class CounterAggregatorGrain : Grain, ICounterAggregatorGrain
{
    private const int PartitionCount = 100;

    public async Task<long> GetTotalCount()
    {
        var tasks = Enumerable.Range(0, PartitionCount)
            .Select(i => GrainFactory
                .GetGrain<IPartitionedCounterGrain>(i)
                .GetLocalCount());

        var counts = await Task.WhenAll(tasks);
        return counts.Sum();
    }
}

// Usage: Distribute load across partitions
var partitionId = HashCode(userId) % PartitionCount;
var counter = grainFactory.GetGrain<IPartitionedCounterGrain>(partitionId);
await counter.Increment();
```

---

## Improper Grain Activation

### Anti-Pattern: Short-Lived Grains

```csharp
// BAD: Creating unique grains for each request
public class ApiController
{
    public async Task<IActionResult> ProcessRequest(RequestData data)
    {
        // New unique grain for each request!
        var processor = _grainFactory.GetGrain<IProcessorGrain>(Guid.NewGuid());
        var result = await processor.Process(data);
        return Ok(result);
    }
}
```

**Why it's bad:**
- Activation overhead on every request
- Grains never benefit from cached state
- Memory churn in silo
- Completely defeats Orleans' actor model benefits

### Correct Approach

```csharp
// GOOD: Reuse grains based on business identity
public class ApiController
{
    public async Task<IActionResult> ProcessRequest(RequestData data)
    {
        // Grain identity based on logical entity
        var processor = _grainFactory.GetGrain<IProcessorGrain>(data.CustomerId);
        var result = await processor.Process(data);
        return Ok(result);
    }
}

// Or use StatelessWorker for truly stateless operations
[StatelessWorker]
public class ProcessorGrain : Grain, IProcessorGrain
{
    public Task<Result> Process(RequestData data)
    {
        // Stateless processing, Orleans manages pooling
        return Task.FromResult(DoProcess(data));
    }
}
```

---

## Ignoring Reentrancy

### Anti-Pattern: Deadlock-Prone Calls

```csharp
// BAD: Can deadlock if A calls B and B calls A
public class GrainA : Grain, IGrainA
{
    public async Task DoSomething()
    {
        var grainB = GrainFactory.GetGrain<IGrainB>(0);
        await grainB.DoOther();  // GrainB might call back to GrainA!
    }

    public Task Callback()
    {
        // This will deadlock if called while DoSomething is waiting
        return Task.CompletedTask;
    }
}
```

**Why it's bad:**
- Circular calls cause deadlock
- Grain waits for itself
- Hard to debug
- System appears hung

### Correct Approach

```csharp
// GOOD: Allow reentrancy for callbacks
[Reentrant]  // Allows interleaved calls
public class GrainA : Grain, IGrainA
{
    public async Task DoSomething()
    {
        var grainB = GrainFactory.GetGrain<IGrainB>(0);
        await grainB.DoOther();
    }

    public Task Callback()
    {
        return Task.CompletedTask;
    }
}

// Or use [AlwaysInterleave] for specific methods
public class GrainA : Grain, IGrainA
{
    public async Task DoSomething()
    {
        var grainB = GrainFactory.GetGrain<IGrainB>(0);
        await grainB.DoOther();
    }

    [AlwaysInterleave]  // This method can always execute
    public Task Callback()
    {
        return Task.CompletedTask;
    }
}
```

---

## Misusing Timers and Reminders

### Anti-Pattern: Timer for Persistence

```csharp
// BAD: Using timer for critical persistence
public class BadGrain : Grain, IBadGrain
{
    private int _importantData;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        // Timer is NOT persistent - data loss on silo crash!
        RegisterGrainTimer(
            SaveData,
            default,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));

        return base.OnActivateAsync(ct);
    }

    public Task UpdateData(int value)
    {
        _importantData = value;
        return Task.CompletedTask;  // Not persisted until timer fires!
    }
}
```

**Why it's bad:**
- Timers don't survive grain deactivation
- Data lost if silo crashes
- No guarantee timer will fire
- Not suitable for critical operations

### Correct Approach

```csharp
// GOOD: Persist immediately for critical data, use reminders for scheduled work
public class GoodGrain : Grain, IGoodGrain, IRemindable
{
    private readonly IPersistentState<GrainState> _state;

    public async Task UpdateData(int value)
    {
        _state.State.ImportantData = value;
        await _state.WriteStateAsync();  // Immediate persistence
    }

    // Use reminder for scheduled work that must survive failures
    public async Task ScheduleDailyReport()
    {
        await this.RegisterOrUpdateReminder(
            "daily-report",
            TimeSpan.FromHours(24),
            TimeSpan.FromHours(24));
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == "daily-report")
        {
            return GenerateReport();
        }
        return Task.CompletedTask;
    }
}
```

---

## Incorrect State Serialization

### Anti-Pattern: Non-Serializable State

```csharp
// BAD: Missing serialization attributes
public class PlayerState
{
    public int Score { get; set; }
    public HttpClient Client { get; set; }  // Can't serialize!
    public Action OnScoreChanged { get; set; }  // Can't serialize!
}
```

**Why it's bad:**
- Serialization fails at runtime
- State cannot be persisted
- Grain crashes on activation

### Correct Approach

```csharp
// GOOD: Proper serialization with Orleans attributes
[GenerateSerializer]
public class PlayerState
{
    [Id(0)] public int Score { get; set; }
    [Id(1)] public DateTime LastPlayed { get; set; }
    [Id(2)] public List<string> Achievements { get; set; } = [];

    // Non-serializable fields marked appropriately
    [NonSerialized]
    private HttpClient? _client;

    [NonSerialized]
    private Action? _onScoreChanged;
}

// Inject dependencies instead of storing them
public class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerState> _state;
    private readonly HttpClient _client;  // Injected, not in state

    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        HttpClient client)
    {
        _state = state;
        _client = client;
    }
}
```

---

## Exception Handling

### Anti-Pattern: Swallowing Exceptions

```csharp
// BAD: Silent failures
public class BadGrain : Grain, IBadGrain
{
    public async Task ProcessOrder(Order order)
    {
        try
        {
            await _paymentService.Charge(order.Amount);
            await _inventoryService.Reserve(order.Items);
        }
        catch (Exception)
        {
            // Silently swallow - order appears successful but isn't!
        }
    }
}
```

**Why it's bad:**
- Failures are hidden
- System enters inconsistent state
- Very hard to debug
- Breaks caller's error handling

### Correct Approach

```csharp
// GOOD: Proper exception handling and propagation
public class GoodGrain : Grain, IGoodGrain
{
    private readonly ILogger<GoodGrain> _logger;

    public async Task ProcessOrder(Order order)
    {
        try
        {
            await _paymentService.Charge(order.Amount);
        }
        catch (PaymentException ex)
        {
            _logger.LogError(ex, "Payment failed for order {OrderId}", order.Id);
            throw new OrderProcessingException("Payment failed", ex);
        }

        try
        {
            await _inventoryService.Reserve(order.Items);
        }
        catch (InventoryException ex)
        {
            _logger.LogError(ex, "Inventory reservation failed for order {OrderId}", order.Id);

            // Compensate for partial success
            await _paymentService.Refund(order.Amount);

            throw new OrderProcessingException("Inventory unavailable", ex);
        }
    }
}
```

---

## Cluster Configuration

### Anti-Pattern: Dev Config in Production

```csharp
// BAD: Localhost clustering in production
builder.UseOrleans(silo =>
{
    silo.UseLocalhostClustering();  // Single-node only!
    silo.AddMemoryGrainStorage("Default");  // No persistence!
});
```

**Why it's bad:**
- Cannot scale beyond one silo
- Data lost on restart
- No fault tolerance
- Not suitable for production

### Correct Approach

```csharp
// GOOD: Environment-appropriate configuration
builder.UseOrleans((context, silo) =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        silo.UseLocalhostClustering();
        silo.AddMemoryGrainStorage("Default");
    }
    else
    {
        silo.UseAzureStorageClustering(options =>
            options.ConfigureTableServiceClient(
                context.Configuration.GetConnectionString("Orleans")));

        silo.AddAzureTableGrainStorage("Default", options =>
            options.ConfigureTableServiceClient(
                context.Configuration.GetConnectionString("Orleans")));

        silo.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = context.Configuration["Orleans:ClusterId"];
            options.ServiceId = context.Configuration["Orleans:ServiceId"];
        });
    }
});
```

---

## Summary: Quick Reference

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| `.Result` / `.Wait()` | Deadlocks, blocks scheduler | Use `async/await` throughout |
| Unbounded state | Slow activation, memory bloat | Bound state, use external storage |
| Many small calls | High latency | Batch operations, parallel calls |
| Hot single grain | Bottleneck, no scaling | Partition across grains |
| Unique grain per request | Activation overhead | Reuse grains, use StatelessWorker |
| Circular calls | Deadlocks | Use [Reentrant] or redesign |
| Timer for persistence | Data loss | Use immediate persist + reminders |
| Missing [GenerateSerializer] | Runtime failures | Add proper serialization |
| Swallowing exceptions | Hidden failures | Log and propagate errors |
| Dev config in prod | No persistence/scaling | Environment-specific config |
