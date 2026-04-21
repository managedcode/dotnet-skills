# ASP.NET Core Anti-Patterns Reference

This document catalogs common mistakes and anti-patterns in ASP.NET Core development, with explanations of why they are problematic and how to fix them.

## Table of Contents

- [HttpClient Anti-Patterns](#httpclient-anti-patterns)
- [Async Anti-Patterns](#async-anti-patterns)
- [Configuration and Secrets Anti-Patterns](#configuration-and-secrets-anti-patterns)
- [Dependency Injection Anti-Patterns](#dependency-injection-anti-patterns)
- [Middleware Anti-Patterns](#middleware-anti-patterns)
- [Error Handling Anti-Patterns](#error-handling-anti-patterns)
- [Security Anti-Patterns](#security-anti-patterns)
- [Performance Anti-Patterns](#performance-anti-patterns)
- [Controller Anti-Patterns](#controller-anti-patterns)
- [Database and EF Core Anti-Patterns](#database-and-ef-core-anti-patterns)

---

## HttpClient Anti-Patterns

### Creating HttpClient Instances Directly

**Anti-Pattern:**
```csharp
public class WeatherService
{
    public async Task<Weather> GetWeatherAsync(string city)
    {
        using var client = new HttpClient(); // BAD: Creates new socket each time
        var response = await client.GetAsync($"https://api.weather.com/{city}");
        return await response.Content.ReadFromJsonAsync<Weather>();
    }
}
```

**Problem:** Creating `HttpClient` directly leads to socket exhaustion. Each `HttpClient` instance opens new TCP connections and disposing it does not immediately release sockets (they stay in TIME_WAIT state).

**Correct Approach:**
```csharp
// Registration
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com/");
});

// Service
public class WeatherService : IWeatherService
{
    private readonly HttpClient _client;

    public WeatherService(HttpClient client)
    {
        _client = client;
    }

    public async Task<Weather> GetWeatherAsync(string city)
    {
        var response = await _client.GetAsync(city);
        return await response.Content.ReadFromJsonAsync<Weather>();
    }
}
```

### Using Static HttpClient Without Respecting DNS Changes

**Anti-Pattern:**
```csharp
public static class ApiClient
{
    private static readonly HttpClient _client = new HttpClient
    {
        BaseAddress = new Uri("https://api.example.com/")
    };

    public static async Task<string> GetDataAsync()
    {
        return await _client.GetStringAsync("data");
    }
}
```

**Problem:** Static HttpClient instances cache DNS indefinitely. If the server's IP address changes, the client will continue to use the old IP.

**Correct Approach:**
```csharp
// Use IHttpClientFactory which handles DNS rotation
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5)); // Rotate handlers periodically
```

---

## Async Anti-Patterns

### Sync-Over-Async (Blocking on Async Code)

**Anti-Pattern:**
```csharp
public class UserController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetUser(int id)
    {
        // BAD: Blocking on async code
        var user = _userService.GetUserAsync(id).Result;
        // Or equally bad:
        var user2 = _userService.GetUserAsync(id).GetAwaiter().GetResult();
        // Or:
        var user3 = Task.Run(() => _userService.GetUserAsync(id)).Result;

        return Ok(user);
    }
}
```

**Problem:** Blocking on async code can cause deadlocks (especially with synchronization contexts) and wastes thread pool threads. The thread waits instead of being released to handle other requests.

**Correct Approach:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    var user = await _userService.GetUserAsync(id);
    return Ok(user);
}
```

### Async Void

**Anti-Pattern:**
```csharp
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async void Invoke(HttpContext context) // BAD: async void
    {
        await LogRequestAsync(context);
        await _next(context);
    }
}
```

**Problem:** `async void` methods cannot be awaited, exceptions cannot be caught, and if an exception occurs it will crash the process.

**Correct Approach:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    await LogRequestAsync(context);
    await _next(context);
}
```

### Not Using ConfigureAwait in Library Code

**Anti-Pattern (in library/shared code):**
```csharp
public class DataService
{
    public async Task<Data> GetDataAsync()
    {
        var result = await _repository.QueryAsync(); // May capture unnecessary context
        return ProcessData(result);
    }
}
```

**Problem:** In library code, not using `ConfigureAwait(false)` captures the synchronization context unnecessarily, which can cause performance issues and potential deadlocks when called from UI applications.

**Correct Approach (for library code):**
```csharp
public async Task<Data> GetDataAsync()
{
    var result = await _repository.QueryAsync().ConfigureAwait(false);
    return ProcessData(result);
}
```

Note: In ASP.NET Core application code (controllers, services directly serving requests), this is less critical since ASP.NET Core does not have a synchronization context.

### Async Over Sync

**Anti-Pattern:**
```csharp
public Task<int> CalculateAsync(int value)
{
    return Task.Run(() => Calculate(value)); // BAD: Wrapping sync in Task.Run
}

public Task SaveAsync(Data data)
{
    Save(data);
    return Task.CompletedTask; // BAD: Fake async
}
```

**Problem:** Wrapping synchronous code in `Task.Run` wastes thread pool threads. Returning `Task.CompletedTask` after sync work is misleading and provides no benefit.

**Correct Approach:**
```csharp
// Either keep it synchronous if there's nothing to await
public int Calculate(int value)
{
    return value * 2;
}

// Or use truly async operations
public async Task SaveAsync(Data data)
{
    await _dbContext.SaveChangesAsync();
}
```

---

## Configuration and Secrets Anti-Patterns

### Storing Secrets in appsettings.json

**Anti-Pattern:**
```json
{
  "ConnectionStrings": {
    "Database": "Server=prod.db.com;User=admin;Password=SuperSecret123!"
  },
  "Jwt": {
    "Key": "MySuperSecretKey12345"
  },
  "ThirdPartyApi": {
    "ApiKey": "sk-live-xxxxxxxxxxxxx"
  }
}
```

**Problem:** Secrets committed to source control are exposed to anyone with repository access. They persist in git history even if removed later.

**Correct Approach:**
```csharp
// Development: User Secrets
// In terminal: dotnet user-secrets set "Jwt:Key" "development-key"

// Production: Environment variables or secret stores
builder.Configuration
    .AddEnvironmentVariables()
    .AddAzureKeyVault(new Uri("https://myvault.vault.azure.net/"),
        new DefaultAzureCredential());

// appsettings.json should only contain non-sensitive defaults
{
  "ConnectionStrings": {
    "Database": "" // Placeholder, real value from env/vault
  }
}
```

### Hardcoding Configuration Values

**Anti-Pattern:**
```csharp
public class EmailService
{
    public async Task SendAsync(string to, string subject, string body)
    {
        using var client = new SmtpClient("smtp.company.com", 587); // BAD: Hardcoded
        client.Credentials = new NetworkCredential("noreply@company.com", "password123");
        // ...
    }
}
```

**Correct Approach:**
```csharp
public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        using var client = new SmtpClient(_settings.SmtpServer, _settings.Port);
        // ...
    }
}
```

### Reading Configuration in Constructor

**Anti-Pattern:**
```csharp
public class ReportService
{
    private readonly string _connectionString;

    public ReportService(IConfiguration configuration)
    {
        // BAD: Reading raw configuration in constructor
        _connectionString = configuration["ConnectionStrings:Reports"];
    }
}
```

**Problem:** Bypasses the options pattern, no validation, harder to test, service knows about configuration structure.

**Correct Approach:**
```csharp
public class ReportService
{
    private readonly ReportSettings _settings;

    public ReportService(IOptions<ReportSettings> options)
    {
        _settings = options.Value;
    }
}
```

---

## Dependency Injection Anti-Patterns

### Captive Dependency (Scoped in Singleton)

**Anti-Pattern:**
```csharp
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddScoped<IUserContext, HttpUserContext>();

public class CacheService : ICacheService
{
    private readonly IUserContext _userContext; // BAD: Scoped in singleton

    public CacheService(IUserContext userContext)
    {
        _userContext = userContext; // This instance will be captured forever
    }
}
```

**Problem:** A scoped service injected into a singleton is "captured" and reused for all requests, leading to stale data, wrong user context, and thread safety issues.

**Correct Approach:**
```csharp
public class CacheService : ICacheService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CacheService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        using var scope = _scopeFactory.CreateScope();
        var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
        // Use userContext safely within this scope
    }
}
```

### Service Locator Pattern

**Anti-Pattern:**
```csharp
public class OrderService
{
    private readonly IServiceProvider _serviceProvider;

    public OrderService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ProcessOrderAsync(Order order)
    {
        // BAD: Service locator hides dependencies
        var emailService = _serviceProvider.GetRequiredService<IEmailService>();
        var inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();
        var paymentService = _serviceProvider.GetRequiredService<IPaymentService>();
    }
}
```

**Problem:** Hides dependencies, makes testing harder, class can request any service at any time, breaks explicit dependency declaration.

**Correct Approach:**
```csharp
public class OrderService
{
    private readonly IEmailService _emailService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    public OrderService(
        IEmailService emailService,
        IInventoryService inventoryService,
        IPaymentService paymentService)
    {
        _emailService = emailService;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
    }
}
```

### Circular Dependencies

**Anti-Pattern:**
```csharp
public class ServiceA
{
    public ServiceA(ServiceB serviceB) { }
}

public class ServiceB
{
    public ServiceB(ServiceA serviceA) { } // BAD: Circular dependency
}
```

**Problem:** Container cannot resolve circular dependencies. This indicates a design problem.

**Correct Approach:**
```csharp
// Option 1: Extract shared logic into a third service
public class SharedService { }
public class ServiceA { public ServiceA(SharedService shared) { } }
public class ServiceB { public ServiceB(SharedService shared) { } }

// Option 2: Use lazy injection
public class ServiceA
{
    private readonly Lazy<ServiceB> _serviceB;
    public ServiceA(Lazy<ServiceB> serviceB) { _serviceB = serviceB; }
}

// Option 3: Use events/mediator pattern to decouple
```

---

## Middleware Anti-Patterns

### Wrong Middleware Order

**Anti-Pattern:**
```csharp
var app = builder.Build();

app.MapControllers();              // BAD: Endpoints before auth
app.UseAuthentication();
app.UseAuthorization();
app.UseRouting();                  // BAD: Routing after endpoints
app.UseExceptionHandler("/error"); // BAD: Exception handler last
```

**Problem:** Middleware order matters. Authentication before routing means route data is not available. Exception handler at the end will not catch exceptions from endpoints.

**Correct Approach:**
```csharp
app.UseExceptionHandler("/error"); // First - catches all exceptions
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();           // After routing
app.UseAuthorization();            // After authentication
app.MapControllers();              // Last
```

### Blocking Operations in Middleware

**Anti-Pattern:**
```csharp
public class ValidationMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // BAD: Synchronous blocking read
        using var reader = new StreamReader(context.Request.Body);
        var body = reader.ReadToEnd();

        // BAD: Blocking database call
        var isValid = _validator.Validate(body).Result;

        await _next(context);
    }
}
```

**Correct Approach:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    context.Request.EnableBuffering();
    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    context.Request.Body.Position = 0;

    var isValid = await _validator.ValidateAsync(body);

    await _next(context);
}
```

### Not Calling Next Unintentionally

**Anti-Pattern:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (SomeCondition(context))
    {
        await _next(context);
    }
    // BAD: If condition is false, pipeline stops silently
}
```

**Problem:** Pipeline stops without response, leading to hanging requests or empty responses.

**Correct Approach:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (!SomeCondition(context))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Validation failed" });
        return; // Explicit short-circuit with response
    }

    await _next(context);
}
```

---

## Error Handling Anti-Patterns

### Swallowing Exceptions

**Anti-Pattern:**
```csharp
public async Task<Order> GetOrderAsync(int id)
{
    try
    {
        return await _repository.GetByIdAsync(id);
    }
    catch (Exception)
    {
        return null; // BAD: Exception swallowed, bug hidden
    }
}
```

**Problem:** Hides bugs, makes debugging impossible, caller does not know something went wrong.

**Correct Approach:**
```csharp
public async Task<Order?> GetOrderAsync(int id)
{
    try
    {
        return await _repository.GetByIdAsync(id);
    }
    catch (SqlException ex) when (ex.Number == 2) // Specific exception
    {
        _logger.LogWarning(ex, "Database timeout while getting order {OrderId}", id);
        throw new ServiceUnavailableException("Database temporarily unavailable", ex);
    }
}
```

### Exposing Exception Details to Users

**Anti-Pattern:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    try
    {
        return Ok(await _userService.GetByIdAsync(id));
    }
    catch (Exception ex)
    {
        // BAD: Stack trace and internal details exposed
        return StatusCode(500, new
        {
            error = ex.Message,
            stackTrace = ex.StackTrace,
            innerException = ex.InnerException?.Message
        });
    }
}
```

**Problem:** Exposes internal system details, potential security vulnerability, helps attackers understand system internals.

**Correct Approach:**
```csharp
// Use global exception handler with environment-aware responses
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = _environment.IsDevelopment() ? exception.Message : null
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

### Catch-All Without Logging

**Anti-Pattern:**
```csharp
try
{
    await ProcessPaymentAsync(order);
}
catch (Exception)
{
    // BAD: No logging, no context
    throw;
}
```

**Correct Approach:**
```csharp
try
{
    await ProcessPaymentAsync(order);
}
catch (PaymentException ex)
{
    _logger.LogWarning(ex, "Payment failed for order {OrderId}: {Reason}",
        order.Id, ex.Reason);
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing payment for order {OrderId}",
        order.Id);
    throw;
}
```

---

## Security Anti-Patterns

### Missing HTTPS Redirection

**Anti-Pattern:**
```csharp
var app = builder.Build();
app.MapControllers(); // BAD: No HTTPS enforcement
app.Run();
```

**Correct Approach:**
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.MapControllers();
```

### Disabled CORS (Allow All)

**Anti-Pattern:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()    // BAD: Any site can access
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

**Correct Approach:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://admin.example.com")
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type")
              .AllowCredentials();
    });
});
```

### SQL Injection via String Concatenation

**Anti-Pattern:**
```csharp
public async Task<User> GetUserAsync(string username)
{
    // BAD: SQL injection vulnerability
    var sql = $"SELECT * FROM Users WHERE Username = '{username}'";
    return await _context.Users.FromSqlRaw(sql).FirstOrDefaultAsync();
}
```

**Correct Approach:**
```csharp
public async Task<User> GetUserAsync(string username)
{
    // Parameterized query
    return await _context.Users
        .FromSqlInterpolated($"SELECT * FROM Users WHERE Username = {username}")
        .FirstOrDefaultAsync();

    // Or use LINQ
    return await _context.Users
        .Where(u => u.Username == username)
        .FirstOrDefaultAsync();
}
```

### Missing Authorization on Sensitive Endpoints

**Anti-Pattern:**
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id) // BAD: No authorization
{
    await _userService.DeleteAsync(id);
    return NoContent();
}
```

