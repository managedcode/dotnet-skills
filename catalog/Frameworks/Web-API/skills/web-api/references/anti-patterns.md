# ASP.NET Core Web API Anti-Patterns

## Fat Controllers

### Problem: Business Logic in Controllers

Controllers should delegate to services, not implement business logic directly.

```csharp
// BAD: Controller contains business logic, data access, and validation
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        // Business logic embedded in controller
        if (request.Items.Count == 0)
            return BadRequest("Order must have items");

        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null)
            return BadRequest("Customer not found");

        decimal total = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null)
                return BadRequest($"Product {item.ProductId} not found");

            if (product.Stock < item.Quantity)
                return BadRequest($"Insufficient stock for {product.Name}");

            product.Stock -= item.Quantity;
            total += product.Price * item.Quantity;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        // Apply discount logic
        if (customer.IsPremium)
            total *= 0.9m;

        var order = new Order
        {
            CustomerId = customer.Id,
            Items = orderItems,
            Total = total,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
}
```

```csharp
// GOOD: Thin controller delegating to services
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request,
        CancellationToken ct)
    {
        var result = await orderService.CreateAsync(request, ct);

        return result.Match<IActionResult>(
            success => CreatedAtAction(nameof(GetOrder), new { id = success.Id }, success),
            failure => BadRequest(failure.ToProblemDetails()));
    }
}
```

---

## Inconsistent Error Responses

### Problem: Mixed Error Response Formats

```csharp
// BAD: Inconsistent error responses across endpoints
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    var item = await _service.GetAsync(id);
    if (item == null)
        return NotFound(); // Returns empty body
}

[HttpPost]
public async Task<IActionResult> Create(CreateRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState); // Returns ModelState dictionary
}

[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, UpdateRequest request)
{
    try
    {
        await _service.UpdateAsync(id, request);
        return Ok();
    }
    catch (NotFoundException)
    {
        return NotFound(new { error = "Item not found" }); // Returns anonymous object
    }
    catch (ValidationException ex)
    {
        return BadRequest(ex.Message); // Returns plain string
    }
}
```

```csharp
// GOOD: Consistent Problem Details responses
[ApiController]
[Route("api/[controller]")]
public class ItemsController(IItemService itemService) : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var item = await itemService.GetAsync(id, ct);

        if (item is null)
        {
            return Problem(
                detail: $"Item with ID {id} was not found",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<ItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateItemRequest request, CancellationToken ct)
    {
        // Validation handled automatically via [ApiController] and configured InvalidModelStateResponseFactory
        var item = await itemService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
}
```

---

## Missing Cancellation Token Support

### Problem: Ignoring CancellationToken

```csharp
// BAD: No cancellation token - wastes resources when client disconnects
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var items = await _repository.GetAllAsync(); // No CT
    var enriched = await _enrichmentService.EnrichAsync(items); // No CT
    return Ok(enriched);
}

[HttpPost("import")]
public async Task<IActionResult> Import(ImportRequest request)
{
    // Long-running operation with no cancellation support
    foreach (var item in request.Items)
    {
        await _service.ProcessAsync(item);
    }
    return Ok();
}
```

```csharp
// GOOD: Proper cancellation token propagation
[HttpGet]
public async Task<IActionResult> GetAll(CancellationToken ct)
{
    var items = await _repository.GetAllAsync(ct);
    var enriched = await _enrichmentService.EnrichAsync(items, ct);
    return Ok(enriched);
}

[HttpPost("import")]
public async Task<IActionResult> Import(
    ImportRequest request,
    CancellationToken ct)
{
    foreach (var item in request.Items)
    {
        ct.ThrowIfCancellationRequested();
        await _service.ProcessAsync(item, ct);
    }
    return Ok();
}
```

---

## Exposing Entity Models Directly

### Problem: Returning Database Entities as API Responses

```csharp
// BAD: Exposing EF entities directly
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    var user = await _context.Users
        .Include(u => u.Orders)
        .FirstOrDefaultAsync(u => u.Id == id);

    return Ok(user); // Exposes navigation properties, internal fields, circular references
}

// Entity with sensitive data
public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; } // Leaked!
    public string SecurityStamp { get; set; } // Leaked!
    public decimal InternalCreditScore { get; set; } // Leaked!
    public List<Order> Orders { get; set; } // Circular reference issues
}
```

