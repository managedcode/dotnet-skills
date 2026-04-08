# Minimal API Anti-Patterns

## Structural Anti-Patterns

### Monolithic Program.cs

**Problem**: All endpoints defined directly in Program.cs becomes unmaintainable.

```csharp
// BAD: Everything in Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/products", async (AppDb db) => await db.Products.ToListAsync());
app.MapGet("/products/{id}", async (int id, AppDb db) => await db.Products.FindAsync(id));
app.MapPost("/products", async (Product p, AppDb db) => { db.Add(p); await db.SaveChangesAsync(); return p; });
// ... 50 more endpoints
app.MapGet("/orders", async (AppDb db) => await db.Orders.ToListAsync());
// ... 50 more endpoints
app.MapGet("/customers", async (AppDb db) => await db.Customers.ToListAsync());
// ... and so on

app.Run();
```

**Solution**: Use extension methods to organize endpoints by domain.

```csharp
// GOOD: Organized via extension methods
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapCustomerEndpoints();
```

### Flat Route Structure Without Groups

**Problem**: Repeating configuration across related endpoints.

```csharp
// BAD: No route groups, repetitive configuration
app.MapGet("/api/products", GetProducts)
    .RequireAuthorization()
    .WithTags("Products");

app.MapGet("/api/products/{id}", GetProductById)
    .RequireAuthorization()
    .WithTags("Products");

app.MapPost("/api/products", CreateProduct)
    .RequireAuthorization()
    .WithTags("Products")
    .AddEndpointFilter<ValidationFilter>();
```

**Solution**: Use route groups to share configuration.

```csharp
// GOOD: Shared configuration via groups
var products = app.MapGroup("/api/products")
    .RequireAuthorization()
    .WithTags("Products");

products.MapGet("/", GetProducts);
products.MapGet("/{id}", GetProductById);
products.MapPost("/", CreateProduct).AddEndpointFilter<ValidationFilter>();
```

## Handler Anti-Patterns

### Anonymous Lambda Handlers

**Problem**: Complex inline lambdas are hard to test and maintain.

```csharp
// BAD: Complex inline logic
app.MapPost("/products", async (CreateProductRequest request, AppDb db, IMapper mapper) =>
{
    if (string.IsNullOrEmpty(request.Name))
        return Results.BadRequest("Name required");
    if (request.Price <= 0)
        return Results.BadRequest("Price must be positive");
    if (await db.Products.AnyAsync(p => p.Sku == request.Sku))
        return Results.Conflict("SKU exists");

    var product = mapper.Map<Product>(request);
    product.CreatedAt = DateTime.UtcNow;
    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/products/{product.Id}", mapper.Map<ProductResponse>(product));
});
```

**Solution**: Extract to named methods or service classes.

```csharp
// GOOD: Extracted handler with validation filter
products.MapPost("/", CreateProduct)
    .AddEndpointFilter<ValidationFilter<CreateProductRequest>>();

private static async Task<Results<Created<ProductResponse>, Conflict<string>>> CreateProduct(
    CreateProductRequest request,
    IProductService service)
{
    var result = await service.CreateAsync(request);
    return result.Match<Results<Created<ProductResponse>, Conflict<string>>>(
        success => TypedResults.Created($"/products/{success.Id}", success),
        conflict => TypedResults.Conflict(conflict.Message));
}
```

### Mixing Concerns in Handlers

**Problem**: Handlers doing validation, mapping, business logic, and persistence.

```csharp
// BAD: Handler does everything
app.MapPut("/products/{id}", async (int id, UpdateProductRequest request, AppDb db) =>
{
    // Validation
    if (request.Price < 0) return Results.BadRequest("Invalid price");

    // Fetch
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    // Authorization
    if (product.OwnerId != GetCurrentUserId()) return Results.Forbid();

    // Mapping
    product.Name = request.Name;
    product.Price = request.Price;
    product.UpdatedAt = DateTime.UtcNow;

    // Persistence
    await db.SaveChangesAsync();

    // Response mapping
    return Results.Ok(new ProductResponse(product.Id, product.Name, product.Price));
});
```

**Solution**: Separate concerns using filters and services.

```csharp
// GOOD: Concerns separated
products.MapPut("/{id}", UpdateProduct)
    .AddEndpointFilter<ValidationFilter<UpdateProductRequest>>()
    .AddEndpointFilter<ProductOwnershipFilter>();

private static async Task<Results<Ok<ProductResponse>, NotFound>> UpdateProduct(
    int id,
    UpdateProductRequest request,
    IProductService service)
{
    var result = await service.UpdateAsync(id, request);
    return result.Match<Results<Ok<ProductResponse>, NotFound>>(
        success => TypedResults.Ok(success),
        _ => TypedResults.NotFound());
}
```

## Response Anti-Patterns

### Using Results Instead of TypedResults

**Problem**: `Results` factory methods lose compile-time type checking.

```csharp
// BAD: No compile-time checking of response types
app.MapGet("/products/{id}", async (int id, AppDb db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null
        ? Results.Ok(product)      // IResult, no type info
        : Results.NotFound();      // IResult, no type info
});
```

