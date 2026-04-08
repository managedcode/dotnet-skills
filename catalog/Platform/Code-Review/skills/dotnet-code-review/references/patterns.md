# .NET Code Review Patterns

This reference covers common patterns and anti-patterns for async code, resource disposal, and security in .NET code reviews.

---

## Async Patterns

### Pattern: Async All the Way

Async should propagate through the entire call chain. Blocking on async code causes deadlocks and thread pool starvation.

```csharp
// GOOD: Async propagates through the call chain
public class OrderService(IOrderRepository repository, IPaymentGateway paymentGateway)
{
    public async Task<OrderResult> ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var validated = await ValidateOrderAsync(order, cancellationToken);
        var payment = await paymentGateway.ChargeAsync(order.Total, cancellationToken);
        return await repository.SaveOrderAsync(validated, payment, cancellationToken);
    }
}

// BAD: Blocking on async - causes deadlocks in UI/ASP.NET contexts
public class OrderService(IOrderRepository repository, IPaymentGateway paymentGateway)
{
    public OrderResult ProcessOrder(Order order)
    {
        // NEVER DO THIS - deadlock risk
        var validated = ValidateOrderAsync(order).Result;
        var payment = paymentGateway.ChargeAsync(order.Total).GetAwaiter().GetResult();
        return repository.SaveOrderAsync(validated, payment).Wait();
    }
}
```

### Pattern: Cancellation Token Propagation

Always accept and propagate `CancellationToken` to support cooperative cancellation.

```csharp
// GOOD: CancellationToken flows through all async operations
public class DataProcessor(HttpClient httpClient, ILogger<DataProcessor> logger)
{
    public async Task<ProcessedData> FetchAndProcessAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return await ProcessContentAsync(content, cancellationToken);
    }

    private async Task<ProcessedData> ProcessContentAsync(
        string content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Processing logic...
        return await Task.FromResult(new ProcessedData(content));
    }
}

// BAD: Ignoring cancellation
public async Task<ProcessedData> FetchAndProcessAsync(string url)
{
    // No way to cancel this operation
    var response = await httpClient.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    return await ProcessContentAsync(content);
}
```

### Pattern: ConfigureAwait in Libraries

Library code should use `ConfigureAwait(false)` to avoid capturing synchronization context.

```csharp
// GOOD: Library code avoids context capture
public class CacheService(IDistributedCache cache)
{
    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        var cached = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<T>(cached);
        }

        var value = await factory().ConfigureAwait(false);
        var json = JsonSerializer.Serialize(value);
        await cache.SetStringAsync(key, json, cancellationToken).ConfigureAwait(false);
        return value;
    }
}
```

### Pattern: Proper Exception Handling in Async

Handle exceptions at appropriate boundaries without losing stack traces.

```csharp
// GOOD: Proper async exception handling
public class RetryingService(IExternalApi api, ILogger<RetryingService> logger)
{
    public async Task<Result> ExecuteWithRetryAsync(
        Request request,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await api.CallAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                attempt++;
                logger.LogWarning(ex, "Attempt {Attempt} failed, retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Operation cancelled");
                throw; // Don't wrap cancellation exceptions
            }
        }
    }
}
```

### Anti-Pattern: Async Void

Never use `async void` except for event handlers. Exceptions cannot be caught.

```csharp
// BAD: async void - exceptions are unobservable
public async void ProcessDataBad(Data data)
{
    await SaveAsync(data); // If this throws, the exception is lost
}

// GOOD: Return Task for proper exception handling
public async Task ProcessDataAsync(Data data)
{
    await SaveAsync(data);
}

// ACCEPTABLE: Event handlers only
private async void Button_Click(object sender, EventArgs e)
{
    try
    {
        await ProcessDataAsync(GetData());
    }
    catch (Exception ex)
    {
        ShowError(ex.Message);
    }
}
```

---

## Disposal Patterns

### Pattern: Using Declarations and Statements

Use `using` to ensure disposal even when exceptions occur.

