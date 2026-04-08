# Minimal API Patterns

## Route Groups

### Hierarchical Route Groups

Build nested groups for complex API structures:

```csharp
var api = app.MapGroup("/api/v1")
    .RequireAuthorization();

var products = api.MapGroup("/products")
    .WithTags("Products");

var productReviews = products.MapGroup("/{productId:int}/reviews")
    .WithTags("Product Reviews");

productReviews.MapGet("/", GetReviewsForProduct);
productReviews.MapPost("/", AddReviewToProduct);
productReviews.MapGet("/{reviewId:int}", GetReviewById);
```

### Group with Parameter Validation

Apply route constraints at the group level:

```csharp
var products = app.MapGroup("/api/products/{productId:int:min(1)}")
    .AddEndpointFilter(async (context, next) =>
    {
        var productId = context.GetArgument<int>(0);
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDb>();

        if (!await db.Products.AnyAsync(p => p.Id == productId))
            return TypedResults.NotFound();

        return await next(context);
    });

products.MapGet("/", (int productId) => ...);
products.MapGet("/variants", (int productId) => ...);
```

### Versioned API Groups

```csharp
var v1 = app.MapGroup("/api/v1").WithGroupName("v1");
var v2 = app.MapGroup("/api/v2").WithGroupName("v2");

v1.MapGet("/products", GetProductsV1);
v2.MapGet("/products", GetProductsV2);
```

## Endpoint Filters

### Validation Filter with FluentValidation

```csharp
public class FluentValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();

        if (argument is null)
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Missing request body",
                Status = StatusCodes.Status400BadRequest
            });

        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();

        if (validator is not null)
        {
            var result = await validator.ValidateAsync(argument);
            if (!result.IsValid)
            {
                return TypedResults.ValidationProblem(
                    result.ToDictionary(),
                    title: "Validation failed");
            }
        }

        return await next(context);
    }
}
```

### Logging Filter

```csharp
public class RequestLoggingFilter(ILogger<RequestLoggingFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var sw = Stopwatch.StartNew();
        var path = context.HttpContext.Request.Path;
        var method = context.HttpContext.Request.Method;

        logger.LogInformation("Request {Method} {Path} started", method, path);

        try
        {
            var result = await next(context);
            sw.Stop();

            logger.LogInformation(
                "Request {Method} {Path} completed in {ElapsedMs}ms",
                method, path, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Request {Method} {Path} failed after {ElapsedMs}ms",
                method, path, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Rate Limiting Filter

```csharp
public class RateLimitingFilter(IRateLimiter limiter) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var clientId = context.HttpContext.User.FindFirst("sub")?.Value
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        if (!await limiter.TryAcquireAsync(clientId))
        {
            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return await next(context);
    }
}
```

### Idempotency Filter

```csharp
public class IdempotencyFilter(IDistributedCache cache) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(idempotencyKey))
            return await next(context);

        var cacheKey = $"idempotency:{idempotencyKey}";
        var cachedResponse = await cache.GetStringAsync(cacheKey);

        if (cachedResponse is not null)
            return TypedResults.Content(cachedResponse, "application/json");

        var result = await next(context);

        if (result is IValueHttpResult httpResult)
        {
            var json = JsonSerializer.Serialize(httpResult.Value);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
        }

        return result;
    }
}
```

### Filter Execution Order

Filters execute in registration order (first registered runs first on request, last on response):

```csharp
app.MapPost("/products", Create)
    .AddEndpointFilter<LoggingFilter>()       // 1st on request, 3rd on response
    .AddEndpointFilter<AuthorizationFilter>() // 2nd on request, 2nd on response
    .AddEndpointFilter<ValidationFilter>();   // 3rd on request, 1st on response
```

## TypedResults Patterns

### Union Return Types

Use `Results<T1, T2, ...>` for compile-time checked multiple response types:

```csharp
app.MapGet("/products/{id}", async Task<Results<Ok<ProductDto>, NotFound, BadRequest<ProblemDetails>>>
    (int id, IProductService service) =>
{
    if (id <= 0)
        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Invalid ID",
            Detail = "Product ID must be positive"
        });

    var product = await service.GetByIdAsync(id);

    return product is not null
        ? TypedResults.Ok(product)
        : TypedResults.NotFound();
});
```

### Complex Result Patterns

```csharp
// Paginated results
app.MapGet("/products", async Task<Ok<PagedResult<ProductDto>>>
    ([AsParameters] PaginationQuery query, IProductService service) =>
{
    var result = await service.GetPagedAsync(query.Page, query.PageSize);
    return TypedResults.Ok(result);
});

public record PaginationQuery(int Page = 1, int PageSize = 20);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### File Results

```csharp
app.MapGet("/products/{id}/export", async Task<Results<FileStreamHttpResult, NotFound>>
    (int id, IProductService service) =>
{
    var product = await service.GetByIdAsync(id);

    if (product is null)
        return TypedResults.NotFound();

    var stream = await service.ExportToCsvAsync(product);
    return TypedResults.File(stream, "text/csv", $"product-{id}.csv");
});
```