```csharp
// GOOD: Using DTOs with explicit mapping
public record UserDto(
    int Id,
    string Email,
    string DisplayName,
    IReadOnlyList<OrderSummaryDto> RecentOrders);

public record OrderSummaryDto(
    int Id,
    DateTime CreatedAt,
    decimal Total);

[HttpGet("{id}")]
[ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
public async Task<IActionResult> Get(int id, CancellationToken ct)
{
    var user = await _userService.GetByIdAsync(id, ct);

    if (user is null)
        return NotFound();

    return Ok(user); // Returns UserDto, not entity
}
```

---

## Synchronous Blocking in Async Methods

### Problem: Blocking Calls in Async Context

```csharp
// BAD: Blocking calls that can cause thread pool starvation
[HttpGet]
public async Task<IActionResult> Get()
{
    var data = _httpClient.GetStringAsync("https://api.example.com/data").Result; // Blocks!
    var processed = Task.Run(() => ProcessData(data)).Result; // Blocks!

    Thread.Sleep(1000); // Blocks the thread

    return Ok(processed);
}

[HttpPost]
public async Task<IActionResult> Create(Request request)
{
    // Using .Wait() blocks the thread
    _backgroundService.ProcessAsync(request).Wait();
    return Ok();
}
```

```csharp
// GOOD: Fully async operations
[HttpGet]
public async Task<IActionResult> Get(CancellationToken ct)
{
    var data = await _httpClient.GetStringAsync("https://api.example.com/data", ct);
    var processed = await ProcessDataAsync(data, ct);

    await Task.Delay(1000, ct); // If delay is actually needed

    return Ok(processed);
}

[HttpPost]
public async Task<IActionResult> Create(Request request, CancellationToken ct)
{
    await _backgroundService.ProcessAsync(request, ct);
    return Ok();
}
```

---

## Over-Fetching Data

### Problem: Loading All Data When Only Some Is Needed

```csharp
// BAD: Loading entire entity graph when only ID and name needed
[HttpGet]
public async Task<IActionResult> GetProductNames()
{
    var products = await _context.Products
        .Include(p => p.Category)
        .Include(p => p.Supplier)
        .Include(p => p.Reviews)
        .Include(p => p.Images)
        .ToListAsync();

    return Ok(products.Select(p => new { p.Id, p.Name }));
}

// BAD: N+1 query pattern
[HttpGet]
public async Task<IActionResult> GetOrdersWithCustomers()
{
    var orders = await _context.Orders.ToListAsync();

    foreach (var order in orders)
    {
        order.Customer = await _context.Customers.FindAsync(order.CustomerId); // N+1!
    }

    return Ok(orders);
}
```

```csharp
// GOOD: Project only what you need
[HttpGet]
public async Task<IActionResult> GetProductNames(CancellationToken ct)
{
    var products = await _context.Products
        .Select(p => new ProductNameDto(p.Id, p.Name))
        .ToListAsync(ct);

    return Ok(products);
}

// GOOD: Eager load related data in single query
[HttpGet]
public async Task<IActionResult> GetOrdersWithCustomers(CancellationToken ct)
{
    var orders = await _context.Orders
        .Include(o => o.Customer)
        .Select(o => new OrderWithCustomerDto(
            o.Id,
            o.Total,
            o.Customer.Name))
        .ToListAsync(ct);

    return Ok(orders);
}
```

---

## Improper Dependency Injection Scopes

### Problem: Scope Mismatch and Captive Dependencies

```csharp
// BAD: Singleton capturing scoped service (captive dependency)
public class CacheService // Registered as Singleton
{
    private readonly AppDbContext _context; // Scoped! Will cause issues

    public CacheService(AppDbContext context)
    {
        _context = context; // Context disposed after first request
    }
}

// BAD: Creating services manually instead of using DI
[ApiController]
public class BadController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var service = new OrderService(new AppDbContext()); // Manual creation
        return Ok(service.GetOrders());
    }
}
```

