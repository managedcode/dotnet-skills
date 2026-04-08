# Microsoft.Extensions Patterns

## Dependency Injection Patterns

### Constructor Injection with Primary Constructors

Primary constructors (C# 12+) provide a concise way to declare dependencies:

```csharp
public class OrderService(
    IOrderRepository repository,
    ILogger<OrderService> logger,
    IOptions<OrderSettings> options)
{
    public async Task<Order> GetOrderAsync(int id)
    {
        logger.LogDebug("Fetching order {OrderId}", id);
        return await repository.GetByIdAsync(id);
    }
}
```

### Service Registration Extensions

Encapsulate related registrations in extension methods:

```csharp
public static class OrderingServiceExtensions
{
    public static IServiceCollection AddOrdering(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OrderSettings>(configuration.GetSection("Ordering"));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
```

Usage in `Program.cs`:

```csharp
builder.Services.AddOrdering(builder.Configuration);
```

### Keyed Services (C# 12+ / .NET 8+)

Register and resolve multiple implementations by key:

```csharp
// Registration
services.AddKeyedScoped<IPaymentProcessor, StripeProcessor>("stripe");
services.AddKeyedScoped<IPaymentProcessor, PayPalProcessor>("paypal");

// Injection via primary constructor
public class CheckoutService(
    [FromKeyedServices("stripe")] IPaymentProcessor stripeProcessor,
    [FromKeyedServices("paypal")] IPaymentProcessor paypalProcessor)
{
    public Task ProcessAsync(string provider, decimal amount) =>
        provider switch
        {
            "stripe" => stripeProcessor.ChargeAsync(amount),
            "paypal" => paypalProcessor.ChargeAsync(amount),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
}
```

### Factory Pattern with DI

When you need runtime parameters to create services:

```csharp
public interface IConnectionFactory
{
    IConnection Create(string connectionString);
}

public class ConnectionFactory(ILogger<ConnectionFactory> logger) : IConnectionFactory
{
    public IConnection Create(string connectionString)
    {
        logger.LogDebug("Creating connection to {ConnectionString}", connectionString);
        return new Connection(connectionString);
    }
}
```

### Decorator Pattern

Wrap existing services with additional behavior:

```csharp
public class CachingOrderRepository(
    IOrderRepository inner,
    IMemoryCache cache,
    ILogger<CachingOrderRepository> logger) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(int id)
    {
        var cacheKey = $"order:{id}";
        if (cache.TryGetValue(cacheKey, out Order? cached))
        {
            logger.LogDebug("Cache hit for order {OrderId}", id);
            return cached;
        }

        var order = await inner.GetByIdAsync(id);
        if (order is not null)
        {
            cache.Set(cacheKey, order, TimeSpan.FromMinutes(5));
        }
        return order;
    }
}
```

Registration with Scrutor or manual:

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
services.Decorate<IOrderRepository, CachingOrderRepository>();
```

---

## Configuration Patterns

### Strongly Typed Configuration

Define a POCO for your settings:

```csharp
public class EmailSettings
{
    public const string SectionName = "Email";

    public required string SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public required string FromAddress { get; init; }
    public bool UseSsl { get; init; } = true;
}
```

Register and bind:

```csharp
services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
```

### Configuration Validation at Startup

Use `ValidateDataAnnotations` or `ValidateOnStart` to fail fast:

```csharp
public class DatabaseSettings
{
    [Required]
    public required string ConnectionString { get; init; }

    [Range(1, 100)]
    public int MaxPoolSize { get; init; } = 10;
}

// Registration with validation
services.AddOptions<DatabaseSettings>()
    .BindConfiguration("Database")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Custom Validation Logic

```csharp
services.AddOptions<ApiSettings>()
    .BindConfiguration("Api")
    .Validate(settings =>
    {
        if (string.IsNullOrEmpty(settings.BaseUrl))
            return false;
        return Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _);
    }, "BaseUrl must be a valid absolute URI")
    .ValidateOnStart();
```

### Environment-Specific Configuration

Standard configuration layering:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Already loaded by CreateApplicationBuilder:
// 1. appsettings.json
// 2. appsettings.{Environment}.json
// 3. Environment variables
// 4. Command-line args

// Add additional sources if needed
builder.Configuration.AddJsonFile("secrets.json", optional: true);
```

### Configuration Sections Binding

Bind nested sections:

```csharp
public class AppSettings
{
    public required DatabaseSettings Database { get; init; }
    public required EmailSettings Email { get; init; }
    public required CacheSettings Cache { get; init; }
}

// Bind entire hierarchy
var settings = configuration.Get<AppSettings>();
```

---

## Options Pattern

### IOptions vs IOptionsSnapshot vs IOptionsMonitor

| Interface | Lifetime | Reloads | Use Case |
|-----------|----------|---------|----------|
| `IOptions<T>` | Singleton | No | Static configuration |
| `IOptionsSnapshot<T>` | Scoped | Per request | Web apps with reloadable config |
| `IOptionsMonitor<T>` | Singleton | Yes (notifications) | Long-running services |

### Using IOptionsMonitor for Live Updates

```csharp
public class FeatureFlagService(IOptionsMonitor<FeatureFlags> optionsMonitor)
{
    public bool IsEnabled(string featureName) =>
        optionsMonitor.CurrentValue.EnabledFeatures.Contains(featureName);

    public IDisposable OnChange(Action<FeatureFlags> listener) =>
        optionsMonitor.OnChange(listener);
}
```

### Named Options

Configure multiple instances of the same type:

```csharp
services.Configure<StorageOptions>("blob", configuration.GetSection("Storage:Blob"));
services.Configure<StorageOptions>("table", configuration.GetSection("Storage:Table"));

// Injection
public class StorageService(IOptionsSnapshot<StorageOptions> options)
{
    public string GetBlobConnectionString() =>
        options.Get("blob").ConnectionString;

    public string GetTableConnectionString() =>
        options.Get("table").ConnectionString;
}
```

### Post-Configure

Apply transformations after binding:

```csharp
services.PostConfigure<ApiSettings>(settings =>
{
    settings.BaseUrl = settings.BaseUrl.TrimEnd('/');
});
```

---

## Logging Patterns

### Structured Logging

Use message templates with named placeholders:

```csharp
public class PaymentService(ILogger<PaymentService> logger)
{
    public async Task ProcessPaymentAsync(Payment payment)
    {
        logger.LogInformation(
            "Processing payment {PaymentId} for {Amount:C} from {CustomerId}",
            payment.Id, payment.Amount, payment.CustomerId);

        try
        {
            await ProcessAsync(payment);
            logger.LogInformation("Payment {PaymentId} processed successfully", payment.Id);
        }
        catch (PaymentException ex)
        {
            logger.LogError(ex,
                "Payment {PaymentId} failed: {ErrorCode}",
                payment.Id, ex.ErrorCode);
            throw;
        }
    }
}
```

### High-Performance Logging with Source Generators

Define log messages at compile time:

```csharp
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing order {OrderId} with {ItemCount} items")]
    public static partial void ProcessingOrder(ILogger logger, int orderId, int itemCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Order {OrderId} processing failed")]
    public static partial void OrderProcessingFailed(ILogger logger, int orderId, Exception exception);
}

// Usage
public class OrderProcessor(ILogger<OrderProcessor> logger)
{
    public void Process(Order order)
    {
        Log.ProcessingOrder(logger, order.Id, order.Items.Count);
    }
}
```

### Logging Scopes

Add contextual data to all logs within a scope:

```csharp
public class RequestProcessor(ILogger<RequestProcessor> logger)
{
    public async Task ProcessAsync(Request request)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = request.Id,
            ["UserId"] = request.UserId,
            ["CorrelationId"] = request.CorrelationId
        }))
        {
            logger.LogInformation("Starting request processing");
            await DoWorkAsync(request);
            logger.LogInformation("Request processing completed");
        }
    }
}
```

### Category-Based Filtering

Configure log levels per category in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "MyApp.DataAccess": "Debug",
      "MyApp.Services": "Information"
    }
  }
}
```