```csharp
// GOOD: Using declaration (C# 8+)
public async Task ProcessFileAsync(string path, CancellationToken cancellationToken = default)
{
    await using var stream = File.OpenRead(path);
    await using var reader = new StreamReader(stream);

    var content = await reader.ReadToEndAsync(cancellationToken);
    await ProcessContentAsync(content, cancellationToken);
}

// GOOD: Using statement for explicit scope
public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
{
    using (var response = await _httpClient.GetAsync(url, cancellationToken))
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}

// BAD: Manual disposal is error-prone
public async Task ProcessFileBadAsync(string path)
{
    var stream = File.OpenRead(path);
    var reader = new StreamReader(stream);

    var content = await reader.ReadToEndAsync();
    await ProcessContentAsync(content);

    reader.Dispose(); // May not be reached if exception occurs
    stream.Dispose();
}
```

### Pattern: IAsyncDisposable

Use `await using` for types implementing `IAsyncDisposable`.

```csharp
// GOOD: Async disposal for async resources
public class AsyncDatabaseConnection : IAsyncDisposable
{
    private readonly SqlConnection _connection;

    public AsyncDatabaseConnection(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _connection.OpenAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

// Usage
public async Task QueryDataAsync(CancellationToken cancellationToken = default)
{
    await using var connection = new AsyncDatabaseConnection(_connectionString);
    await connection.OpenAsync(cancellationToken);
    // Use connection...
}
```

### Pattern: HttpClient via IHttpClientFactory

Never create `HttpClient` instances directly in application code.

```csharp
// GOOD: Use IHttpClientFactory to avoid socket exhaustion
public class ApiClient(IHttpClientFactory httpClientFactory)
{
    public async Task<ApiResponse> GetDataAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient("ExternalApi");
        var response = await client.GetAsync(endpoint, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken: cancellationToken);
    }
}

// Registration in DI
services.AddHttpClient("ExternalApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// BAD: Direct HttpClient creation causes socket exhaustion
public class ApiClientBad
{
    public async Task<ApiResponse> GetDataAsync(string endpoint)
    {
        // NEVER DO THIS - leads to socket exhaustion
        using var client = new HttpClient();
        var response = await client.GetAsync($"https://api.example.com/{endpoint}");
        return await response.Content.ReadFromJsonAsync<ApiResponse>();
    }
}
```

### Pattern: Unsubscribe Event Handlers

Prevent memory leaks by unsubscribing from events.

```csharp
// GOOD: Unsubscribe in Dispose
public class DataMonitor(IEventSource eventSource) : IDisposable
{
    private bool _disposed;

    public void StartMonitoring()
    {
        eventSource.DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataEventArgs e)
    {
        // Handle event...
    }

    public void Dispose()
    {
        if (_disposed) return;

        eventSource.DataReceived -= OnDataReceived;
        _disposed = true;
    }
}
```

### Pattern: DI Lifetime Management

Choose appropriate service lifetimes to avoid captured dependencies and memory leaks.

```csharp
// Registration
services.AddSingleton<ICacheService, CacheService>();     // One instance for app lifetime
services.AddScoped<IUserContext, UserContext>();          // One instance per request
services.AddTransient<IValidator, Validator>();           // New instance each time

// BAD: Singleton captures scoped dependency - memory leak and incorrect behavior
public class BadSingletonService(IUserContext userContext) // Scoped captured in singleton!
{
    // userContext will be the same for all requests
}

// GOOD: Use IServiceScopeFactory for scoped access in singletons
public class GoodSingletonService(IServiceScopeFactory scopeFactory)
{
    public async Task DoWorkAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
        // Use userContext...
    }
}
```

---

## Security Patterns

### Pattern: Parameterized Queries

Always use parameterized queries to prevent SQL injection.

```csharp
// GOOD: Parameterized query via Dapper
public class UserRepository(IDbConnection connection)
{
    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var command = new CommandDefinition(
            "SELECT Id, Name, Email FROM Users WHERE Id = @Id",
            new { Id = userId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<User>(command);
    }
}

// GOOD: Entity Framework handles parameterization
public class UserRepository(AppDbContext context)
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }
}

// BAD: String interpolation = SQL injection vulnerability
public async Task<User?> GetByIdBadAsync(int userId)
{
    // NEVER DO THIS
    var sql = $"SELECT * FROM Users WHERE Id = {userId}";
    return await connection.QuerySingleOrDefaultAsync<User>(sql);
}
```

### Pattern: Input Validation

Validate all inputs at system boundaries.