```csharp
// GOOD: Proper scope management
public class CacheService(IServiceScopeFactory scopeFactory) // Singleton-safe
{
    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<AppDbContext, Task<T>> factory,
        CancellationToken ct)
    {
        // Create scope when needed
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await factory(context);
    }
}

// GOOD: Use DI properly
[ApiController]
public class GoodController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await orderService.GetOrdersAsync(ct));
    }
}
```

---

## Missing or Inconsistent Validation

### Problem: Incomplete or Scattered Validation

```csharp
// BAD: Validation scattered and inconsistent
[HttpPost]
public async Task<IActionResult> Create(CreateUserRequest request)
{
    // Some validation in controller
    if (string.IsNullOrEmpty(request.Email))
        return BadRequest("Email required");

    // Some in service
    var result = await _userService.CreateAsync(request); // Might throw or return error

    // Duplicate validation
    if (!IsValidEmail(request.Email))
        return BadRequest("Invalid email format");

    return Ok(result);
}

// BAD: No validation at all
[HttpPost]
public async Task<IActionResult> CreateUnsafe(CreateRequest request)
{
    // Trust the input blindly
    await _repository.InsertAsync(request.ToEntity());
    return Ok();
}
```

```csharp
// GOOD: Centralized validation with FluentValidation
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserRepository users)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MustAsync(async (email, ct) => !await users.EmailExistsAsync(email, ct))
            .WithMessage("Email already registered");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches("[0-9]").WithMessage("Password must contain digit");

        RuleFor(x => x.Age)
            .InclusiveBetween(18, 120);
    }
}

// Controller stays clean
[HttpPost]
[ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
{
    // Validation already performed by pipeline
    var user = await _userService.CreateAsync(request, ct);
    return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
}
```

---

## Swallowing Exceptions

### Problem: Catching and Hiding Errors

```csharp
// BAD: Swallowing exceptions silently
[HttpPost]
public async Task<IActionResult> Process(ProcessRequest request)
{
    try
    {
        await _service.ProcessAsync(request);
        return Ok();
    }
    catch (Exception)
    {
        // Swallowed - no logging, no indication of failure
        return Ok(); // Returns success even on failure!
    }
}

// BAD: Generic catch returning vague error
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    try
    {
        return Ok(await _service.GetAsync(id));
    }
    catch (Exception)
    {
        return StatusCode(500, "An error occurred"); // No details, no logging
    }
}
```

```csharp
// GOOD: Let global exception handler manage unexpected errors
[HttpPost]
public async Task<IActionResult> Process(ProcessRequest request, CancellationToken ct)
{
    // No try-catch for unexpected exceptions - let global handler deal with them
    await _service.ProcessAsync(request, ct);
    return Ok();
}

// GOOD: Handle expected exceptions explicitly, log and surface appropriately
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id, CancellationToken ct)
{
    try
    {
        return Ok(await _service.GetAsync(id, ct));
    }
    catch (EntityNotFoundException)
    {
        // Expected case - return 404
        return NotFound();
    }
    // Unexpected exceptions propagate to global handler
}
```

---

## Hardcoded Configuration

### Problem: Magic Strings and Hardcoded Values

```csharp
// BAD: Hardcoded configuration values
[HttpGet]
public async Task<IActionResult> GetExternal()
{
    var client = new HttpClient
    {
        BaseAddress = new Uri("https://api.example.com"), // Hardcoded
        Timeout = TimeSpan.FromSeconds(30) // Hardcoded
    };

    client.DefaultRequestHeaders.Add("X-Api-Key", "abc123secret"); // Hardcoded secret!

    var response = await client.GetAsync("/data");
    return Ok(await response.Content.ReadAsStringAsync());
}
```

```csharp
// GOOD: Configuration-driven approach
public class ExternalApiOptions
{
    public const string SectionName = "ExternalApi";

    public required string BaseUrl { get; init; }
    public required int TimeoutSeconds { get; init; }
}

// In Program.cs
builder.Services.Configure<ExternalApiOptions>(
    builder.Configuration.GetSection(ExternalApiOptions.SectionName));

builder.Services.AddHttpClient("ExternalApi", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// Controller
[HttpGet]
public async Task<IActionResult> GetExternal(
    [FromServices] IHttpClientFactory clientFactory,
    CancellationToken ct)
{
    var client = clientFactory.CreateClient("ExternalApi");
    var response = await client.GetAsync("/data", ct);
    return Ok(await response.Content.ReadAsStringAsync(ct));
}
```

