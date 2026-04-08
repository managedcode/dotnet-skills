# ASP.NET Core Web API Patterns

## Controller Patterns

### Thin Controllers with Service Delegation

Controllers should map HTTP concerns to services, not implement business logic.

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

### Feature-Sliced Controllers

Group related endpoints by feature rather than by entity when it improves cohesion.

```csharp
[ApiController]
[Route("api/checkout")]
public class CheckoutController(
    ICartService cartService,
    IPaymentService paymentService,
    IOrderService orderService) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCart(CancellationToken ct)
    {
        var result = await cartService.ValidateCurrentCartAsync(ct);
        return result.IsValid ? Ok() : BadRequest(result.Errors);
    }

    [HttpPost("payment")]
    public async Task<IActionResult> ProcessPayment(
        PaymentRequest request,
        CancellationToken ct)
    {
        var result = await paymentService.ProcessAsync(request, ct);
        return result.Succeeded ? Ok(result.TransactionId) : BadRequest(result.Error);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteOrder(CancellationToken ct)
    {
        var order = await orderService.CreateFromCartAsync(ct);
        return CreatedAtRoute("GetOrder", new { id = order.Id }, order);
    }
}
```

### Base Controller for Shared Behavior

Use a base controller for cross-cutting concerns like user context extraction.

```csharp
[ApiController]
public abstract class ApiControllerBase(ICurrentUserService currentUser) : ControllerBase
{
    protected Guid UserId => currentUser.UserId
        ?? throw new UnauthorizedAccessException("User not authenticated");

    protected string? TenantId => currentUser.TenantId;

    protected IActionResult Problem(Error error) => error.Type switch
    {
        ErrorType.NotFound => NotFound(error.ToProblemDetails()),
        ErrorType.Validation => BadRequest(error.ToProblemDetails()),
        ErrorType.Conflict => Conflict(error.ToProblemDetails()),
        ErrorType.Forbidden => Forbid(),
        _ => StatusCode(500, error.ToProblemDetails())
    };
}
```

---

## Model Binding Patterns

### Binding from Multiple Sources

Combine route, query, header, and body binding explicitly.

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetWithOptions(
    [FromRoute] Guid id,
    [FromQuery] bool includeDeleted = false,
    [FromHeader(Name = "X-Correlation-Id")] string? correlationId = null,
    CancellationToken ct = default)
{
    // Route: id
    // Query: ?includeDeleted=true
    // Header: X-Correlation-Id
}

[HttpPost("{id:guid}/comments")]
public async Task<IActionResult> AddComment(
    [FromRoute] Guid id,
    [FromBody] CreateCommentRequest request,
    [FromServices] ICommentService commentService,
    CancellationToken ct)
{
    // Explicit binding sources for clarity
}
```

### Custom Model Binder for Complex Types

```csharp
public class CommaSeparatedArrayBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider
            .GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrEmpty(value))
        {
            bindingContext.Result = ModelBindingResult.Success(Array.Empty<string>());
            return Task.CompletedTask;
        }

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bindingContext.Result = ModelBindingResult.Success(values);
        return Task.CompletedTask;
    }
}

// Usage
[HttpGet]
public IActionResult Search(
    [ModelBinder(typeof(CommaSeparatedArrayBinder))] string[] tags)
{
    // GET /api/items?tags=csharp,dotnet,api
}
```

### Record DTOs with Required Members

```csharp
public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public record UpdateProductRequest
{
    public string? Name { get; init; }
    public decimal? Price { get; init; }
    public string? Description { get; init; }
}
```

---

## Validation Patterns

### FluentValidation Integration

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

// Registration
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
```

### Custom Validation Filter

```csharp
public class ValidateModelFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
            return;
        }

        await next();
    }
}
```

### Validation with Problem Details

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});

// Configure validation to return Problem Details
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

---

## API Versioning Patterns

### URL Path Versioning

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

[ApiController]
[Route("api/v{version:apiVersion}/products")]
[ApiVersion("2.0")]
public class ProductsV2Controller(IProductService productService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var product = await productService.GetEnrichedAsync(id, ct);
        return Ok(product); // Returns enhanced DTO
    }
}
```

### Header-Based Versioning

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
});
```

### Version Deprecation

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/legacy")]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
public class LegacyController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public IActionResult GetV1() => Ok("Deprecated endpoint");

    [HttpGet]
    [MapToApiVersion("2.0")]
    public IActionResult GetV2() => Ok("Current endpoint");
}
```

---

## Response Patterns

### Typed Response with TypedResults

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<Results<Ok<UserDto>, NotFound<ProblemDetails>>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct);

        if (user is null)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = $"No user exists with ID {id}",
                Status = StatusCodes.Status404NotFound
            });
        }

        return TypedResults.Ok(user);
    }
}
```

### Pagination Response Pattern

```csharp
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

[HttpGet]
[ProducesResponseType<PagedResponse<ProductDto>>(StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    var result = await productService.GetPagedAsync(page, pageSize, ct);

    Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
    Response.Headers.Append("X-Total-Pages", result.TotalPages.ToString());

    return Ok(result);
}
```

### Result Pattern Integration

```csharp
public class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(Error error) => Error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}

// Controller extension
public static class ControllerExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result) where T : class
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Error!.Type switch
        {
            ErrorType.NotFound => controller.NotFound(result.Error.ToProblemDetails()),
            ErrorType.Validation => controller.BadRequest(result.Error.ToProblemDetails()),
            ErrorType.Conflict => controller.Conflict(result.Error.ToProblemDetails()),
            _ => controller.StatusCode(500, result.Error.ToProblemDetails())
        };
    }
}
```

---

## Exception Handling Patterns

### Global Exception Handler

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
                Detail = validationEx.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },
            NotFoundException notFoundEx => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = notFoundEx.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

// Registration
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

app.UseExceptionHandler();
```

---

## Filter Patterns

### Action Filter for Logging

```csharp
public class LoggingActionFilter(ILogger<LoggingActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.DisplayName;
        var arguments = context.ActionArguments;

        logger.LogInformation(
            "Executing {Action} with arguments {@Arguments}",
            actionName,
            arguments);

        var stopwatch = Stopwatch.StartNew();
        var result = await next();
        stopwatch.Stop();

        if (result.Exception is not null)
        {
            logger.LogError(
                result.Exception,
                "Action {Action} failed after {ElapsedMs}ms",
                actionName,
                stopwatch.ElapsedMilliseconds);
        }
        else
        {
            logger.LogInformation(
                "Action {Action} completed in {ElapsedMs}ms",
                actionName,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

### Resource Filter for Caching

```csharp
public class ETagFilter : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var result = await next();

        if (result.Result is ObjectResult { Value: not null } objectResult)
        {
            var content = JsonSerializer.Serialize(objectResult.Value);
            var etag = $"\"{ComputeHash(content)}\"";

            context.HttpContext.Response.Headers.ETag = etag;

            if (context.HttpContext.Request.Headers.IfNoneMatch == etag)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            }
        }
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(bytes)[..22];
    }
}
```
