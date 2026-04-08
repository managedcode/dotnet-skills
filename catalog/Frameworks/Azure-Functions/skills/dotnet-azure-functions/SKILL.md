---
name: dotnet-azure-functions
version: "1.0.0"
category: "Cloud"
description: "Build, review, or migrate Azure Functions in .NET with correct execution model, isolated worker setup, bindings, DI, and Durable Functions patterns."
compatibility: "Requires an Azure Functions project or a migration plan for one."
---

# Azure Functions for .NET

## Trigger On

- working on Azure Functions in .NET
- migrating from the in-process model to the isolated worker model
- adding Durable Functions, bindings, or host configuration

## Documentation

- [Guide for running C# Azure Functions in an isolated worker process](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Differences between in-process and isolated worker process](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-in-process-differences)
- [Migrate C# app from in-process to isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model)
- [Durable Functions overview](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview)
- [Durable Functions best practices and diagnostic tools](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-best-practice-reference)

### References

- [Patterns](references/patterns.md) - Isolated worker patterns, Durable Functions patterns, advanced binding patterns
- [Anti-Patterns](references/anti-patterns.md) - Common Azure Functions mistakes and how to avoid them

## Workflow

1. **Use isolated worker model for all new work:**
   - In-process model reaches end of support on November 10, 2026
   - Runtime v1.x ends support on September 14, 2026
   - Target .NET 8+ for longest support window

2. **Detect current project shape:**
   - Target framework and runtime version
   - Worker model (isolated vs in-process)
   - Binding packages and host configuration

3. **Use standard .NET patterns in isolated model:**
   - Normal dependency injection
   - Middleware pipeline
   - `IOptions<T>` for configuration
   - `ILogger<T>` for logging

4. **For Durable Functions:**
   - Validate orchestration determinism constraints
   - Handle replay behavior correctly
   - Use typed activity patterns

5. **Verify both local and deployment behavior.**

## Isolated Worker Model Setup

### Basic Function with DI
```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<IMyService, MyService>();
    })
    .Build();

host.Run();
```

### HTTP Trigger Function
```csharp
public class HttpFunctions(ILogger<HttpFunctions> logger, IMyService myService)
{
    [Function("GetItems")]
    public async Task<IActionResult> GetItems(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "items")] HttpRequest req)
    {
        logger.LogInformation("Processing GetItems request");
        var items = await myService.GetItemsAsync();
        return new OkObjectResult(items);
    }
}
```

### Queue Trigger with Options
```csharp
public class QueueFunctions(ILogger<QueueFunctions> logger, IOptions<ProcessingOptions> options)
{
    [Function("ProcessMessage")]
    public async Task ProcessMessage(
        [QueueTrigger("myqueue", Connection = "AzureWebJobsStorage")] string message)
    {
        logger.LogInformation("Processing message: {Message}", message);
        // Process with retry policy from options
    }
}
```

## Middleware Pattern

### Custom Middleware
```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseMiddleware<ExceptionHandlingMiddleware>();
        builder.UseMiddleware<CorrelationIdMiddleware>();
    })
    .Build();

// CorrelationIdMiddleware.cs
public class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var correlationId = context.Features.Get<IHttpRequestFeature>()?.Headers["X-Correlation-Id"]
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;

        await next(context);
    }
}
```

## Durable Functions Patterns

### Function Chaining
```csharp
[Function(nameof(ChainOrchestrator))]
public static async Task<string> ChainOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var result1 = await context.CallActivityAsync<string>(nameof(Step1), "input");
    var result2 = await context.CallActivityAsync<string>(nameof(Step2), result1);
    var result3 = await context.CallActivityAsync<string>(nameof(Step3), result2);
    return result3;
}

[Function(nameof(Step1))]
public static string Step1([ActivityTrigger] string input) => $"Step1({input})";

[Function(nameof(Step2))]
public static string Step2([ActivityTrigger] string input) => $"Step2({input})";

[Function(nameof(Step3))]
public static string Step3([ActivityTrigger] string input) => $"Step3({input})";
```

### Fan-Out/Fan-In
```csharp
[Function(nameof(FanOutFanInOrchestrator))]
public static async Task<int[]> FanOutFanInOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var workItems = await context.CallActivityAsync<string[]>(nameof(GetWorkItems), null);

    // Fan out - process all items in parallel
    var tasks = workItems.Select(item =>
        context.CallActivityAsync<int>(nameof(ProcessWorkItem), item));

    // Fan in - wait for all to complete
    var results = await Task.WhenAll(tasks);

    return results;
}

[Function(nameof(ProcessWorkItem))]
public static int ProcessWorkItem([ActivityTrigger] string item)
{
    // Process item and return result
    return item.Length;
}
```

### Human Interaction Pattern
```csharp
[Function(nameof(ApprovalOrchestrator))]
public static async Task<string> ApprovalOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    var request = context.GetInput<ApprovalRequest>();

    // Send notification
    await context.CallActivityAsync(nameof(SendApprovalRequest), request);

    // Wait for external event with timeout
    using var cts = new CancellationTokenSource();
    var approvalTask = context.WaitForExternalEvent<bool>("ApprovalEvent");
    var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.AddDays(7), cts.Token);

    var winner = await Task.WhenAny(approvalTask, timeoutTask);

    if (winner == approvalTask)
    {
        cts.Cancel();
        return approvalTask.Result ? "Approved" : "Rejected";
    }

    return "Timed out";
}
```

## Best Practices

1. **Use isolated worker model for new development** - Full .NET ecosystem access, middleware support, and longer support lifecycle
2. **Inject dependencies via constructor** - Use `ILogger<T>` and service interfaces for testability
3. **Keep orchestrator code deterministic** - No I/O, random, DateTime.Now, or Guid.NewGuid() in orchestrators
4. **Handle sensitive data in activities** - Fetch secrets from Key Vault in activity functions, never in orchestrators
5. **Use unique task hub names** - Prevent accidental sharing when multiple apps use the same storage
6. **Avoid large inputs/outputs** - Serialize to blob storage for large payloads to prevent history bloat
7. **Configure concurrency limits** - Set appropriate limits in host.json for resource-intensive functions
8. **Keep SDK and extensions updated** - Latest versions include performance improvements and bug fixes

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Mixing in-process and isolated guidance | Incompatible APIs and patterns | Choose one model consistently |
| Non-deterministic orchestrator code | Replay failures, stuck orchestrations | Use `context.CurrentUtcDateTime`, no I/O |
| Large orchestrator inputs/outputs | History bloat, memory issues | Store large data in blob storage |
| Shared task hub names | Message conflicts, stuck orchestrations | Use unique names per app |
| Secrets in orchestrator history | Security risk, exposed in logs | Fetch secrets in activity functions |
| Blocking calls in async functions | Thread pool exhaustion | Use `await` throughout |
| Missing retry policies | Transient failures cause job loss | Configure retry in bindings or code |
| Ignoring execution model migration | EOL November 2026 for in-process | Migrate to isolated worker model |

## Deployment Considerations

### Linux Consumption Plan Limitations
```
.NET 10+ apps cannot run on Linux Consumption plan.
Use Flex Consumption plan or App Service for .NET 10+.
.NET 9 is the last version supported on Linux Consumption.
```

### host.json Configuration
```json
{
  "version": "2.0",
  "extensions": {
    "durableTask": {
      "hubName": "MyUniqueTaskHub",
      "maxConcurrentActivityFunctions": 10,
      "maxConcurrentOrchestratorFunctions": 5
    }
  },
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    }
  }
}
```

## Deliver

- correct Functions project setup for the isolated worker model
- clear binding and host configuration
- middleware for cross-cutting concerns
- Durable Functions with proper orchestration patterns
- migration-safe guidance when upgrading execution models

## Validate

- execution model guidance is consistent (isolated only for new work)
- orchestrator code is deterministic
- bindings and host settings match the target runtime
- large payloads are externalized to blob storage
- retry policies are configured for transient failures
- local and deployment behavior are both verified
