# Azure Functions Anti-Patterns

## Isolated Worker Model Anti-Patterns

### Using In-Process APIs in Isolated Worker

```csharp
// WRONG - Using in-process types in isolated worker
public class BrokenFunction
{
    [Function("BrokenHttp")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestMessage req) // Wrong type
    {
        // HttpRequestMessage is in-process model
        return new OkResult();
    }
}

// CORRECT - Using isolated worker types
public class CorrectFunction
{
    [Function("CorrectHttp")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req) // Correct type
    {
        return new OkResult();
    }
}
```

### Service Locator Instead of Constructor Injection

```csharp
// WRONG - Using service locator anti-pattern
public class ServiceLocatorFunction
{
    [Function("BadDI")]
    public async Task Run(
        [QueueTrigger("myqueue")] string message,
        FunctionContext context)
    {
        // Anti-pattern: getting services from context
        var service = context.InstanceServices.GetRequiredService<IMyService>();
        await service.ProcessAsync(message);
    }
}

// CORRECT - Constructor injection
public class ConstructorInjectionFunction(IMyService myService, ILogger<ConstructorInjectionFunction> logger)
{
    [Function("GoodDI")]
    public async Task Run([QueueTrigger("myqueue")] string message)
    {
        logger.LogInformation("Processing message");
        await myService.ProcessAsync(message);
    }
}
```

### Blocking Async Code

```csharp
// WRONG - Blocking on async operations
public class BlockingFunction
{
    [Function("BlockingCall")]
    public string Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        // Anti-pattern: blocking on async
        var result = _httpClient.GetStringAsync("https://api.example.com/data").Result;
        return result;
    }
}

// CORRECT - Async all the way
public class AsyncFunction
{
    [Function("AsyncCall")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var result = await _httpClient.GetStringAsync("https://api.example.com/data");
        return result;
    }
}
```

### Creating HttpClient Per Request

```csharp
// WRONG - Creating new HttpClient per request
public class BadHttpClientFunction
{
    [Function("BadHttpClient")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        // Anti-pattern: socket exhaustion risk
        using var client = new HttpClient();
        return await client.GetStringAsync("https://api.example.com/data");
    }
}

// CORRECT - Using IHttpClientFactory
public class GoodHttpClientFunction(IHttpClientFactory httpClientFactory)
{
    [Function("GoodHttpClient")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var client = httpClientFactory.CreateClient("ApiClient");
        return await client.GetStringAsync("https://api.example.com/data");
    }
}

// Program.cs registration
services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

### Ignoring Cancellation Tokens

```csharp
// WRONG - Ignoring cancellation
public class NoCancellationFunction
{
    [Function("NoCancellation")]
    public async Task Run(
        [QueueTrigger("longrunning")] string message)
    {
        // Anti-pattern: long operation without cancellation support
        await LongRunningOperation();
    }
}

// CORRECT - Respecting cancellation
public class CancellationAwareFunction
{
    [Function("WithCancellation")]
    public async Task Run(
        [QueueTrigger("longrunning")] string message,
        CancellationToken cancellationToken)
    {
        // Pass cancellation token to long operations
        await LongRunningOperation(cancellationToken);
    }

    private async Task LongRunningOperation(CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItem(item, cancellationToken);
        }
    }
}
```

### Static State Across Invocations

```csharp
// WRONG - Mutable static state
public class StaticStateFunction
{
    private static int _requestCount = 0; // Anti-pattern: race conditions
    private static List<string> _cache = new(); // Anti-pattern: memory growth

    [Function("StaticState")]
    public string Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _requestCount++; // Race condition in concurrent execution
        _cache.Add(req.Path); // Unbounded memory growth
        return $"Count: {_requestCount}";
    }
}