---

## HttpClientFactory Patterns

### Typed Clients

```csharp
public class GitHubClient(HttpClient httpClient, ILogger<GitHubClient> logger)
{
    public async Task<User?> GetUserAsync(string username)
    {
        logger.LogDebug("Fetching GitHub user {Username}", username);
        return await httpClient.GetFromJsonAsync<User>($"users/{username}");
    }
}

// Registration
services.AddHttpClient<GitHubClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
});
```

### Resilience with Polly

```csharp
services.AddHttpClient<ExternalApiClient>()
    .AddStandardResilienceHandler();

// Or custom policies
services.AddHttpClient<ExternalApiClient>()
    .AddResilienceHandler("custom", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential
        });
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 10
        });
    });
```

### Named Clients

```csharp
services.AddHttpClient("github", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
});

// Usage via factory
public class MultiApiService(IHttpClientFactory clientFactory)
{
    public async Task<string> GetDataAsync()
    {
        var client = clientFactory.CreateClient("github");
        return await client.GetStringAsync("zen");
    }
}
```

---

## Hosted Service Patterns

### Background Worker

```csharp
public class QueueProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<QueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queue processor starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<IMessageQueue>();

            if (await queue.TryDequeueAsync(stoppingToken) is { } message)
            {
                await ProcessMessageAsync(message, scope.ServiceProvider, stoppingToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
```

### Timed Background Service

```csharp
public class HealthCheckService(
    ILogger<HealthCheckService> logger,
    IOptions<HealthCheckOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed");
            }
        }
    }
}
```

---

## Generic Host Patterns

### Minimal Console App

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<WorkerService>();
builder.Services.AddOrdering(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
```

### Graceful Shutdown

```csharp
public class GracefulWorker(
    IHostApplicationLifetime lifetime,
    ILogger<GracefulWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
            logger.LogInformation("Application started"));

        lifetime.ApplicationStopping.Register(() =>
            logger.LogInformation("Application stopping, draining work..."));

        // Work loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
        }
    }
}
```
