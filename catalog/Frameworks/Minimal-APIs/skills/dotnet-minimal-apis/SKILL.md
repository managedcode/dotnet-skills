---
name: dotnet-minimal-apis
description: "Design and implement Minimal APIs in ASP.NET Core using handler-first endpoints, route groups, filters, and lightweight composition suited to modern .NET services."
compatibility: "Requires ASP.NET Core 6+, preferably .NET 8+ for full features."
---

# Minimal APIs

## Trigger On

- building new HTTP APIs in ASP.NET Core
- creating lightweight microservices
- choosing between Minimal APIs and controllers
- organizing endpoints with route groups
- implementing validation and filters

## Documentation

- [Minimal APIs Overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Minimal API Tutorial](https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0)
- [Filters in Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0)
- [OpenAPI Support](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [Route Groups](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0#route-groups)

### References

- [patterns.md](references/patterns.md) - detailed route groups, filters, TypedResults patterns, parameter binding, error handling, and testing
- [anti-patterns.md](references/anti-patterns.md) - common Minimal API mistakes to avoid

## When to Use Minimal APIs vs Controllers

| Use Minimal APIs | Use Controllers |
|------------------|-----------------|
| New projects | Existing MVC/API projects |
| Microservices | Complex model binding |
| Simple CRUD APIs | OData, JsonPatch |
| Lightweight handlers | Heavy use of attributes |
| .NET 8+ projects | Need `[ApiController]` features |

## Workflow

1. **Define endpoints directly in Program.cs** (for small APIs)
2. **Use route groups** for related endpoints
3. **Move handlers to separate classes** as the API grows
4. **Apply filters** for cross-cutting concerns
5. **Use TypedResults** for type-safe responses
6. **Generate OpenAPI docs** with `.WithOpenApi()`

## Basic Patterns

### Simple Endpoints
```csharp
var app = builder.Build();

app.MapGet("/", () => "Hello World");

app.MapGet("/products/{id}", (int id) => Results.Ok(new { Id = id }));

app.MapPost("/products", (Product product) => Results.Created($"/products/{product.Id}", product));
```

### TypedResults (Strongly-Typed)
```csharp
app.MapGet("/products/{id}", Results<Ok<Product>, NotFound> (int id, AppDb db) =>
{
    var product = db.Products.Find(id);
    return product is not null
        ? TypedResults.Ok(product)
        : TypedResults.NotFound();
});
```

### Dependency Injection
```csharp
app.MapGet("/products", async (IProductService service) =>
{
    return await service.GetAllAsync();
});

// Or with [FromServices] for clarity
app.MapGet("/products", async ([FromServices] IProductService service) =>
    await service.GetAllAsync());
```

## Route Groups

### Basic Grouping
```csharp
var products = app.MapGroup("/api/products");

products.MapGet("/", GetAll);
products.MapGet("/{id}", GetById);
products.MapPost("/", Create);
products.MapPut("/{id}", Update);
products.MapDelete("/{id}", Delete);
```

### Groups with Shared Configuration
```csharp
var api = app.MapGroup("/api")
    .RequireAuthorization()
    .AddEndpointFilter<ValidationFilter>();

var products = api.MapGroup("/products")
    .WithTags("Products");

var orders = api.MapGroup("/orders")
    .WithTags("Orders")
    .RequireAuthorization("AdminOnly");
```

## Endpoint Filters

### Inline Filter
```csharp
app.MapGet("/products/{id}", (int id) => Results.Ok(id))
    .AddEndpointFilter(async (context, next) =>
    {
        var id = context.GetArgument<int>(0);
        if (id <= 0)
            return Results.BadRequest("Invalid ID");

        return await next(context);
    });
```

### Class-Based Filter
```csharp
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments
            .OfType<T>()
            .FirstOrDefault();

        if (argument is null)
            return Results.BadRequest("Invalid request body");

        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();

        if (validator is not null)
        {
            var result = await validator.ValidateAsync(argument);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}

// Usage
products.MapPost("/", Create)
    .AddEndpointFilter<ValidationFilter<CreateProductRequest>>();
```

### Global Filters via Root Group
```csharp
// All endpoints inherit filters from root group
var root = app.MapGroup("")
    .AddEndpointFilter<LoggingFilter>()
    .AddEndpointFilter<ErrorHandlingFilter>();

root.MapGet("/health", () => Results.Ok());
root.MapGroup("/api/products").MapGet("/", GetProducts);
```

## Organizing Larger APIs

### Extension Method Pattern
```csharp
// ProductEndpoints.cs
public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products");

        group.MapGet("/", GetAll);
        group.MapGet("/{id}", GetById);
        group.MapPost("/", Create);

        return group;
    }

    private static async Task<Ok<List<Product>>> GetAll(IProductService service)
        => TypedResults.Ok(await service.GetAllAsync());

    private static async Task<Results<Ok<Product>, NotFound>> GetById(
        int id, IProductService service)
    {
        var product = await service.GetByIdAsync(id);
        return product is not null
            ? TypedResults.Ok(product)
            : TypedResults.NotFound();
    }

    private static async Task<Created<Product>> Create(
        CreateProductRequest request, IProductService service)
    {
        var product = await service.CreateAsync(request);
        return TypedResults.Created($"/api/products/{product.Id}", product);
    }
}

// Program.cs
app.MapProductEndpoints();
app.MapOrderEndpoints();
```

## Request/Response DTOs

```csharp
// Separate from domain models
public record CreateProductRequest(string Name, decimal Price);
public record UpdateProductRequest(string Name, decimal Price);
public record ProductResponse(int Id, string Name, decimal Price);

// Don't expose domain entities directly
app.MapPost("/products", (CreateProductRequest request, IMapper mapper) =>
{
    var product = mapper.Map<Product>(request);
    // ...
    return TypedResults.Created($"/products/{product.Id}",
        mapper.Map<ProductResponse>(product));
});
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Everything in Program.cs | Unmaintainable | Use extension methods |
| No route groups | Repetitive config | Group related endpoints |
| Manual validation | Error-prone | Use filters + FluentValidation |
| Exposing entities | Tight coupling | Use DTOs |
| No TypedResults | No compile-time checks | Use `TypedResults` |
| Ignoring OpenAPI | No documentation | Add `.WithOpenApi()` |

## OpenAPI Integration

```csharp
builder.Services.AddOpenApi();

app.MapOpenApi();  // Serves OpenAPI spec

app.MapGet("/products", GetProducts)
    .WithName("GetProducts")
    .WithSummary("Get all products")
    .WithDescription("Returns a list of all available products")
    .Produces<List<Product>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status500InternalServerError);
```

## Deliver

- clean, organized Minimal API endpoints
- proper use of route groups and filters
- type-safe responses with TypedResults
- OpenAPI documentation
- validation with endpoint filters

## Validate

- endpoints return correct status codes
- validation filters catch invalid input
- OpenAPI spec is accurate
- route groups share common configuration
- handlers are testable (can mock dependencies)