---

## Missing OpenAPI Documentation

### Problem: Undocumented or Poorly Documented APIs

```csharp
// BAD: No response type documentation
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    var item = await _service.GetAsync(id);
    if (item == null) return NotFound();
    return Ok(item);
}

// BAD: Object return type loses type information
[HttpGet]
public async Task<object> GetAll()
{
    return await _service.GetAllAsync();
}
```

```csharp
// GOOD: Fully documented endpoint
/// <summary>
/// Retrieves a specific item by its unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the item.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The requested item.</returns>
/// <response code="200">Returns the requested item.</response>
/// <response code="404">If the item is not found.</response>
[HttpGet("{id}")]
[ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Get(int id, CancellationToken ct)
{
    var item = await _service.GetAsync(id, ct);

    if (item is null)
    {
        return Problem(
            detail: $"Item with ID {id} not found",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Ok(item);
}

/// <summary>
/// Retrieves all items with optional filtering.
/// </summary>
[HttpGet]
[ProducesResponseType<IReadOnlyList<ItemDto>>(StatusCodes.Status200OK)]
public async Task<IReadOnlyList<ItemDto>> GetAll(
    [FromQuery] string? category,
    CancellationToken ct)
{
    return await _service.GetAllAsync(category, ct);
}
```

---

## Ignoring HTTP Semantics

### Problem: Misusing HTTP Methods and Status Codes

```csharp
// BAD: GET with side effects
[HttpGet("send-email/{userId}")]
public async Task<IActionResult> SendEmail(int userId)
{
    await _emailService.SendWelcomeEmail(userId); // Side effect on GET!
    return Ok();
}

// BAD: Always returning 200 OK
[HttpPost]
public async Task<IActionResult> Create(CreateRequest request)
{
    var result = await _service.CreateAsync(request);
    return Ok(result); // Should be 201 Created
}

// BAD: Wrong status codes
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id)
{
    var exists = await _service.ExistsAsync(id);
    if (!exists)
        return Ok(); // Should be 404

    await _service.DeleteAsync(id);
    return Ok(new { message = "Deleted" }); // Should be 204 No Content
}
```

```csharp
// GOOD: Proper HTTP semantics
[HttpPost("users/{userId}/welcome-email")]
public async Task<IActionResult> SendWelcomeEmail(int userId, CancellationToken ct)
{
    await _emailService.SendWelcomeEmailAsync(userId, ct);
    return Accepted(); // 202 for async operations
}

[HttpPost]
[ProducesResponseType<ItemDto>(StatusCodes.Status201Created)]
public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct)
{
    var item = await _service.CreateAsync(request, ct);
    return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
}

[HttpDelete("{id}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Delete(int id, CancellationToken ct)
{
    var deleted = await _service.DeleteAsync(id, ct);

    if (!deleted)
        return NotFound();

    return NoContent();
}
```

---

## Tight Controller-to-Controller Coupling

### Problem: Controllers Calling Other Controllers

```csharp
// BAD: Controller calling another controller
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly OrdersController _ordersController;
    private readonly UsersController _usersController;

    public ReportsController(
        OrdersController ordersController,
        UsersController usersController)
    {
        _ordersController = ordersController;
        _usersController = usersController;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var orders = await _ordersController.GetAll() as OkObjectResult;
        var users = await _usersController.GetAll() as OkObjectResult;
        // Process and combine...
    }
}
```

```csharp
// GOOD: Controllers depend on shared services
[ApiController]
[Route("api/[controller]")]
public class ReportsController(
    IOrderService orderService,
    IUserService userService,
    IReportGenerator reportGenerator) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<ReportSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var orders = await orderService.GetAllAsync(ct);
        var users = await userService.GetAllAsync(ct);

        var summary = reportGenerator.GenerateSummary(orders, users);

        return Ok(summary);
    }
}
```