**Correct Approach:**
```csharp
[Authorize(Policy = "AdminOnly")]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id)
{
    await _userService.DeleteAsync(id);
    return NoContent();
}
```

### Storing Passwords in Plain Text

**Anti-Pattern:**
```csharp
public async Task<User> CreateUserAsync(string email, string password)
{
    var user = new User
    {
        Email = email,
        Password = password // BAD: Plain text password
    };
    await _context.SaveChangesAsync();
    return user;
}
```

**Correct Approach:**
```csharp
// Use ASP.NET Core Identity or proper password hashing
public async Task<User> CreateUserAsync(string email, string password)
{
    var user = new User
    {
        Email = email,
        PasswordHash = _passwordHasher.HashPassword(null, password)
    };
    await _context.SaveChangesAsync();
    return user;
}
```

---

## Performance Anti-Patterns

### Not Using Async I/O

**Anti-Pattern:**
```csharp
[HttpGet]
public IActionResult GetReport()
{
    // BAD: Synchronous database call blocks thread
    var data = _context.Reports.ToList();
    var file = File.ReadAllBytes("template.pdf"); // BAD: Sync file I/O
    return Ok(GenerateReport(data, file));
}
```

**Correct Approach:**
```csharp
[HttpGet]
public async Task<IActionResult> GetReport()
{
    var data = await _context.Reports.ToListAsync();
    var file = await File.ReadAllBytesAsync("template.pdf");
    return Ok(GenerateReport(data, file));
}
```

