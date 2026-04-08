# Worker Services Patterns Reference

This reference covers BackgroundService patterns, graceful shutdown implementation, and health check patterns for .NET worker services.

## BackgroundService Patterns

### 1. Basic Worker Pattern

The simplest pattern for periodic work:

```csharp
public class BasicWorker : BackgroundService
{
    private readonly ILogger<BasicWorker> _logger;

    public BasicWorker(ILogger<BasicWorker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing at {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### 2. Scoped Dependency Pattern

Create a new scope for each unit of work to properly handle scoped dependencies like DbContext:

```csharp
public class ScopedDependencyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopedDependencyWorker> _logger;

    public ScopedDependencyWorker(IServiceScopeFactory scopeFactory, ILogger<ScopedDependencyWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = scope.ServiceProvider.GetRequiredService<IDataProcessor>();

            await processor.ProcessBatchAsync(dbContext, stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### 3. PeriodicTimer Pattern

Preferred over `Task.Delay` for scheduled work with cleaner cancellation semantics:

```csharp
public class PeriodicTimerWorker : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PeriodicTimerWorker> _logger;

    public PeriodicTimerWorker(IServiceScopeFactory scopeFactory, ILogger<PeriodicTimerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        await RunIterationAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunIterationAsync(stoppingToken);
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IScheduledService>();
            await service.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in scheduled iteration");
        }
    }
}
```

### 4. Queue Consumer Pattern

Process items from a queue with proper backpressure and error handling:

```csharp
public class QueueConsumerWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueConsumerWorker> _logger;

    public QueueConsumerWorker(
        IBackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<QueueConsumerWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Consumer starting");

        await foreach (var workItem in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await workItem.ExecuteAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing work item {Id}", workItem.Id);
                await HandleFailedItemAsync(workItem, ex);
            }
        }
    }

    private Task HandleFailedItemAsync(IWorkItem workItem, Exception exception)
    {
        // Implement retry, dead-letter, or alerting logic
        return Task.CompletedTask;
    }
}
```

### 5. Parallel Worker Pattern

Process multiple items concurrently with controlled parallelism:

```csharp
public class ParallelWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParallelWorker> _logger;
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount;

    public ParallelWorker(IServiceScopeFactory scopeFactory, ILogger<ParallelWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // Start producer
        var producer = ProduceItemsAsync(channel.Writer, stoppingToken);

        // Start consumers
        var consumers = Enumerable.Range(0, _maxDegreeOfParallelism)
            .Select(_ => ConsumeItemsAsync(channel.Reader, stoppingToken))
            .ToArray();

        await Task.WhenAll(consumers.Prepend(producer));
    }

    private async Task ProduceItemsAsync(ChannelWriter<WorkItem> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var source = scope.ServiceProvider.GetRequiredService<IWorkItemSource>();

                await foreach (var item in source.GetItemsAsync(cancellationToken))
                {
                    await writer.WriteAsync(item, cancellationToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ConsumeItemsAsync(ChannelReader<WorkItem> reader, CancellationToken cancellationToken)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWorkItemProcessor>();
                await processor.ProcessAsync(item, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item {Id}", item.Id);
            }
        }
    }
}
```

### 6. Event-Driven Worker Pattern

React to external events instead of polling:

```csharp
public class EventDrivenWorker : BackgroundService
{
    private readonly IMessageSubscriber _subscriber;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventDrivenWorker> _logger;

    public EventDrivenWorker(
        IMessageSubscriber subscriber,
        IServiceScopeFactory scopeFactory,
        ILogger<EventDrivenWorker> logger)
    {
        _subscriber = subscriber;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriber.SubscribeAsync<OrderCreatedEvent>(
            async (message, ct) =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IOrderHandler>();
                await handler.HandleAsync(message, ct);
            },
            stoppingToken);

        // Keep alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

---

## Graceful Shutdown Patterns

### 1. Basic Graceful Shutdown

Respond to cancellation token and complete current work:

```csharp
public class GracefulShutdownWorker : BackgroundService
{
    private readonly ILogger<GracefulShutdownWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCurrentBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Shutdown requested, completing gracefully");
                break;
            }
        }

        _logger.LogInformation("Worker completed shutdown");
    }
}
```

### 2. StopAsync Override for Cleanup

Override StopAsync for explicit cleanup operations:

```csharp
public class CleanupWorker : BackgroundService
{
    private readonly ILogger<CleanupWorker> _logger;
    private readonly IExternalConnection _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _connection.ConnectAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _connection.ProcessMessagesAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping worker, disconnecting...");