// CORRECT - Use proper distributed state
public class DistributedStateFunction(IDistributedCache cache)
{
    [Function("DistributedState")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var count = await cache.GetStringAsync("request-count") ?? "0";
        var newCount = int.Parse(count) + 1;
        await cache.SetStringAsync("request-count", newCount.ToString());
        return $"Count: {newCount}";
    }
}
```

---

## Durable Functions Anti-Patterns

### Non-Deterministic Orchestrator Code

```csharp
// WRONG - Non-deterministic operations in orchestrator
[Function(nameof(BadOrchestrator))]
public static async Task BadOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Anti-pattern: DateTime.Now changes on replay
    var timestamp = DateTime.Now;

    // Anti-pattern: Guid.NewGuid() changes on replay
    var correlationId = Guid.NewGuid();

    // Anti-pattern: Random values change on replay
    var random = new Random().Next();

    // Anti-pattern: Reading config/environment in orchestrator
    var setting = Environment.GetEnvironmentVariable("MySetting");

    // Anti-pattern: I/O in orchestrator
    var httpClient = new HttpClient();
    var result = await httpClient.GetStringAsync("https://api.example.com");
}

// CORRECT - Deterministic orchestrator
[Function(nameof(GoodOrchestrator))]
public static async Task GoodOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Use context for deterministic time
    var timestamp = context.CurrentUtcDateTime;

    // Use NewGuid from context or pass from activity
    var correlationId = context.NewGuid();

    // Move I/O and non-deterministic operations to activities
    var result = await context.CallActivityAsync<string>(nameof(FetchDataActivity), null);
    var config = await context.CallActivityAsync<ConfigData>(nameof(GetConfigActivity), null);
}
```

### Large Orchestrator Input/Output

```csharp
// WRONG - Large payloads in orchestrator history
[Function(nameof(LargePayloadOrchestrator))]
public static async Task<byte[]> LargePayloadOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Anti-pattern: Large input stored in history
    var largeInput = context.GetInput<byte[]>(); // Could be MB of data

    // Anti-pattern: Large activity result stored in history
    var processedData = await context.CallActivityAsync<byte[]>(
        nameof(ProcessLargeFile),
        largeInput); // Each call adds to history

    return processedData; // Large output in history
}

// CORRECT - Use blob storage for large payloads
[Function(nameof(SmallPayloadOrchestrator))]
public static async Task<string> SmallPayloadOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Pass blob reference instead of data
    var blobRef = context.GetInput<BlobReference>();

    // Activity processes blob and returns new reference
    var resultBlobRef = await context.CallActivityAsync<BlobReference>(
        nameof(ProcessLargeFileFromBlob),
        blobRef);

    return resultBlobRef.Url; // Small reference in history
}

public class BlobReference
{
    public string ContainerName { get; set; }
    public string BlobName { get; set; }
    public string Url => $"https://storage.blob.core.windows.net/{ContainerName}/{BlobName}";
}
```

### Shared Task Hub Across Applications

```csharp
// WRONG - Default or shared task hub name
// host.json
{
    "version": "2.0",
    "extensions": {
        "durableTask": {
            // Anti-pattern: Using default name or sharing across apps
            // "hubName": "DurableFunctionsHub" (default)
        }
    }
}

// CORRECT - Unique task hub per application
// host.json
{
    "version": "2.0",
    "extensions": {
        "durableTask": {
            "hubName": "OrderProcessingApp-Prod-Hub"
        }
    }
}
```

### Secrets in Orchestrator History

```csharp
// WRONG - Secrets passed through orchestrator
[Function(nameof(SecretsInHistoryOrchestrator))]
public static async Task SecretsInHistoryOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Anti-pattern: API key in orchestrator input (stored in history)
    var input = context.GetInput<ProcessingInput>();
    var apiKey = input.ApiKey; // Visible in Durable Functions storage

    // Anti-pattern: Fetching secrets in orchestrator
    var secret = await context.CallActivityAsync<string>(nameof(GetSecret), "my-secret");

    // Anti-pattern: Passing secret as activity input
    await context.CallActivityAsync(nameof(CallApi), new { ApiKey = secret });
}

// CORRECT - Activities fetch their own secrets
[Function(nameof(SecureOrchestrator))]
public static async Task SecureOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var input = context.GetInput<ProcessingInput>();

    // Pass only non-sensitive identifiers
    await context.CallActivityAsync(nameof(CallApiSecurely), new {
        SecretName = "api-key-name", // Just the name, not the value
        Endpoint = input.Endpoint
    });
}

