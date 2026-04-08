---
name: dotnet-worker-services
version: "1.0.0"
category: "Distributed"
description: "Build long-running .NET background services with `BackgroundService`, Generic Host, graceful shutdown, configuration, logging, and deployment patterns suited to workers and daemons."
compatibility: "Requires a worker, hosted service, or background-processing scenario."
---

# .NET Worker Services

## Trigger On

- building long-running background services or scheduled workers
- adding hosted services to an app or extracting them into a worker process
- reviewing graceful shutdown, cancellation, queue processing, or health behavior

## Documentation

- [Worker Services in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Create Windows Service using BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [App health checks in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-health-checks)
- [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)

### References

- [patterns.md](references/patterns.md) - BackgroundService patterns, graceful shutdown, and health check implementations
- [anti-patterns.md](references/anti-patterns.md) - Common worker service mistakes and how to avoid them

## Workflow

1. **Use BackgroundService as your base class:**
   - Provides standard `StartAsync`/`StopAsync` handling
   - Focus on implementing `ExecuteAsync` only
   - Proper cancellation token management built-in

2. **Handle scoped dependencies correctly:**
   - Create service scopes for scoped services
   - No scope is created by default in hosted services

3. **Implement graceful shutdown:**
   - Propagate cancellation tokens throughout
   - Complete work promptly when token fires
   - Avoid ungraceful shutdown at timeout

4. **Keep execution loop thin:**
   - Move business logic to testable services
   - Handle exceptions to prevent service crashes
   - Use `PeriodicTimer` for scheduled work

5. **Add observability:**
   - Use health checks for readiness/liveness
   - Expose metrics and structured logging
   - Consider distributed locks for multi-instance

## Basic BackgroundService Pattern

### Simple Worker
```csharp
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
                await DoWorkAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown, not an error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker iteration");
                // Continue or break based on error severity
            }
        }

        _logger.LogInformation("Worker stopping");
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        // Business logic here
    }
}
```

### Using PeriodicTimer (Recommended)
```csharp
public class TimedWorker : BackgroundService
{
    private readonly ILogger<TimedWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(1);

    public TimedWorker(ILogger<TimedWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDataProcessor>();
                await processor.ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled task");
            }
        }
    }
}
```

## Handling Scoped Dependencies

### Correct Pattern with Scope Factory
```csharp
public class ScopedWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopedWorker> _logger;

    public ScopedWorker(IServiceScopeFactory scopeFactory, ILogger<ScopedWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create scope for each unit of work
            await using var scope = _scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IScopedService>();

            await service.ProcessAsync(dbContext, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

## Queue Processing Pattern

### Message Queue Worker
```csharp
public class QueueWorker : BackgroundService
{
    private readonly ILogger<QueueWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskQueue _taskQueue;

    public QueueWorker(
        ILogger<QueueWorker> logger,
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskQueue taskQueue)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued work item");
                // Handle poison message - retry, dead-letter, etc.
            }
        }
    }
}

// Task queue interface
public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(
        Func<IServiceProvider, CancellationToken, ValueTask> workItem);

    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken);
}
```

## Health Checks for Workers

### Adding Health Check Endpoint
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<WorkerHealthCheck>("worker_health")
    .AddResourceUtilizationHealthCheck();

// Add HTTP endpoint for health checks
builder.Services.AddHealthChecksUI();

// Or use simple TCP listener for Kubernetes
builder.Services.AddSingleton<TcpHealthProbeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TcpHealthProbeService>());

var host = builder.Build();
host.Run();
```

### Custom Health Check
```csharp
public class WorkerHealthCheck : IHealthCheck
{
    private readonly WorkerState _workerState;

    public WorkerHealthCheck(WorkerState workerState)
    {
        _workerState = workerState;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_workerState.LastSuccessfulRun > DateTime.UtcNow.AddMinutes(-5))
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Last successful run: {_workerState.LastSuccessfulRun}"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"No successful run since: {_workerState.LastSuccessfulRun}"));
    }
}

// Shared state
public class WorkerState
{
    public DateTime LastSuccessfulRun { get; set; } = DateTime.UtcNow;
    public bool IsProcessing { get; set; }
}
```

## Graceful Shutdown Pattern

### Proper Shutdown Handling
```csharp
public class GracefulWorker : BackgroundService
{
    private readonly ILogger<GracefulWorker> _logger;
    private int _currentWorkItemId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            _currentWorkItemId = GetNextWorkItemId();

            try
            {
                // Pass cancellation token to all async operations
                await ProcessWorkItemAsync(_currentWorkItemId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Shutdown requested, stopping after work item {Id}", _currentWorkItemId);
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping gracefully");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Worker stopped");
    }
}
```

## Windows Service Deployment

### Configuring as Windows Service
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "My Worker Service";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

### Project File Settings
```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
  </PropertyGroup>
</Project>
```

## Best Practices

1. **Use BackgroundService as base class** - Handles `StartAsync`/`StopAsync` boilerplate and cancellation management
2. **Create scopes for scoped dependencies** - Use `IServiceScopeFactory` to resolve scoped services like DbContext
3. **Propagate cancellation tokens everywhere** - Pass to all async methods for responsive shutdown
4. **Wrap work in try-catch** - Unhandled exceptions stop the service completely
5. **Use PeriodicTimer for timed tasks** - Cleaner than `Task.Delay` with proper cancellation support
6. **Add health checks** - Essential for Kubernetes liveness/readiness probes
7. **Avoid blocking StartAsync** - Long initialization delays other hosted services
8. **Call base methods when overriding** - Always call `await base.StartAsync()` and `await base.StopAsync()`
9. **Publish as single file for Windows Service** - Reduces deployment complexity and errors
10. **Consider scaling requirements** - Separate worker projects if independent scaling is needed

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Ad-hoc `while(true)` loops | No graceful shutdown, poor lifecycle | Use `BackgroundService` |
| Ignoring cancellation token | Ungraceful shutdown, resource leaks | Propagate token to all async calls |
| Injecting scoped services directly | Captive dependencies, memory leaks | Use `IServiceScopeFactory` |
| Unhandled exceptions in `ExecuteAsync` | Silently stops the worker | Wrap in try-catch, log, continue |
| Long-running `StartAsync` | Blocks other services from starting | Move work to `ExecuteAsync` |
| `async void` methods | Crashes process on exception | Use `async Task` |
| Missing health checks | No visibility into worker status | Implement `IHealthCheck` |
| Polling with tight loops | CPU waste, no responsiveness | Use `PeriodicTimer` or event-driven |
| Not overriding `StopAsync` | Missed cleanup opportunity | Override for graceful cleanup |
| Singleton DbContext | Not thread-safe, stale data | Create scopes per operation |

## Deliver

- well-behaved worker processes and hosted services
- predictable startup and shutdown behavior
- proper scoped dependency handling
- health checks for production observability
- retry and poison-message handling for queue work

## Validate

- cancellation token propagated and shutdown honored
- scoped services resolved within proper scopes
- exception handling prevents service crashes
- health checks report accurate worker status
- runtime behavior visible through logs or telemetry
- no blocking calls in async context