### Loading Entire Collections into Memory

**Anti-Pattern:**
```csharp
public async Task<decimal> GetTotalRevenueAsync()
{
    // BAD: Loads all orders into memory
    var orders = await _context.Orders.ToListAsync();
    return orders.Sum(o => o.Total);
}
```

**Correct Approach:**
```csharp
public async Task<decimal> GetTotalRevenueAsync()
{
    // Computed in database
    return await _context.Orders.SumAsync(o => o.Total);
}
```

### N+1 Query Problem

**Anti-Pattern:**
```csharp
public async Task<List<OrderDto>> GetOrdersAsync()
{
    var orders = await _context.Orders.ToListAsync();

    return orders.Select(o => new OrderDto
    {
        Id = o.Id,
        // BAD: Each access triggers a separate query
        CustomerName = o.Customer.Name,
        Items = o.Items.Select(i => i.Name).ToList()
    }).ToList();
}
```

**Correct Approach:**
```csharp
public async Task<List<OrderDto>> GetOrdersAsync()
{
    return await _context.Orders
        .Include(o => o.Customer)
        .Include(o => o.Items)
        .Select(o => new OrderDto
        {
            Id = o.Id,
            CustomerName = o.Customer.Name,
            Items = o.Items.Select(i => i.Name).ToList()
        })
        .ToListAsync();
}
```

