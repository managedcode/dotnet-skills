# Microsoft.Extensions Anti-Patterns

## Dependency Injection Anti-Patterns

### Service Locator Pattern

**Problem**: Resolving services manually instead of using constructor injection.

```csharp
// BAD: Service locator
public class OrderService(IServiceProvider serviceProvider)
{
    public async Task ProcessAsync(Order order)
    {
        // Hidden dependency, hard to test, breaks DI benefits
        var repository = serviceProvider.GetRequiredService<IOrderRepository>();
        await repository.SaveAsync(order);
    }
}

// GOOD: Explicit constructor injection
public class OrderService(IOrderRepository repository)
{
    public async Task ProcessAsync(Order order)
    {
        await repository.SaveAsync(order);
    }
}
```

**Exception**: `IServiceProvider` is acceptable in factory classes or hosted services that need to create scopes.

### Captive Dependencies

**Problem**: A singleton service captures a scoped or transient dependency.

```csharp
// BAD: Singleton captures scoped DbContext
services.AddSingleton<ICacheService, CacheService>();
services.AddDbContext<AppDbContext>(); // Scoped by default

public class CacheService(AppDbContext dbContext) : ICacheService
{
    // dbContext is now captive - same instance for entire app lifetime
    // This causes threading issues and stale data
}

// GOOD: Use IServiceScopeFactory for scoped dependencies
public class CacheService(IServiceScopeFactory scopeFactory) : ICacheService
{
    public async Task RefreshAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Use dbContext within this scope
    }
}
```

### Constructor Over-Injection

**Problem**: Too many dependencies indicate a class is doing too much.

```csharp
// BAD: 8+ dependencies suggest SRP violation
public class OrderProcessor(
    IOrderRepository orderRepository,
    ICustomerRepository customerRepository,
    IInventoryService inventoryService,
    IPaymentService paymentService,
    IShippingService shippingService,
    INotificationService notificationService,
    ILogger<OrderProcessor> logger,
    IOptions<OrderSettings> options,
    IAuditService auditService,
    IMetricsService metricsService)
{
    // This class is doing too much
}

// GOOD: Split into focused services or use aggregates
public class OrderProcessor(
    IOrderWorkflow workflow,
    ILogger<OrderProcessor> logger,
    IOptions<OrderSettings> options)
{
    public async Task ProcessAsync(Order order)
    {
        await workflow.ExecuteAsync(order);
    }
}
```

### Registering Implementations Instead of Interfaces

**Problem**: Registering concrete types makes testing and swapping implementations difficult.

```csharp
// BAD: Hard to mock in tests
services.AddScoped<OrderService>();

// GOOD: Register interface to implementation
services.AddScoped<IOrderService, OrderService>();
```

### Inappropriate Lifetimes

**Problem**: Using wrong service lifetimes.

```csharp
// BAD: Transient for expensive-to-create services
services.AddTransient<IExpensiveService, ExpensiveService>();

// BAD: Singleton for services with request-specific state
services.AddSingleton<IUserContext, UserContext>();

// GOOD: Match lifetime to actual requirements
services.AddSingleton<IExpensiveService, ExpensiveService>(); // Create once
services.AddScoped<IUserContext, UserContext>(); // Per-request state
```

---

## Configuration Anti-Patterns

### Magic Strings Everywhere

**Problem**: Configuration keys scattered as strings throughout the code.

```csharp
// BAD: Magic strings
var connectionString = configuration["ConnectionStrings:DefaultConnection"];
var timeout = int.Parse(configuration["HttpClient:Timeout"]);

// GOOD: Strongly typed configuration
public class HttpClientSettings
{
    public const string SectionName = "HttpClient";
    public int Timeout { get; init; } = 30;
}

services.Configure<HttpClientSettings>(configuration.GetSection(HttpClientSettings.SectionName));
```

### No Validation

**Problem**: Invalid configuration discovered at runtime instead of startup.

```csharp
// BAD: Fails at runtime when service is first used
public class EmailService(IOptions<EmailSettings> options)
{
    public void Send(string to, string subject, string body)
    {
        // Crashes here if SmtpHost is null
        using var client = new SmtpClient(options.Value.SmtpHost);
    }
}

// GOOD: Validate at startup
services.AddOptions<EmailSettings>()
    .BindConfiguration("Email")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Secrets in Configuration Files

**Problem**: Sensitive data in appsettings.json checked into source control.

```json
// BAD: appsettings.json in source control
{
  "Database": {
    "ConnectionString": "Server=prod;Password=actualPassword123"
  }
}
```

**Solutions**:
- Use User Secrets for local development
- Use environment variables for production
- Use Azure Key Vault, AWS Secrets Manager, or similar
- Use `appsettings.Development.json` (gitignored) for local overrides

### Ignoring Configuration Hierarchy

**Problem**: Not understanding that later sources override earlier ones.

```csharp
// Configuration sources (later overrides earlier):
// 1. appsettings.json
// 2. appsettings.{Environment}.json
// 3. Environment variables
// 4. Command-line arguments