        try
        {
            await _connection.DisconnectAsync(cancellationToken);
            _logger.LogInformation("Disconnected cleanly");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect, may have leaked resources");
        }

        await base.StopAsync(cancellationToken);
    }
}
```

### 3. Work-in-Progress Tracking

Track ongoing work to ensure completion before shutdown:

```csharp
public class TrackedWorkWorker : BackgroundService
{
    private readonly ILogger<TrackedWorkWorker> _logger;
    private readonly SemaphoreSlim _workTracker = new(0, int.MaxValue);
    private int _activeWorkItems;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateUnbounded<WorkItem>();

        // Start processor
        var processor = ProcessItemsAsync(channel.Reader, stoppingToken);

        // Produce items until cancelled
        while (!stoppingToken.IsCancellationRequested)
        {
            var items = await FetchItemsAsync(stoppingToken);
            foreach (var item in items)
            {
                Interlocked.Increment(ref _activeWorkItems);
                await channel.Writer.WriteAsync(item, stoppingToken);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        channel.Writer.Complete();
        await processor;
    }

    private async Task ProcessItemsAsync(ChannelReader<WorkItem> reader, CancellationToken cancellationToken)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessItemAsync(item, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkItems);
                _workTracker.Release();
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for {Count} work items to complete", _activeWorkItems);

        // Wait for all work to complete or timeout
        var timeout = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        while (_activeWorkItems > 0 && !timeout.IsCompleted)
        {
            await Task.WhenAny(_workTracker.WaitAsync(cancellationToken), timeout);
        }

        if (_activeWorkItems > 0)
        {
            _logger.LogWarning("Shutdown with {Count} items still processing", _activeWorkItems);
        }

        await base.StopAsync(cancellationToken);
    }
}
```

### 4. Configurable Shutdown Timeout

Configure host shutdown timeout for long-running operations:

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHostedService<LongRunningWorker>();
```

### 5. Two-Phase Shutdown

Implement soft and hard shutdown phases:

```csharp
public class TwoPhaseShutdownWorker : BackgroundService
{
    private readonly ILogger<TwoPhaseShutdownWorker> _logger;
    private volatile bool _softShutdownRequested;
    private readonly CancellationTokenSource _internalCts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, _internalCts.Token);

        stoppingToken.Register(() => _softShutdownRequested = true);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            if (_softShutdownRequested)
            {
                _logger.LogInformation("Soft shutdown: finishing current batch");
                // Complete current iteration but don't start new work
            }

            await ProcessBatchAsync(linkedCts.Token);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Phase 1: Soft shutdown (stop accepting new work)
        _softShutdownRequested = true;

        // Give time for graceful completion
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Phase 2: Hard shutdown (cancel internal operations)
        _internalCts.Cancel();

        await base.StopAsync(cancellationToken);
    }
}
```

---

## Health Check Patterns

### 1. Basic Worker Health Check

Track last successful execution time:

```csharp
public class WorkerHealthCheck : IHealthCheck
{
    private readonly WorkerState _state;
    private readonly TimeSpan _maxAge = TimeSpan.FromMinutes(5);

    public WorkerHealthCheck(WorkerState state) => _state = state;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var timeSinceLastRun = DateTime.UtcNow - _state.LastSuccessfulRun;

        if (timeSinceLastRun > _maxAge)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"No successful run in {timeSinceLastRun.TotalMinutes:F1} minutes"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Last run: {_state.LastSuccessfulRun:O}"));
    }
}

public class WorkerState
{
    public DateTime LastSuccessfulRun { get; set; } = DateTime.UtcNow;
    public int ConsecutiveFailures { get; set; }
    public bool IsProcessing { get; set; }
}
```