### Not Using Response Compression

**Anti-Pattern:**
```csharp
var app = builder.Build();
app.MapControllers(); // BAD: Large responses sent uncompressed
```

**Correct Approach:**
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

app.UseResponseCompression();
app.MapControllers();
```

---

## Controller Anti-Patterns

### Fat Controllers

**Anti-Pattern:**
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
{
    // BAD: Business logic in controller
    if (request.Items.Count == 0)
        return BadRequest("Order must have items");

    var customer = await _context.Customers.FindAsync(request.CustomerId);
    if (customer == null)
        return NotFound("Customer not found");

    var order = new Order { CustomerId = request.CustomerId };

    foreach (var item in request.Items)
    {
        var product = await _context.Products.FindAsync(item.ProductId);
        if (product == null)
            return BadRequest($"Product {item.ProductId} not found");

        if (product.Stock < item.Quantity)
            return BadRequest($"Insufficient stock for {product.Name}");

        product.Stock -= item.Quantity;
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = item.Quantity,
            Price = product.Price
        });
    }

    order.Total = order.Items.Sum(i => i.Price * i.Quantity);
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    await _emailService.SendOrderConfirmationAsync(customer.Email, order);

    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

**Correct Approach:**
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
{
    var result = await _orderService.CreateOrderAsync(request);

    return result.Match<IActionResult>(
        success => CreatedAtAction(nameof(GetOrder), new { id = success.OrderId }, success),
        notFound => NotFound(notFound.Message),
        validation => BadRequest(validation.Errors));
}
```