[Function(nameof(CallApiSecurely))]
public static async Task CallApiSecurely(
    [ActivityTrigger] ApiCallInput input,
    FunctionContext context)
{
    // Fetch secret in activity (not stored in orchestrator history)
    var secretClient = new SecretClient(new Uri("https://myvault.vault.azure.net"), new DefaultAzureCredential());
    var apiKey = await secretClient.GetSecretAsync(input.SecretName);

    // Use secret directly
    await MakeApiCall(input.Endpoint, apiKey.Value.Value);
}
```

### Infinite Orchestration Without ContinueAsNew

```csharp
// WRONG - Unbounded history growth
[Function(nameof(InfiniteLoopOrchestrator))]
public static async Task InfiniteLoopOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    while (true)
    {
        // Anti-pattern: History grows unbounded
        await context.CallActivityAsync(nameof(DoWork), null);
        await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(5), CancellationToken.None);
        // Eventually: OutOfMemory, slow replays, storage bloat
    }
}

// CORRECT - Use ContinueAsNew to reset history
[Function(nameof(EternalOrchestrator))]
public static async Task EternalOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var state = context.GetInput<OrchestratorState>() ?? new OrchestratorState();

    // Do bounded amount of work
    for (int i = 0; i < 10; i++)
    {
        await context.CallActivityAsync(nameof(DoWork), state.LastProcessedId);
        state.IterationCount++;
        state.LastProcessedId++;
    }

    await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(5), CancellationToken.None);

    // Reset history and continue with current state
    context.ContinueAsNew(state);
}
```

### Awaiting Non-Durable Tasks

```csharp
// WRONG - Awaiting non-durable async operations
[Function(nameof(NonDurableAwaitOrchestrator))]
public static async Task NonDurableAwaitOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Anti-pattern: Direct HTTP call in orchestrator
    var client = new HttpClient();
    var result = await client.GetStringAsync("https://api.example.com"); // Not replayed correctly

    // Anti-pattern: Direct database call
    using var db = new MyDbContext();
    var data = await db.Items.ToListAsync(); // Not replayed correctly

    // Anti-pattern: Reading from file system
    var content = await File.ReadAllTextAsync("data.json"); // Not replayed correctly
}

// CORRECT - Use activities for all I/O
[Function(nameof(DurableAwaitOrchestrator))]
public static async Task DurableAwaitOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // All I/O through activities
    var apiResult = await context.CallActivityAsync<string>(nameof(CallApiActivity), null);
    var dbData = await context.CallActivityAsync<List<Item>>(nameof(GetDatabaseItemsActivity), null);
    var fileContent = await context.CallActivityAsync<string>(nameof(ReadFileActivity), "data.json");
}
```

### Missing Retry Policies for External Calls

```csharp
// WRONG - No retry policy for unreliable operations
[Function(nameof(NoRetryOrchestrator))]
public static async Task NoRetryOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // Anti-pattern: External call without retry
    var result = await context.CallActivityAsync<string>(nameof(CallExternalApi), null);
    // First transient failure = orchestration failure
}

// CORRECT - Configure appropriate retry policies
[Function(nameof(RetryOrchestrator))]
public static async Task RetryOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var retryOptions = new TaskOptions
    {
        Retry = new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(1),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromMinutes(1),
            retryTimeout: TimeSpan.FromMinutes(5))
    };

    var result = await context.CallActivityAsync<string>(
        nameof(CallExternalApi),
        null,
        retryOptions);
}
```

---

## Configuration and Deployment Anti-Patterns

### Hardcoded Connection Strings

```csharp
// WRONG - Hardcoded secrets
public class HardcodedSecretsFunction
{
    [Function("HardcodedSecrets")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        // Anti-pattern: Secrets in code
        var connectionString = "AccountName=myaccount;AccountKey=abc123...";
        var apiKey = "sk-live-abcdefg";
    }
}

// CORRECT - Use configuration and Key Vault
// Program.cs
var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var builtConfig = config.Build();
        var keyVaultUri = builtConfig["KeyVaultUri"];
        config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    })
    .Build();