### 2. Liveness vs Readiness Health Checks

Separate liveness (is the process alive) from readiness (can it accept work):

```csharp
// Liveness: Is the worker process running and not deadlocked?
public class WorkerLivenessCheck : IHealthCheck
{
    private readonly WorkerState _state;
    private readonly TimeSpan _maxProcessingTime = TimeSpan.FromMinutes(10);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_state.IsProcessing && _state.ProcessingStarted < DateTime.UtcNow - _maxProcessingTime)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Worker appears stuck - processing for too long"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}

// Readiness: Is the worker ready to handle new work?
public class WorkerReadinessCheck : IHealthCheck
{
    private readonly WorkerState _state;
    private readonly IDbConnectionFactory _dbFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check dependencies are available
        try
        {
            await using var connection = await _dbFactory.CreateConnectionAsync(cancellationToken);
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unavailable", ex);
        }

        if (_state.ConsecutiveFailures > 5)
        {
            return HealthCheckResult.Degraded("Multiple consecutive failures");
        }

        return HealthCheckResult.Healthy();
    }
}
```

### 3. Health Check Registration

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<WorkerState>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddHealthChecks()
    .AddCheck<WorkerLivenessCheck>("liveness", tags: ["live"])
    .AddCheck<WorkerReadinessCheck>("readiness", tags: ["ready"])
    .AddCheck<WorkerHealthCheck>("worker", tags: ["worker"]);

// For workers without ASP.NET Core, expose via TCP
builder.Services.AddHostedService<TcpHealthProbeService>();
```

### 4. TCP Health Probe for Kubernetes

Expose health without HTTP for pure workers:

```csharp
public class TcpHealthProbeService : BackgroundService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<TcpHealthProbeService> _logger;
    private readonly int _port;

    public TcpHealthProbeService(
        HealthCheckService healthCheckService,
        ILogger<TcpHealthProbeService> logger,
        IConfiguration configuration)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
        _port = configuration.GetValue<int>("HealthProbe:TcpPort", 8080);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        _logger.LogInformation("Health probe listening on port {Port}", _port);

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
        }

        listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var result = await _healthCheckService.CheckHealthAsync(cancellationToken);
            var response = result.Status == HealthStatus.Healthy ? "HTTP/1.1 200 OK\r\n\r\n" : "HTTP/1.1 503 Service Unavailable\r\n\r\n";
            var bytes = Encoding.UTF8.GetBytes(response);
            await client.GetStream().WriteAsync(bytes, cancellationToken);
        }
    }
}
```

### 5. Startup Health Check

Report unhealthy during initialization:

```csharp
public class StartupHealthCheck : IHealthCheck
{
    private volatile bool _isReady;

    public bool IsReady
    {
        get => _isReady;
        set => _isReady = value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_isReady)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Startup complete"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Still starting up"));
    }
}

public class InitializingWorker : BackgroundService
{
    private readonly StartupHealthCheck _startupCheck;
    private readonly ILogger<InitializingWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Initializing...");
        await InitializeAsync(stoppingToken);
        _startupCheck.IsReady = true;
        _logger.LogInformation("Initialization complete, starting work");

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
        }
    }
}
```

### 6. Circuit Breaker Health Check

Track error rates and circuit state:

```csharp
public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly CircuitBreakerState _state;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state.State switch
        {
            CircuitState.Closed => HealthCheckResult.Healthy("Circuit closed"),
            CircuitState.HalfOpen => HealthCheckResult.Degraded("Circuit half-open, testing recovery"),
            CircuitState.Open => HealthCheckResult.Unhealthy($"Circuit open until {_state.ResetTime:O}"),
            _ => HealthCheckResult.Unhealthy("Unknown circuit state")
        });
    }
}

public class CircuitBreakerState
{
    public CircuitState State { get; set; } = CircuitState.Closed;
    public DateTime ResetTime { get; set; }
    public int FailureCount { get; set; }
}

public enum CircuitState { Closed, Open, HalfOpen }
```