**Solution**: Use `TypedResults` with union return types.

```csharp
// GOOD: Compile-time checked response types
app.MapGet("/products/{id}", async Task<Results<Ok<ProductDto>, NotFound>> (int id, AppDb db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null
        ? TypedResults.Ok(product.ToDto())
        : TypedResults.NotFound();
});
```

### Exposing Domain Entities

**Problem**: Returning EF Core entities exposes internals and causes serialization issues.

```csharp
// BAD: Exposing domain entity
app.MapGet("/products/{id}", async (int id, AppDb db) =>
{
    return await db.Products
        .Include(p => p.Category)
        .Include(p => p.Supplier)
        .FirstOrDefaultAsync(p => p.Id == id);
    // Leaks navigation properties, internal IDs, circular references
});
```

**Solution**: Use DTOs/response records.

```csharp
// GOOD: Return DTO
app.MapGet("/products/{id}", async Task<Results<Ok<ProductResponse>, NotFound>>
    (int id, IProductService service) =>
{
    var dto = await service.GetByIdAsync(id);
    return dto is not null
        ? TypedResults.Ok(dto)
        : TypedResults.NotFound();
});

public record ProductResponse(
    int Id,
    string Name,
    decimal Price,
    string CategoryName,
    bool InStock);
```

### Inconsistent Status Codes

**Problem**: Endpoints return inconsistent status codes for similar operations.

```csharp
// BAD: Inconsistent responses
app.MapPost("/products", (Product p, AppDb db) =>
{
    db.Add(p);
    db.SaveChanges();
    return Results.Ok(p);  // Should be 201 Created
});

app.MapPost("/orders", (Order o, AppDb db) =>
{
    db.Add(o);
    db.SaveChanges();
    return Results.Json(o, statusCode: 200);  // Also wrong
});
```

**Solution**: Follow REST conventions consistently.

```csharp
// GOOD: Consistent REST responses
app.MapPost("/products", async Task<Created<ProductResponse>>
    (CreateProductRequest request, IProductService service) =>
{
    var product = await service.CreateAsync(request);
    return TypedResults.Created($"/products/{product.Id}", product);
});
```

## Validation Anti-Patterns

### Inline Validation in Handlers

**Problem**: Validation logic scattered across handlers, hard to maintain and test.

```csharp
// BAD: Manual validation in handler
app.MapPost("/products", (CreateProductRequest request, AppDb db) =>
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(request.Name))
        errors.Add("Name is required");
    if (request.Name?.Length > 100)
        errors.Add("Name must be 100 characters or less");
    if (request.Price <= 0)
        errors.Add("Price must be positive");
    if (string.IsNullOrWhiteSpace(request.Sku))
        errors.Add("SKU is required");
    if (!Regex.IsMatch(request.Sku ?? "", @"^[A-Z]{3}-\d{4}$"))
        errors.Add("SKU must match format XXX-0000");

    if (errors.Any())
        return Results.BadRequest(new { Errors = errors });

    // ... create product
});
```

**Solution**: Use validation filters with FluentValidation.

```csharp
// GOOD: Declarative validation
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Price)
            .GreaterThan(0);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .Matches(@"^[A-Z]{3}-\d{4}$")
            .WithMessage("SKU must match format XXX-0000");
    }
}

products.MapPost("/", Create)
    .AddEndpointFilter<ValidationFilter<CreateProductRequest>>();
```

### Not Returning Problem Details

**Problem**: Custom error formats break client expectations.

```csharp
// BAD: Non-standard error format
return Results.BadRequest(new
{
    success = false,
    error_code = "VALIDATION_ERROR",
    messages = errors
});
```

**Solution**: Use RFC 7807 Problem Details.

```csharp
// GOOD: Standard Problem Details
return TypedResults.ValidationProblem(
    errors.ToDictionary(e => e.PropertyName, e => new[] { e.ErrorMessage }),
    title: "Validation failed",
    detail: "One or more validation errors occurred");
```

## Dependency Injection Anti-Patterns

### Service Locator Pattern

**Problem**: Using HttpContext.RequestServices directly hides dependencies.

```csharp
// BAD: Service locator
app.MapGet("/products", (HttpContext context) =>
{
    var db = context.RequestServices.GetRequiredService<AppDb>();
    var logger = context.RequestServices.GetRequiredService<ILogger>();
    var cache = context.RequestServices.GetRequiredService<IDistributedCache>();

    // ... use services
});
```

**Solution**: Declare dependencies as parameters.

```csharp
// GOOD: Explicit dependencies
app.MapGet("/products", async (
    AppDb db,
    ILogger<ProductsEndpoints> logger,
    IDistributedCache cache) =>
{
    // Dependencies are clear and testable
});
```

### Over-Injection

**Problem**: Too many parameters indicate the handler does too much.