### Not Using Action Filters

**Anti-Pattern:**
```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateRequest request)
{
    // BAD: Manual validation in every action
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    // ...
}
```

**Correct Approach:**
```csharp
// Configure globally
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false; // Enable automatic validation
});

// Or use a filter
public class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(context.ModelState);
        }
    }
}
```

---

## Database and EF Core Anti-Patterns

### Using DbContext as Singleton

**Anti-Pattern:**
```csharp
builder.Services.AddSingleton<MyDbContext>(); // BAD: DbContext is not thread-safe
```

**Problem:** `DbContext` is not thread-safe and tracks entities. Using it as singleton causes concurrency issues and memory leaks.

**Correct Approach:**
```csharp
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString));
// Default is scoped lifetime
```

### Not Disposing DbContext (Manual Creation)

**Anti-Pattern:**
```csharp
public class ReportService
{
    private readonly MyDbContext _context = new MyDbContext(); // BAD: Never disposed
}
```

**Correct Approach:**
```csharp
public class ReportService
{
    private readonly MyDbContext _context;

    public ReportService(MyDbContext context)
    {
        _context = context; // Injected, container manages lifetime
    }
}
```

### Tracking Entities When Not Needed

**Anti-Pattern:**
```csharp
public async Task<List<ProductDto>> GetProductsAsync()
{
    // BAD: Tracking enabled for read-only query
    var products = await _context.Products.ToListAsync();
    return products.Select(p => new ProductDto { Name = p.Name }).ToList();
}
```

**Correct Approach:**
```csharp
public async Task<List<ProductDto>> GetProductsAsync()
{
    return await _context.Products
        .AsNoTracking()
        .Select(p => new ProductDto { Name = p.Name })
        .ToListAsync();
}
```

### Lazy Loading in Web Applications

**Anti-Pattern:**
```csharp
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseLazyLoadingProxies() // BAD: Hidden N+1 queries
           .UseSqlServer(connectionString));
```

**Problem:** Lazy loading causes unexpected database queries, N+1 problems, and makes it hard to track what data is being loaded.

**Correct Approach:**
```csharp
// Use explicit loading with Include/ThenInclude
var orders = await _context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
    .ToListAsync();

// Or use projection
var orderDtos = await _context.Orders
    .Select(o => new OrderDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name
    })
    .ToListAsync();
```