// BAD: Adding JSON after environment variables reverses expected precedence
builder.Configuration.AddJsonFile("override.json"); // Now overrides env vars!

// GOOD: Understand and maintain intended precedence
builder.Configuration.AddJsonFile("defaults.json", optional: true);
// Environment variables and command-line args still win
```

---

## Options Pattern Anti-Patterns

### Using IOptions When Reload Is Needed

**Problem**: `IOptions<T>` never reloads after startup.

```csharp
// BAD: IOptions won't see configuration changes
public class FeatureService(IOptions<FeatureFlags> options)
{
    public bool IsEnabled(string feature) =>
        options.Value.EnabledFeatures.Contains(feature);
    // This never updates even if config file changes
}

// GOOD: Use IOptionsMonitor for live updates
public class FeatureService(IOptionsMonitor<FeatureFlags> options)
{
    public bool IsEnabled(string feature) =>
        options.CurrentValue.EnabledFeatures.Contains(feature);
}
```

### Mutable Options Classes

**Problem**: Options objects that can be modified after binding.

```csharp
// BAD: Mutable options
public class ApiSettings
{
    public string BaseUrl { get; set; } = "";
    public int Timeout { get; set; }
}

// GOOD: Immutable options with init-only setters
public class ApiSettings
{
    public required string BaseUrl { get; init; }
    public int Timeout { get; init; } = 30;
}
```

### Complex Logic in Options Classes

**Problem**: Adding business logic to configuration POCOs.

```csharp
// BAD: Options class with logic
public class RetrySettings
{
    public int MaxAttempts { get; init; }
    public int BaseDelayMs { get; init; }

    // Don't put business logic here
    public TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt));
}

// GOOD: Keep options as pure data; logic belongs in services
public class RetrySettings
{
    public int MaxAttempts { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 100;
}

public class RetryPolicy(IOptions<RetrySettings> options)
{
    public TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromMilliseconds(options.Value.BaseDelayMs * Math.Pow(2, attempt));
}
```

---

## Logging Anti-Patterns

### String Interpolation in Log Messages

**Problem**: String interpolation defeats structured logging and always allocates.

```csharp
// BAD: String interpolation
logger.LogInformation($"Processing order {order.Id} for customer {order.CustomerId}");

// GOOD: Message templates
logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
    order.Id, order.CustomerId);
```

### Logging Sensitive Data

**Problem**: Accidentally logging PII, credentials, or secrets.

```csharp
// BAD: Logging sensitive data
logger.LogDebug("User login: {Email} with password {Password}", email, password);
logger.LogInformation("Processing payment for card {CardNumber}", cardNumber);

// GOOD: Redact or omit sensitive data
logger.LogDebug("User login attempt for {Email}", email);
logger.LogInformation("Processing payment for card ending in {CardLast4}",
    cardNumber[^4..]);
```

### Incorrect Log Levels

**Problem**: Using wrong log levels makes filtering difficult.

```csharp
// BAD: Using Information for errors
logger.LogInformation("Failed to connect to database: {Error}", ex.Message);

// BAD: Using Error for normal operations
logger.LogError("Request completed successfully");

// BAD: Using Debug for critical failures
logger.LogDebug("Payment processing failed: {Error}", ex.Message);

// GOOD: Match level to severity
logger.LogDebug("Entering method {MethodName}", nameof(ProcessAsync));
logger.LogInformation("Order {OrderId} processed successfully", orderId);
logger.LogWarning("Retry attempt {Attempt} of {MaxAttempts}", attempt, max);
logger.LogError(ex, "Payment {PaymentId} failed", paymentId);
logger.LogCritical("Database connection pool exhausted");
```

### Missing Exception in Log

**Problem**: Logging exception message but not the exception itself.

```csharp
// BAD: Loses stack trace and exception type
catch (Exception ex)
{
    logger.LogError("Operation failed: {Message}", ex.Message);
}