```csharp
// BAD: Too many dependencies
app.MapPost("/orders", async (
    CreateOrderRequest request,
    AppDb db,
    IMapper mapper,
    IInventoryService inventory,
    IPaymentService payment,
    IShippingService shipping,
    INotificationService notifications,
    ILogger<OrdersEndpoints> logger,
    IDistributedCache cache) =>
{
    // This handler orchestrates too much
});
```

**Solution**: Introduce a service to coordinate the operation.

```csharp
// GOOD: Single coordinating service
app.MapPost("/orders", async Task<Results<Created<OrderResponse>, BadRequest<ProblemDetails>>>
    (CreateOrderRequest request, IOrderService orderService) =>
{
    var result = await orderService.CreateAsync(request);
    return result.ToHttpResult();
});
```

## OpenAPI Anti-Patterns

### Missing Response Documentation

**Problem**: Endpoints without OpenAPI metadata have incomplete specs.

```csharp
// BAD: No OpenAPI metadata
app.MapGet("/products/{id}", GetProductById);
```

**Solution**: Add comprehensive OpenAPI metadata.

```csharp
// GOOD: Complete OpenAPI metadata
app.MapGet("/products/{id}", GetProductById)
    .WithName("GetProductById")
    .WithSummary("Get a product by ID")
    .Produces<ProductResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .WithOpenApi();
```

### Inconsistent Naming

**Problem**: Endpoint names don't follow conventions.

```csharp
// BAD: Inconsistent naming
app.MapGet("/products", GetAll).WithName("products_list");
app.MapGet("/products/{id}", GetById).WithName("GetProduct");
app.MapPost("/products", Create).WithName("create-product");
```

**Solution**: Follow consistent naming convention.

```csharp
// GOOD: Consistent PascalCase operation IDs
app.MapGet("/products", GetAll).WithName("GetProducts");
app.MapGet("/products/{id}", GetById).WithName("GetProductById");
app.MapPost("/products", Create).WithName("CreateProduct");
```

## Security Anti-Patterns

### Missing Authorization

**Problem**: Forgetting to protect sensitive endpoints.

```csharp
// BAD: No authorization
app.MapDelete("/products/{id}", DeleteProduct);
app.MapGet("/admin/users", GetAllUsers);
```

**Solution**: Apply authorization at group level or per-endpoint.

```csharp
// GOOD: Authorization applied
var api = app.MapGroup("/api")
    .RequireAuthorization();

var admin = app.MapGroup("/admin")
    .RequireAuthorization("AdminOnly");
```

### Logging Sensitive Data

**Problem**: Logging request bodies or headers that contain secrets.

```csharp
// BAD: Logs sensitive data
app.MapPost("/auth/login", async (LoginRequest request, ILogger logger) =>
{
    logger.LogInformation("Login attempt: {@Request}", request);
    // Logs password!
});
```

**Solution**: Exclude sensitive fields from logging.

```csharp
// GOOD: Exclude sensitive data
public record LoginRequest(
    string Username,
    [property: JsonIgnore] string Password);

// Or use destructuring carefully
logger.LogInformation("Login attempt for user {Username}", request.Username);
```

## Performance Anti-Patterns

### N+1 Queries

**Problem**: Lazy loading causes multiple database roundtrips.

```csharp
// BAD: N+1 queries
app.MapGet("/orders", async (AppDb db) =>
{
    var orders = await db.Orders.ToListAsync();
    return orders.Select(o => new OrderResponse(
        o.Id,
        o.Customer.Name,  // N additional queries!
        o.Items.Count     // N additional queries!
    ));
});
```

**Solution**: Eager load required data.

```csharp
// GOOD: Single query with includes
app.MapGet("/orders", async (AppDb db) =>
{
    var orders = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Items)
        .Select(o => new OrderResponse(
            o.Id,
            o.Customer.Name,
            o.Items.Count))
        .ToListAsync();

    return TypedResults.Ok(orders);
});
```

### No Cancellation Token Support

**Problem**: Long-running operations don't respect client disconnection.

```csharp
// BAD: No cancellation
app.MapGet("/reports/generate", async (IReportService service) =>
{
    var report = await service.GenerateLargeReportAsync();
    // Continues even if client disconnects
    return Results.Ok(report);
});
```

**Solution**: Accept and pass cancellation tokens.

```csharp
// GOOD: Cancellation token support
app.MapGet("/reports/generate", async (
    IReportService service,
    CancellationToken cancellationToken) =>
{
    var report = await service.GenerateLargeReportAsync(cancellationToken);
    return TypedResults.Ok(report);
});
```

### Blocking Calls

**Problem**: Synchronous operations block the thread pool.

```csharp
// BAD: Blocking calls
app.MapGet("/products", (AppDb db) =>
{
    var products = db.Products.ToList();  // Synchronous!
    Thread.Sleep(1000);  // Blocking!
    return Results.Ok(products);
});
```

**Solution**: Use async operations throughout.

```csharp
// GOOD: Fully async
app.MapGet("/products", async (AppDb db) =>
{
    await Task.Delay(1000);  // If delay needed
    var products = await db.Products.ToListAsync();
    return TypedResults.Ok(products);
});
```
