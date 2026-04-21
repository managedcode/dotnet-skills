# Worker Services Anti-Patterns Reference

This reference documents common mistakes when building .NET worker services and provides corrective guidance.

---

## Lifecycle and Initialization Anti-Patterns

### 1. Blocking in StartAsync

**Problem:** Long-running operations in `StartAsync` block other hosted services from starting.

```csharp
// BAD: Blocks host startup
public class BadWorker : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // This blocks all other hosted services from starting
        await LoadLargeDatasetAsync();
        await WarmupCachesAsync();
        await base.StartAsync(cancellationToken);
    }
}
```

**Correction:** Move initialization to `ExecuteAsync`:

```csharp
// GOOD: Non-blocking startup
public class GoodWorker : BackgroundService
{
    private bool _initialized;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize in ExecuteAsync, not StartAsync
        await LoadLargeDatasetAsync(stoppingToken);
        await WarmupCachesAsync(stoppingToken);
        _initialized = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
        }
    }
}
```

### 2. Forgetting to Call Base Methods

**Problem:** Not calling base class methods breaks lifecycle management.

```csharp
// BAD: Missing base.StartAsync
public class BadWorker : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync();
        // Missing: await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CleanupAsync();
        // Missing: await base.StopAsync(cancellationToken);
    }
}
```

**Correction:** Always call base methods:

```csharp
// GOOD: Proper base method calls
public class GoodWorker : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync();
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CleanupAsync();
        await base.StopAsync(cancellationToken);
    }
}
```

### 3. Using async void

**Problem:** `async void` methods crash the process on unhandled exceptions.

```csharp
// BAD: async void crashes process
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessItemAsync(); // Fire and forget
        }
    }

    private async void ProcessItemAsync() // DANGEROUS
    {
        await DoWorkThatMightFailAsync(); // Unhandled exception = process crash
    }
}
```

**Correction:** Use `async Task` and handle exceptions:

```csharp
// GOOD: async Task with proper handling
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessItemAsync(stoppingToken);
        }
    }

    private async Task ProcessItemAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DoWorkThatMightFailAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item");
        }
    }
}
```

---

## Cancellation and Shutdown Anti-Patterns

### 4. Ignoring Cancellation Token

**Problem:** Not propagating cancellation tokens causes ungraceful shutdown.

```csharp
// BAD: Ignoring cancellation
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true) // Never checks stoppingToken
        {
            await ProcessAsync(); // No cancellation token passed
            await Task.Delay(5000); // No cancellation token
        }
    }
}
```

**Correction:** Propagate cancellation everywhere:

```csharp
// GOOD: Proper cancellation
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // Graceful exit
            }
        }
    }
}
```

### 5. Swallowing OperationCanceledException

**Problem:** Catching and ignoring `OperationCanceledException` during shutdown hides issues.

```csharp
// BAD: Swallowing cancellation
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex) // Catches OperationCanceledException too
            {
                _logger.LogError(ex, "Error"); // Logs cancellation as error
                // Continues loop even on shutdown
            }
        }
    }
}
```

**Correction:** Handle cancellation separately:

```csharp
// GOOD: Proper exception handling
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during work");
            }
        }
    }
}
```

### 6. No Shutdown Timeout Configuration

**Problem:** Default 30-second shutdown timeout may be too short for draining work.

```csharp
// BAD: Relying on default timeout
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<LongRunningWorker>();
// Default ShutdownTimeout is 30 seconds
```

**Correction:** Configure appropriate timeout:

```csharp
// GOOD: Configured shutdown timeout
var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHostedService<LongRunningWorker>();
```

---

## Dependency Injection Anti-Patterns

### 7. Injecting Scoped Services Directly

**Problem:** Injecting scoped services into singleton `BackgroundService` creates captive dependencies.

```csharp
// BAD: Captive dependency
public class BadWorker : BackgroundService
{
    private readonly AppDbContext _dbContext; // Singleton captures scoped service

    public BadWorker(AppDbContext dbContext) // Direct injection
    {
        _dbContext = dbContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // DbContext is never refreshed, connection may be stale
            var items = await _dbContext.Items.ToListAsync(stoppingToken);
        }
    }
}
```

**Correction:** Use `IServiceScopeFactory`:

```csharp
// GOOD: Proper scoped service handling
public class GoodWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GoodWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var items = await dbContext.Items.ToListAsync(stoppingToken);
        }
    }
}
```