// GOOD: Pass exception as first parameter
catch (Exception ex)
{
    logger.LogError(ex, "Operation failed for order {OrderId}", orderId);
}
```

### Logging Inside Tight Loops

**Problem**: Excessive logging in hot paths kills performance.

```csharp
// BAD: Logging every iteration
foreach (var item in items) // Could be millions
{
    logger.LogDebug("Processing item {ItemId}", item.Id);
    Process(item);
}

// GOOD: Log summary or use conditional logging
logger.LogInformation("Processing {Count} items", items.Count);
foreach (var item in items)
{
    Process(item);
}
logger.LogInformation("Completed processing {Count} items", items.Count);
```

---

## HttpClient Anti-Patterns

### Creating HttpClient Directly

**Problem**: Not using `IHttpClientFactory` leads to socket exhaustion.

```csharp
// BAD: Creates new HttpClient per request
public async Task<string> GetDataAsync(string url)
{
    using var client = new HttpClient(); // Socket exhaustion risk
    return await client.GetStringAsync(url);
}

// BAD: Singleton HttpClient ignores DNS changes
private static readonly HttpClient _client = new();

// GOOD: Use IHttpClientFactory
public class ApiClient(HttpClient httpClient)
{
    public Task<string> GetDataAsync(string path) =>
        httpClient.GetStringAsync(path);
}
```

### Not Disposing HttpResponseMessage

**Problem**: Leaking connections by not disposing responses.

```csharp
// BAD: Response not disposed
public async Task<string?> GetValueAsync(string url)
{
    var response = await httpClient.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        return null; // Response not disposed!
    return await response.Content.ReadAsStringAsync();
}

// GOOD: Always dispose response
public async Task<string?> GetValueAsync(string url)
{
    using var response = await httpClient.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        return null;
    return await response.Content.ReadAsStringAsync();
}
```

### Ignoring Cancellation Tokens

**Problem**: Not passing cancellation tokens to async operations.

```csharp
// BAD: No cancellation support
public async Task<Data> GetDataAsync()
{
    var response = await httpClient.GetAsync("api/data");
    return await response.Content.ReadFromJsonAsync<Data>();
}

// GOOD: Support cancellation throughout
public async Task<Data?> GetDataAsync(CancellationToken cancellationToken = default)
{
    using var response = await httpClient.GetAsync("api/data", cancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<Data>(cancellationToken);
}
```

---

## Hosted Service Anti-Patterns

### Blocking in ExecuteAsync

**Problem**: Blocking the hosted service startup.

```csharp
// BAD: Blocks application startup
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        Thread.Sleep(1000); // Blocks thread!
        await ProcessAsync();
    }
}

// GOOD: Use async delays
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
    }
}
```

### Not Handling Exceptions

**Problem**: Unhandled exceptions crash the hosted service silently.

```csharp
// BAD: Unhandled exception stops the service
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await DoWorkAsync(stoppingToken); // Exception kills the loop
    }
}

// GOOD: Handle exceptions and continue
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
            // Expected during shutdown
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in background processing");
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

### Injecting Scoped Services Directly

**Problem**: Scoped services injected into singleton hosted services.

```csharp
// BAD: Scoped DbContext in singleton BackgroundService
public class DataSyncService(AppDbContext dbContext) : BackgroundService
{
    // dbContext is now a captive dependency with wrong lifetime
}

// GOOD: Create scope for each unit of work
public class DataSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SyncDataAsync(dbContext, stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

---

## Generic Host Anti-Patterns

### Long-Running StartAsync

**Problem**: Blocking application startup in `IHostedService.StartAsync`.

```csharp
// BAD: Blocks entire application startup
public class WarmupService : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadEntireCacheAsync(); // Takes 30 seconds!
    }
}

// GOOD: Start work in background, return quickly
public class WarmupService(ILogger<WarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting cache warmup");
        await LoadEntireCacheAsync(stoppingToken);
        logger.LogInformation("Cache warmup complete");
    }
}
```

### Ignoring Application Lifetime Events

**Problem**: Not cleaning up resources during shutdown.

```csharp
// BAD: No graceful shutdown handling
public class WorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(); // May be interrupted mid-batch
        }
    }
}

// GOOD: Handle shutdown gracefully
public class WorkerService(
    IHostApplicationLifetime lifetime,
    ILogger<WorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStopping.Register(() =>
            logger.LogInformation("Shutdown requested, finishing current batch..."));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
        }

        logger.LogInformation("Graceful shutdown complete");
    }
}
```
