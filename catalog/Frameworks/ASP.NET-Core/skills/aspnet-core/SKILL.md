---
name: aspnet-core
description: "Build, debug, modernize, or review ASP.NET Core applications with correct hosting, middleware, security, configuration, logging, and deployment patterns on current .NET."
compatibility: "Requires an ASP.NET Core project or solution."
---

# ASP.NET Core

## Trigger On

- working on ASP.NET Core apps, services, or middleware
- changing auth, routing, configuration, hosting, or deployment behavior
- deciding between ASP.NET Core sub-stacks such as Blazor, Minimal APIs, or controller APIs
- debugging request pipeline issues
- modernizing legacy ASP.NET to ASP.NET Core

## Documentation

- [ASP.NET Core Overview](https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-10.0)
- [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0)
- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)
- [Configuration in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)
- [Authentication and Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-10.0)

### References

- [patterns.md](references/patterns.md) - Detailed middleware patterns, security patterns, configuration patterns, DI patterns, error handling patterns, and logging patterns
- [anti-patterns.md](references/anti-patterns.md) - Common ASP.NET Core mistakes including HttpClient misuse, async anti-patterns, configuration errors, DI issues, middleware ordering problems, and security vulnerabilities

## Workflow

1. **Detect the real hosting shape first:**
   - top-level `Program.cs` structure
   - middleware order and registration
   - auth model (Identity, JWT, OAuth, cookies)
   - endpoint registrations and routing

2. **Follow the correct middleware order:**
   ```
   ExceptionHandler → HttpsRedirection → Static Files → Routing
   → CORS → Authentication → Authorization → Rate Limiting
   → Response Caching → Custom Middleware → Endpoints
   ```

3. **Use built-in patterns correctly:**
   - Prefer `IOptions<T>` / `IOptionsSnapshot<T>` for configuration
   - Use `ILogger<T>` for structured logging
   - Use `IHttpClientFactory` for HTTP clients (never `new HttpClient()`)
   - Use `IHostedService` / `BackgroundService` for background work

4. **Route specialized work to specific skills:**
   - UI and components → `blazor`
   - Real-time → `signalr`
   - RPC → `grpc`
   - New HTTP APIs → `minimal-apis` (prefer unless controllers needed)
   - Controller APIs → `web-api`

5. **Validate with build, tests, and targeted endpoint checks.**

## Middleware Patterns

### Correct Order Matters
```csharp
var app = builder.Build();

app.UseExceptionHandler("/error");      // 1. Catch all exceptions
app.UseHsts();                          // 2. Security headers
app.UseHttpsRedirection();              // 3. HTTPS redirect
app.UseStaticFiles();                   // 4. Serve static files
app.UseRouting();                       // 5. Route matching
app.UseCors();                          // 6. CORS policy
app.UseAuthentication();                // 7. Who are you?
app.UseAuthorization();                 // 8. Can you access?
app.UseRateLimiter();                   // 9. Rate limiting
app.UseResponseCaching();               // 10. Response cache
app.MapControllers();                   // 11. Endpoints
```

### Custom Middleware Pattern
```csharp
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        _logger.LogInformation("Request {Path} completed in {Elapsed}ms",
            context.Request.Path, sw.ElapsedMilliseconds);
    }
}
```

## Configuration Patterns

### Strongly-Typed Options
```csharp
// appsettings.json
{
  "EmailSettings": {
    "SmtpServer": "smtp.example.com",
    "Port": 587
  }
}

// Registration
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// Usage
public class EmailService(IOptions<EmailSettings> options)
{
    private readonly EmailSettings _settings = options.Value;
}
```

### Environment-Based Configuration
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);
```

## Security Patterns

### Authentication Setup
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
```

### Authorization Policies
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("MinAge18", policy =>
        policy.RequireClaim("Age", "18", "19", "20")); // simplified
});
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| `new HttpClient()` | Socket exhaustion | `IHttpClientFactory` |
| Sync-over-async (`Task.Result`) | Thread pool starvation | `await` properly |
| Storing secrets in `appsettings.json` | Security risk | User Secrets, Key Vault |
| Catching all exceptions silently | Hides bugs | Use `IExceptionHandler` |
| `async void` in middleware | Crashes process | `async Task` |
| Missing HTTPS redirect | Security risk | `UseHttpsRedirection()` |

## Performance Best Practices

1. **Use async/await everywhere** — avoid sync blocking calls
2. **Pool DbContext properly** — use scoped lifetime
3. **Enable response compression** — `UseResponseCompression()`
4. **Use output caching** — `UseOutputCache()` for .NET 7+
5. **Profile with diagnostic tools** — Visual Studio Diagnostic Tools, PerfView
6. **Avoid allocations in hot paths** — use `Span<T>`, pooling

## Deliver

- production-credible ASP.NET Core code and config
- a clear request pipeline and hosting story
- verification that matches the affected endpoints and middleware
- security headers and HTTPS configured correctly

## Validate

- middleware order is intentional and documented
- security and configuration changes are explicit
- endpoint behavior is covered by tests or smoke checks
- no blocking calls in async context
- secrets are not committed to source control
- health checks are implemented for production readiness