### 8. Singleton DbContext

**Problem:** Using `DbContext` as singleton causes thread-safety issues and stale data.

```csharp
// BAD: Singleton DbContext registration
builder.Services.AddSingleton<AppDbContext>(); // Thread-unsafe, stale tracking
builder.Services.AddHostedService<Worker>();
```

**Correction:** Register as scoped and use scope factory:

```csharp
// GOOD: Scoped DbContext
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddHostedService<Worker>();

// In worker, create scopes for each unit of work
```

### 9. Service Locator in Constructor

**Problem:** Resolving services in constructor can fail if service isn't registered.

```csharp
// BAD: Service resolution in constructor
public class BadWorker : BackgroundService
{
    private readonly ISpecialService _service;

    public BadWorker(IServiceProvider provider)
    {
        _service = provider.GetRequiredService<ISpecialService>(); // May throw
    }
}
```

**Correction:** Use proper constructor injection or scope factory:

```csharp
// GOOD: Explicit dependency
public class GoodWorker : BackgroundService
{
    private readonly ISpecialService _service;

    public GoodWorker(ISpecialService service) // Fails at startup if not registered
    {
        _service = service;
    }
}
```

---

## Loop and Timing Anti-Patterns

### 10. Tight Polling Loop

**Problem:** Polling without delay wastes CPU and may overload resources.

```csharp
// BAD: CPU-burning tight loop
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var hasWork = await CheckForWorkAsync();
            if (hasWork)
            {
                await ProcessAsync();
            }
            // No delay - burns CPU when idle
        }
    }
}
```

**Correction:** Add appropriate delays or use event-driven patterns:

```csharp
// GOOD: Appropriate delay
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var hasWork = await CheckForWorkAsync(stoppingToken);
            if (hasWork)
            {
                await ProcessAsync(stoppingToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
```

### 11. Task.Delay Instead of PeriodicTimer

**Problem:** `Task.Delay` doesn't account for processing time, causing drift.

```csharp
// BAD: Interval drift
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(); // Takes variable time
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            // Actual interval is 5 minutes + processing time
        }
    }
}
```

**Correction:** Use `PeriodicTimer` for consistent intervals:

```csharp
// GOOD: Consistent intervals with PeriodicTimer
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        // Run immediately, then on schedule
        await ProcessAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessAsync(stoppingToken);
        }
    }
}
```

### 12. Ad-hoc while(true) Without BackgroundService

**Problem:** Raw `while(true)` loops lack proper lifecycle management.

```csharp
// BAD: Manual loop without BackgroundService
public class BadService : IHostedService
{
    private Task? _executingTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = Task.Run(async () =>
        {
            while (true) // No way to stop
            {
                await DoWorkAsync();
                await Task.Delay(5000);
            }
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Cannot stop the loop
        return Task.CompletedTask;
    }
}
```

**Correction:** Use `BackgroundService`:

```csharp
// GOOD: BackgroundService with proper lifecycle
public class GoodService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
            await Task.Delay(5000, stoppingToken);
        }
    }
}
```

---

## Exception Handling Anti-Patterns

### 13. Unhandled Exceptions in ExecuteAsync

**Problem:** Unhandled exceptions silently stop the worker without notification.

```csharp
// BAD: No exception handling
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken); // Exception stops worker silently
        }
    }
}
```

**Correction:** Wrap in try-catch with logging:

```csharp
// GOOD: Exception handling with logging
public class GoodWorker : BackgroundService
{
    private readonly ILogger<GoodWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker iteration");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Backoff
            }
        }
    }
}
```

### 14. Retrying Forever Without Backoff

**Problem:** Immediate retry on failure can overwhelm resources.

```csharp
// BAD: No backoff
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndProcessAsync(stoppingToken);
            }
            catch (Exception)
            {
                // Immediately retry - can overwhelm network/service
            }
        }
    }
}
```

**Correction:** Implement exponential backoff:

```csharp
// GOOD: Exponential backoff
public class GoodWorker : BackgroundService
{
    private readonly ILogger<GoodWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromMinutes(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndProcessAsync(stoppingToken);
                backoff = TimeSpan.FromSeconds(1); // Reset on success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection failed, retrying in {Delay}", backoff);
                await Task.Delay(backoff, stoppingToken);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, maxBackoff.Ticks));
            }
        }
    }
}
```