// Function
public class SecureFunction(IConfiguration configuration)
{
    [Function("SecureConfig")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        var connectionString = configuration["StorageConnectionString"]; // From Key Vault
    }
}
```

### Missing Logging Correlation

```csharp
// WRONG - Basic logging without correlation
public class NoCorrelationFunction
{
    private readonly ILogger _logger;

    [Function("NoCorrelation")]
    public async Task Run([QueueTrigger("myqueue")] string message)
    {
        // Anti-pattern: No correlation across function calls
        _logger.LogInformation("Processing message");
        await CallDownstreamService();
        _logger.LogInformation("Done"); // Can't correlate with downstream
    }
}

// CORRECT - Correlation via middleware and structured logging
// Middleware
public class CorrelationMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var correlationId = context.TraceContext?.TraceParent
            ?? Guid.NewGuid().ToString();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["FunctionName"] = context.FunctionDefinition.Name
        }))
        {
            await next(context);
        }
    }
}
```

### Over-Provisioned or Under-Provisioned Concurrency

```json
// WRONG - Default concurrency for all workloads
{
    "version": "2.0"
    // No concurrency configuration - defaults may not fit workload
}

// WRONG - Aggressive concurrency for CPU-bound work
{
    "version": "2.0",
    "extensions": {
        "queues": {
            "batchSize": 32,
            "newBatchThreshold": 16
        }
    }
    // CPU-bound functions will compete for threads
}

// CORRECT - Tuned for workload type
// For I/O-bound queue processing
{
    "version": "2.0",
    "extensions": {
        "queues": {
            "batchSize": 16,
            "newBatchThreshold": 8,
            "maxDequeueCount": 5,
            "visibilityTimeout": "00:05:00"
        }
    }
}

// For CPU-intensive Durable Functions
{
    "version": "2.0",
    "extensions": {
        "durableTask": {
            "maxConcurrentActivityFunctions": 4,
            "maxConcurrentOrchestratorFunctions": 2
        }
    }
}
```

### Ignoring Cold Start Impact

```csharp
// WRONG - Heavy initialization in function code
public class HeavyInitFunction
{
    [Function("HeavyInit")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        // Anti-pattern: Initialization on every cold start
        var bigCache = LoadLargeDataset(); // 5 seconds
        var mlModel = LoadMachineLearningModel(); // 10 seconds

        return "Ready";
    }
}

// CORRECT - Lazy initialization or warm-up
public class OptimizedInitFunction
{
    private static readonly Lazy<BigDataset> _cache = new(() => LoadLargeDataset());

    [Function("OptimizedInit")]
    public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        // Lazy loaded once, reused across invocations
        var data = _cache.Value;
        return "Ready";
    }
}

// Program.cs - Pre-warm in startup
services.AddSingleton<IModelService>(sp =>
{
    var service = new ModelService();
    // Warm up during startup, not first request
    _ = service.EnsureModelLoadedAsync();
    return service;
});
```

### Missing Health Checks for Dependencies

```csharp
// WRONG - No health checks, silent failures
public class NoHealthCheckFunction(IMyService service)
{
    [Function("NoHealthCheck")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        // Fails with cryptic errors if database is down
        var result = await service.GetDataAsync();
        return new OkObjectResult(result);
    }
}

// CORRECT - Health endpoint for monitoring
public class HealthCheckFunction(
    IMyService service,
    IHealthCheck[] healthChecks)
{
    [Function("HealthCheck")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        var results = new Dictionary<string, string>();
        var isHealthy = true;

        foreach (var check in healthChecks)
        {
            try
            {
                var result = await check.CheckHealthAsync(new HealthCheckContext());
                results[check.GetType().Name] = result.Status.ToString();
                if (result.Status != HealthStatus.Healthy)
                {
                    isHealthy = false;
                }
            }
            catch (Exception ex)
            {
                results[check.GetType().Name] = $"Failed: {ex.Message}";
                isHealthy = false;
            }
        }

        return isHealthy
            ? new OkObjectResult(new { status = "Healthy", checks = results })
            : new ObjectResult(new { status = "Unhealthy", checks = results })
              { StatusCode = 503 };
    }
}
```