### Accepted with Location

```csharp
app.MapPost("/products/import", async Task<Accepted<ImportJobResponse>>
    (ImportRequest request, IImportService service) =>
{
    var jobId = await service.StartImportAsync(request);
    return TypedResults.Accepted(
        $"/jobs/{jobId}",
        new ImportJobResponse(jobId, "Processing"));
});
```

## Parameter Binding

### [AsParameters] for Complex Queries

```csharp
public record ProductSearchQuery(
    string? Name,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Category,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "name",
    bool Descending = false);

app.MapGet("/products/search", async Task<Ok<PagedResult<ProductDto>>>
    ([AsParameters] ProductSearchQuery query, IProductService service) =>
{
    var result = await service.SearchAsync(query);
    return TypedResults.Ok(result);
});
```

### Header and Query Binding

```csharp
app.MapGet("/products", async Task<Ok<List<ProductDto>>>
    ([FromHeader(Name = "X-Tenant-Id")] string tenantId,
     [FromQuery] string? category,
     IProductService service) =>
{
    var products = await service.GetByTenantAsync(tenantId, category);
    return TypedResults.Ok(products);
});
```

### Custom Model Binding

```csharp
public record DateRange(DateOnly Start, DateOnly End) : IParsable<DateRange>
{
    public static DateRange Parse(string s, IFormatProvider? provider)
    {
        var parts = s.Split("..");
        return new DateRange(DateOnly.Parse(parts[0]), DateOnly.Parse(parts[1]));
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out DateRange result)
    {
        result = default!;
        if (string.IsNullOrEmpty(s)) return false;

        var parts = s.Split("..");
        if (parts.Length != 2) return false;

        if (!DateOnly.TryParse(parts[0], out var start)) return false;
        if (!DateOnly.TryParse(parts[1], out var end)) return false;

        result = new DateRange(start, end);
        return true;
    }
}

// Usage: /orders?dateRange=2024-01-01..2024-12-31
app.MapGet("/orders", (DateRange dateRange) => ...);
```

## Error Handling

### Global Exception Handler

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";

        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var problem = exception switch
        {
            ValidationException vex => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage))
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred"
            }
        };

        context.Response.StatusCode = problem.Status ?? 500;
        await context.Response.WriteAsJsonAsync(problem);
    });
});
```

### Result Pattern Integration

```csharp
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : result.Error switch
            {
                NotFoundError => TypedResults.NotFound(),
                ValidationError ve => TypedResults.ValidationProblem(ve.Errors),
                ConflictError ce => TypedResults.Conflict(ce.Message),
                _ => TypedResults.Problem(result.Error.Message)
            };
}

app.MapGet("/products/{id}", async (int id, IProductService service) =>
{
    var result = await service.GetByIdAsync(id);
    return result.ToHttpResult();
});
```

## OpenAPI Enhancements

### Rich Metadata

```csharp
app.MapGet("/products/{id}", GetProductById)
    .WithName("GetProductById")
    .WithSummary("Get a product by ID")
    .WithDescription("Returns detailed information about a specific product including pricing and availability")
    .Produces<ProductDto>(StatusCodes.Status200OK, "application/json")
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesValidationProblem()
    .WithOpenApi(operation =>
    {
        operation.Parameters[0].Description = "The unique product identifier";
        operation.Parameters[0].Example = new OpenApiInteger(42);
        return operation;
    });
```

### Request/Response Examples

```csharp
app.MapPost("/products", CreateProduct)
    .WithOpenApi(operation =>
    {
        operation.RequestBody.Content["application/json"].Example = new OpenApiObject
        {
            ["name"] = new OpenApiString("Widget Pro"),
            ["price"] = new OpenApiDouble(29.99),
            ["category"] = new OpenApiString("Electronics")
        };
        return operation;
    });
```

## Testing Patterns

### WebApplicationFactory Setup

```csharp
public class MinimalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MinimalApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IProductService, MockProductService>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.Should().NotBeEmpty();
    }
}
```

### Testing Filters in Isolation

```csharp
[Fact]
public async Task ValidationFilter_InvalidInput_ReturnsBadRequest()
{
    var filter = new FluentValidationFilter<CreateProductRequest>();

    var httpContext = new DefaultHttpContext();
    httpContext.RequestServices = new ServiceCollection()
        .AddScoped<IValidator<CreateProductRequest>, CreateProductValidator>()
        .BuildServiceProvider();

    var context = new EndpointFilterInvocationContext(
        httpContext,
        new object[] { new CreateProductRequest("", -1) });

    var result = await filter.InvokeAsync(context, _ =>
        ValueTask.FromResult<object?>(TypedResults.Ok()));

    result.Should().BeOfType<ValidationProblem>();
}
```