---

## Observability Anti-Patterns

### 15. No Health Checks

**Problem:** Workers without health checks are invisible to orchestrators like Kubernetes.

```csharp
// BAD: No health visibility
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
```

**Correction:** Add health checks:

```csharp
// GOOD: Health check integration
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<WorkerState>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHealthChecks()
    .AddCheck<WorkerHealthCheck>("worker");
builder.Services.AddHostedService<TcpHealthProbeService>();

var host = builder.Build();
host.Run();
```

### 16. Console.WriteLine Instead of Structured Logging

**Problem:** `Console.WriteLine` loses structure and is hard to aggregate.

```csharp
// BAD: Unstructured output
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Worker started at " + DateTime.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("Processing item " + itemId);
        }
    }
}
```

**Correction:** Use `ILogger` with structured data:

```csharp
// GOOD: Structured logging
public class GoodWorker : BackgroundService
{
    private readonly ILogger<GoodWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at {StartTime}", DateTime.UtcNow);
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing item {ItemId}", itemId);
        }
    }
}
```

### 17. Missing Correlation IDs

**Problem:** Log entries without correlation IDs are hard to trace across operations.

```csharp
// BAD: No correlation
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var items = await FetchItemsAsync();
            foreach (var item in items)
            {
                _logger.LogInformation("Processing item"); // Which item?
                await ProcessAsync(item);
            }
        }
    }
}
```

**Correction:** Add correlation/operation context:

```csharp
// GOOD: Correlation and context
public class GoodWorker : BackgroundService
{
    private readonly ILogger<GoodWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var batchId = Guid.NewGuid().ToString("N")[..8];
            using var scope = _logger.BeginScope(new { BatchId = batchId });

            var items = await FetchItemsAsync(stoppingToken);
            _logger.LogInformation("Fetched {Count} items", items.Count);

            foreach (var item in items)
            {
                using var itemScope = _logger.BeginScope(new { ItemId = item.Id });
                _logger.LogInformation("Processing item");
                await ProcessAsync(item, stoppingToken);
            }
        }
    }
}
```

---

## Concurrency Anti-Patterns

### 18. Shared Mutable State Without Synchronization

**Problem:** Multiple workers accessing shared state causes race conditions.

```csharp
// BAD: Unsynchronized shared state
public class BadWorker : BackgroundService
{
    private int _processedCount; // Shared mutable state

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => ProcessLoopAsync(stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcessAsync();
            _processedCount++; // Race condition
        }
    }
}
```

**Correction:** Use thread-safe operations:

```csharp
// GOOD: Thread-safe counter
public class GoodWorker : BackgroundService
{
    private int _processedCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => ProcessLoopAsync(stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcessAsync(cancellationToken);
            Interlocked.Increment(ref _processedCount);
        }
    }
}
```

### 19. Unbounded Parallelism

**Problem:** Processing items without limiting concurrency can exhaust resources.

```csharp
// BAD: Unbounded parallelism
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var items = await GetItemsAsync();
        var tasks = items.Select(item => ProcessAsync(item)); // Starts ALL at once
        await Task.WhenAll(tasks); // May exhaust connections, memory
    }
}
```

**Correction:** Use bounded parallelism:

```csharp
// GOOD: Bounded parallelism
public class GoodWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var items = await GetItemsAsync(stoppingToken);
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = stoppingToken
            },
            async (item, ct) => await ProcessAsync(item, ct));
    }
}
```

### 20. No Distributed Locking for Multi-Instance

**Problem:** Multiple worker instances process the same items without coordination.

```csharp
// BAD: No coordination between instances
public class BadWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Multiple instances may process same items
            var items = await FetchPendingItemsAsync();
            foreach (var item in items)
            {
                await ProcessAsync(item);
            }
        }
    }
}
```

**Correction:** Implement distributed locking or partitioning:

```csharp
// GOOD: Distributed lock
public class GoodWorker : BackgroundService
{
    private readonly IDistributedLockProvider _lockProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var items = await FetchPendingItemsAsync(stoppingToken);
            foreach (var item in items)
            {
                await using var @lock = await _lockProvider.TryAcquireLockAsync(
                    $"item:{item.Id}", TimeSpan.FromMinutes(5), stoppingToken);

                if (@lock != null)
                {
                    await ProcessAsync(item, stoppingToken);
                }
            }
        }
    }
}
```
