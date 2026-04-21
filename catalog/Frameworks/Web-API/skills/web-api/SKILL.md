---
name: web-api
description: "Build or maintain controller-based ASP.NET Core APIs when the project needs controller conventions, advanced model binding, validation extensions, OData, JsonPatch, or existing API patterns."
compatibility: "Requires an ASP.NET Core API project that uses or should use controllers."
---

# ASP.NET Core Web API

## Trigger On

- working on controller-based APIs in ASP.NET Core
- needing controller-specific extensibility or conventions
- migrating or reviewing existing API controllers and filters

## Workflow

1. Use controllers when the API needs controller-centric features, not simply because older templates did so.
2. Keep controllers thin: map HTTP concerns to application services or handlers, and avoid embedding data access and business rules directly in actions.
3. Use clear DTO boundaries, explicit validation, and predictable HTTP status behavior.
4. Review authentication and authorization at both controller and endpoint levels so the API surface is not accidentally inconsistent.
5. Keep OpenAPI generation, versioning, and error contract behavior deliberate rather than incidental.
6. Use `minimal-apis` for new simple APIs instead of defaulting to controllers out of habit.

## Deliver

- controller APIs with explicit contracts and policies
- reduced controller bloat
- tests or smoke checks for critical API behavior

## Validate

- controller features are actually justified
- actions do not hide business logic and persistence details
- HTTP semantics stay predictable across endpoints

## Controller Structure

Use primary constructors (C# 12+) for dependency injection and keep controllers focused on HTTP concerns:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(
    IOrderService orderService,
    ILogger<OrdersController> logger) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await orderService.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await orderService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
```

## Model Binding

Explicitly declare binding sources for clarity:

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetWithOptions(
    [FromRoute] Guid id,
    [FromQuery] bool includeDeleted = false,
    [FromHeader(Name = "X-Correlation-Id")] string? correlationId = null,
    CancellationToken ct = default)
{
    // Route: id, Query: includeDeleted, Header: X-Correlation-Id
}
```

Use record types with required members for request DTOs:

```csharp
public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
```

## Validation

Prefer FluentValidation for complex validation rules:

```csharp
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(IProductRepository products)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must contain at least one item");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await products.ExistsAsync(id, ct))
                .WithMessage("Product does not exist");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .LessThanOrEqualTo(100);
        });
    }
}
```

Configure consistent Problem Details responses:

```csharp
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problemDetails);
    };
});
```

## API Versioning

Configure URL path versioning:

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

[ApiController]
[Route("api/v{version:apiVersion}/products")]
[ApiVersion("1.0")]
public class ProductsV1Controller(IProductService productService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var product = await productService.GetAsync(id, ct);
        return Ok(product);
    }
}
```

## Exception Handling

Use global exception handlers for consistent error responses:

```csharp
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = exception switch
        {
            ValidationException validationEx => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = validationEx.Message
            },
            NotFoundException notFoundEx => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = notFoundEx.Message
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error"
            }
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

## References

- [patterns.md](references/patterns.md) - Controller patterns, model binding, validation, versioning, response handling, and filter patterns
- [anti-patterns.md](references/anti-patterns.md) - Common API mistakes to avoid including fat controllers, inconsistent errors, missing cancellation tokens, and improper HTTP semantics