```csharp
// GOOD: Comprehensive input validation
public class CreateUserCommand
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; init; }

    [Required]
    [MinLength(12)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$",
        ErrorMessage = "Password must contain uppercase, lowercase, and number")]
    public required string Password { get; init; }
}

public class UserService(IValidator<CreateUserCommand> validator)
{
    public async Task<Result<User>> CreateUserAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<User>.Failure(validation.Errors);
        }

        // Proceed with creation...
    }
}
```

### Pattern: Path Traversal Prevention

Validate file paths to prevent directory traversal attacks.

```csharp
// GOOD: Validate paths are within allowed directory
public class FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
{
    public async Task<byte[]> GetFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // Sanitize filename
        var sanitizedName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(sanitizedName) || sanitizedName != fileName)
        {
            throw new ArgumentException("Invalid filename", nameof(fileName));
        }

        var uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
        var filePath = Path.GetFullPath(Path.Combine(uploadsPath, sanitizedName));

        // Ensure resolved path is within uploads directory
        if (!filePath.StartsWith(uploadsPath + Path.DirectorySeparatorChar))
        {
            logger.LogWarning("Path traversal attempt detected: {FileName}", fileName);
            throw new UnauthorizedAccessException("Access denied");
        }

        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }
}

// BAD: Allows path traversal
public async Task<byte[]> GetFileBadAsync(string fileName)
{
    // NEVER DO THIS - allows ../../../etc/passwd
    var path = Path.Combine(_uploadsPath, fileName);
    return await File.ReadAllBytesAsync(path);
}
```

### Pattern: Secret Management

Never hardcode secrets; use secure configuration.

```csharp
// GOOD: Use configuration and secret management
public class ExternalApiClient(IOptions<ApiSettings> options, HttpClient httpClient)
{
    public async Task<Response> CallApiAsync(Request request, CancellationToken cancellationToken = default)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        return await httpClient.PostAsJsonAsync("/api/endpoint", request, cancellationToken);
    }
}

// appsettings.json (development only, use user secrets or Azure Key Vault in production)
// {
//   "ApiSettings": {
//     "ApiKey": "from-user-secrets-or-keyvault"
//   }
// }

// BAD: Hardcoded secrets
public class ExternalApiClientBad(HttpClient httpClient)
{
    // NEVER DO THIS
    private const string ApiKey = "sk-abc123secretkey";

    public async Task CallApiAsync()
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);
    }
}
```

### Pattern: Authorization Checks

Enforce authorization before accessing protected resources.

```csharp
// GOOD: Authorization check before sensitive operation
public class DocumentService(
    IDocumentRepository repository,
    IAuthorizationService authorizationService)
{
    public async Task<Document?> GetDocumentAsync(
        Guid documentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var authResult = await authorizationService.AuthorizeAsync(
            user, document, "DocumentRead");

        if (!authResult.Succeeded)
        {
            throw new UnauthorizedAccessException("Access to document denied");
        }

        return document;
    }
}

// Controller with authorization attribute
[Authorize]
public class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<Document>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await documentService.GetDocumentAsync(id, User, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }
}
```

### Pattern: Output Encoding

Encode output to prevent XSS attacks.

```csharp
// GOOD: Razor automatically encodes output
// In .cshtml:
// <p>@Model.UserInput</p>  <!-- Automatically HTML-encoded -->

// GOOD: Explicit encoding when needed
public class HtmlService(HtmlEncoder encoder)
{
    public string SafeRender(string userInput)
    {
        return encoder.Encode(userInput);
    }
}

// BAD: Raw HTML output
// <p>@Html.Raw(Model.UserInput)</p>  <!-- XSS vulnerability if UserInput contains scripts -->
```

---

## Review Questions

When reviewing code for these patterns, ask:

**Async**
- Can any async call block the calling thread?
- Are all `CancellationToken` parameters propagated?
- Does library code use `ConfigureAwait(false)`?

**Disposal**
- Are all `IDisposable`/`IAsyncDisposable` resources properly disposed?
- Are event handlers unsubscribed?
- Are DI lifetimes appropriate for the dependencies?

**Security**
- Could user input reach a sensitive operation unvalidated?
- Are queries parameterized?
- Could file operations be exploited for path traversal?
- Are secrets stored securely?
