# ASP.NET Core Patterns Reference

This document provides detailed patterns for middleware, security, and configuration in ASP.NET Core applications.

## Table of Contents

- [Middleware Patterns](#middleware-patterns)
- [Security Patterns](#security-patterns)
- [Configuration Patterns](#configuration-patterns)
- [Dependency Injection Patterns](#dependency-injection-patterns)
- [Error Handling Patterns](#error-handling-patterns)
- [Logging Patterns](#logging-patterns)

---

## Middleware Patterns

### Pipeline Architecture

The ASP.NET Core request pipeline consists of middleware components that process requests in sequence. Each middleware can:
- Handle the request and short-circuit the pipeline
- Pass the request to the next middleware
- Execute code before and after the next middleware

### Canonical Middleware Order

```csharp
var app = builder.Build();

// 1. Exception handling - must be first to catch all exceptions
app.UseExceptionHandler("/error");

// 2. HSTS - HTTP Strict Transport Security header
app.UseHsts();

// 3. HTTPS redirection - before any content is served
app.UseHttpsRedirection();

// 4. Static files - serve before routing for performance
app.UseStaticFiles();

// 5. Routing - must precede authentication/authorization
app.UseRouting();

// 6. CORS - after routing, before auth
app.UseCors("PolicyName");

// 7. Authentication - identify the user
app.UseAuthentication();

// 8. Authorization - verify access rights
app.UseAuthorization();

// 9. Rate limiting - protect endpoints
app.UseRateLimiter();

// 10. Response caching - cache authorized responses
app.UseResponseCaching();

// 11. Output caching (.NET 7+) - server-side caching
app.UseOutputCache();

// 12. Custom middleware - business logic
app.UseMiddleware<CustomMiddleware>();

// 13. Endpoints - terminal middleware
app.MapControllers();
app.MapRazorPages();
app.MapBlazorHub();
```

### Class-Based Middleware Pattern

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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Request {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
    }
}

// Extension method for clean registration
public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestTimingMiddleware>();
    }
}
```

### Convention-Based Middleware with Dependencies

```csharp
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // Scoped services can be injected into InvokeAsync
    public async Task InvokeAsync(
        HttpContext context,
        ITenantService tenantService,
        ILogger<TenantResolutionMiddleware> logger)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant header required" });
            return;
        }

        var tenant = await tenantService.GetTenantAsync(tenantId);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" });
            return;
        }

        context.Items["Tenant"] = tenant;
        logger.LogDebug("Resolved tenant {TenantId}", tenantId);

        await _next(context);
    }
}
```

### Inline Middleware for Simple Cases

```csharp
// Use for simple, non-reusable middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    await next(context);
});

// Terminal middleware (does not call next)
app.Map("/health", app => app.Run(async context =>
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { status = "healthy" });
}));
```

### Conditional Middleware

```csharp
// Apply middleware only for specific paths
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    appBuilder => appBuilder.UseMiddleware<ApiLoggingMiddleware>());

// Branch the pipeline
app.MapWhen(
    context => context.Request.Query.ContainsKey("debug"),
    appBuilder => appBuilder.UseMiddleware<DebugMiddleware>());
```

### Short-Circuiting Middleware

```csharp
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<MaintenanceOptions> _options;

    public MaintenanceModeMiddleware(RequestDelegate next, IOptions<MaintenanceOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Value.IsEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = _options.Value.RetryAfterSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Service under maintenance",
                estimatedReturn = _options.Value.EstimatedReturn
            });
            return; // Short-circuit - do not call next
        }

        await _next(context);
    }
}
```

---

## Security Patterns

### JWT Authentication Setup

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1) // Reduce default 5-minute skew
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Add custom claims or perform additional validation
            return Task.CompletedTask;
        }
    };
});
```

### Cookie Authentication with Security Best Practices

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AppAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/access-denied";

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Validate user still exists and is active
                var userService = context.HttpContext.RequestServices
                    .GetRequiredService<IUserService>();
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userId is null || !await userService.IsUserActiveAsync(userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
        };
    });
```

### Policy-Based Authorization

```csharp
builder.Services.AddAuthorization(options =>
{
    // Simple role-based policy
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Claim-based policy
    options.AddPolicy("VerifiedEmail", policy =>
        policy.RequireClaim("email_verified", "true"));

    // Multiple requirements (AND)
    options.AddPolicy("SeniorAdmin", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("experience_years", "5", "6", "7", "8", "9", "10"));

    // Custom requirement
    options.AddPolicy("MinimumAge", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(18)));

    // Resource-based authorization setup
    options.AddPolicy("DocumentOwner", policy =>
        policy.Requirements.Add(new DocumentOwnerRequirement()));
});

// Custom requirement
public class MinimumAgeRequirement : IAuthorizationRequirement
{
    public int MinimumAge { get; }
    public MinimumAgeRequirement(int minimumAge) => MinimumAge = minimumAge;
}

// Handler for custom requirement
public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var birthDateClaim = context.User.FindFirst("birth_date");
        if (birthDateClaim is null)
        {
            return Task.CompletedTask;
        }

        if (DateTime.TryParse(birthDateClaim.Value, out var birthDate))
        {
            var age = DateTime.Today.Year - birthDate.Year;
            if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;

            if (age >= requirement.MinimumAge)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

// Register handler
builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeHandler>();
```

### Resource-Based Authorization

```csharp
public class DocumentOwnerRequirement : IAuthorizationRequirement { }

public class DocumentOwnerHandler : AuthorizationHandler<DocumentOwnerRequirement, Document>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentOwnerRequirement requirement,
        Document resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == resource.OwnerId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Usage in controller
public class DocumentsController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;

    public DocumentsController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, DocumentUpdateDto dto)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(
            User, document, "DocumentOwner");

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        // Proceed with update
        return Ok();
    }
}
```

### Security Headers Middleware

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.XContentTypeOptions = "nosniff";

        // Prevent clickjacking
        context.Response.Headers.XFrameOptions = "DENY";

        // XSS protection (legacy browsers)
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy
        context.Response.Headers["Permissions-Policy"] =
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        // Content Security Policy (customize based on your needs)
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:;";

        await _next(context);
    }
}
```

### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    // Named policy for specific origins
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://admin.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Policy for API clients
    options.AddPolicy("ApiClients", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!)
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Apply globally or per-endpoint
app.UseCors("AllowSpecificOrigins");

// Or per-controller
[EnableCors("ApiClients")]
public class ApiController : ControllerBase { }
```

### Rate Limiting (.NET 7+)

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Fixed window limiter
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Sliding window limiter
    options.AddSlidingWindowLimiter("sliding", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.PermitLimit = 100;
    });

    // Token bucket limiter
    options.AddTokenBucketLimiter("token", opt =>
    {
        opt.TokenLimit = 100;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.TokensPerPeriod = 10;
    });

    // Per-user rate limiting
    options.AddPolicy("per-user", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60
            });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.TotalSeconds
                : 60
        }, token);
    };
});

app.UseRateLimiter();

// Apply to endpoints
app.MapGet("/api/data", () => "data")
    .RequireRateLimiting("per-user");
```

---

## Configuration Patterns

### Strongly-Typed Options with Validation

```csharp
public class EmailSettings
{
    public const string SectionName = "Email";

    [Required]
    public string SmtpServer { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

// Registration with validation
builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart(); // Fail fast on startup if invalid

// Custom validation
builder.Services.AddOptions<DatabaseSettings>()
    .Bind(builder.Configuration.GetSection("Database"))
    .Validate(settings =>
    {
        return !string.IsNullOrEmpty(settings.ConnectionString)
            && settings.MaxPoolSize >= settings.MinPoolSize;
    }, "Database configuration is invalid")
    .ValidateOnStart();
```

### Options Interfaces Usage

```csharp
public class EmailService
{
    // IOptions<T> - singleton, read once at startup
    public EmailService(IOptions<EmailSettings> options)
    {
        var settings = options.Value;
    }
}

public class NotificationService
{
    // IOptionsSnapshot<T> - scoped, re-reads config per request
    public NotificationService(IOptionsSnapshot<EmailSettings> options)
    {
        var settings = options.Value;
    }
}

public class BackgroundEmailProcessor : BackgroundService
{
    // IOptionsMonitor<T> - singleton with change notifications
    private readonly IOptionsMonitor<EmailSettings> _optionsMonitor;
    private EmailSettings _currentSettings;

    public BackgroundEmailProcessor(IOptionsMonitor<EmailSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
        _currentSettings = optionsMonitor.CurrentValue;

        _optionsMonitor.OnChange(settings =>
        {
            _currentSettings = settings;
            // Handle configuration change
        });
    }
}
```

### Multi-Environment Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuration sources (later sources override earlier ones)
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true) // Git-ignored local overrides
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Development-only sources
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Production secret stores
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/"),
        new DefaultAzureCredential());
}
```

### Named Options Pattern

```csharp
// appsettings.json
{
  "Storage": {
    "Primary": {
      "ConnectionString": "...",
      "ContainerName": "primary"
    },
    "Backup": {
      "ConnectionString": "...",
      "ContainerName": "backup"
    }
  }
}

// Registration
builder.Services.Configure<StorageSettings>("Primary",
    builder.Configuration.GetSection("Storage:Primary"));
builder.Services.Configure<StorageSettings>("Backup",
    builder.Configuration.GetSection("Storage:Backup"));

// Usage with IOptionsSnapshot
public class StorageService
{
    private readonly StorageSettings _primarySettings;
    private readonly StorageSettings _backupSettings;

    public StorageService(IOptionsSnapshot<StorageSettings> options)
    {
        _primarySettings = options.Get("Primary");
        _backupSettings = options.Get("Backup");
    }
}
```

### Post-Configure and Configure All

```csharp
// Post-configure runs after all Configure calls
builder.Services.PostConfigure<EmailSettings>(settings =>
{
    // Apply defaults or computed values
    if (string.IsNullOrEmpty(settings.FromAddress))
    {
        settings.FromAddress = "noreply@example.com";
    }
});

// Configure all instances of an options type
builder.Services.ConfigureAll<StorageSettings>(settings =>
{
    settings.TimeoutSeconds = 30; // Apply to all named instances
});
```

---

## Dependency Injection Patterns

### Service Lifetimes

```csharp
// Singleton - one instance for application lifetime
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Scoped - one instance per HTTP request
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Transient - new instance every time
builder.Services.AddTransient<IEmailBuilder, EmailBuilder>();
```

### Factory Pattern Registration

```csharp
// Simple factory
builder.Services.AddScoped<IPaymentProcessor>(sp =>
{
    var config = sp.GetRequiredService<IOptions<PaymentSettings>>().Value;
    return config.Provider switch
    {
        "Stripe" => new StripePaymentProcessor(config),
        "PayPal" => new PayPalPaymentProcessor(config),
        _ => throw new InvalidOperationException($"Unknown provider: {config.Provider}")
    };
});

// Named factory with keyed services (.NET 8+)
builder.Services.AddKeyedScoped<IPaymentProcessor, StripePaymentProcessor>("stripe");
builder.Services.AddKeyedScoped<IPaymentProcessor, PayPalPaymentProcessor>("paypal");

// Usage
public class CheckoutService([FromKeyedServices("stripe")] IPaymentProcessor stripeProcessor)
{
    // ...
}
```

### Decorator Pattern

```csharp
// Manual decoration
builder.Services.AddScoped<IRepository, SqlRepository>();
builder.Services.Decorate<IRepository, CachingRepository>();
builder.Services.Decorate<IRepository, LoggingRepository>();

// Extension method implementation
public static class ServiceCollectionExtensions
{
    public static IServiceCollection Decorate<TInterface, TDecorator>(
        this IServiceCollection services)
        where TDecorator : TInterface
    {
        var descriptor = services.Single(d => d.ServiceType == typeof(TInterface));

        services.Add(new ServiceDescriptor(
            typeof(TInterface),
            sp =>
            {
                var inner = (TInterface)ActivatorUtilities.CreateInstance(
                    sp,
                    descriptor.ImplementationType!);
                return ActivatorUtilities.CreateInstance<TDecorator>(sp, inner);
            },
            descriptor.Lifetime));

        services.Remove(descriptor);
        return services;
    }
}
```

### HttpClientFactory Patterns

```csharp
// Typed client
builder.Services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// Named client
builder.Services.AddHttpClient("weather", client =>
{
    client.BaseAddress = new Uri("https://api.weather.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Resilience policies with Polly
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

---

## Error Handling Patterns

### Global Exception Handler (.NET 8+)

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = httpContext.Request.Path,
            Detail = _environment.IsDevelopment() ? exception.Message : null
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

// Registration
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

app.UseExceptionHandler();
```

### Problem Details Configuration

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        if (context.HttpContext.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true)
        {
            context.ProblemDetails.Extensions["machineName"] = Environment.MachineName;
        }
    };
});
```

---

## Logging Patterns

### Structured Logging with Serilog

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "MyApp")
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.Seq(context.Configuration["Seq:ServerUrl"]!);
});

// Request logging middleware
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    };
});
```

### High-Performance Logging with Source Generators

```csharp
public static partial class LogMessages
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Processing order {OrderId} for customer {CustomerId}")]
    public static partial void ProcessingOrder(
        ILogger logger,
        string orderId,
        string customerId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Order {OrderId} processing delayed, retry attempt {Attempt}")]
    public static partial void OrderProcessingDelayed(
        ILogger logger,
        string orderId,
        int attempt);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to process order {OrderId}")]
    public static partial void OrderProcessingFailed(
        ILogger logger,
        string orderId,
        Exception exception);
}

// Usage
public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(ILogger<OrderProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(Order order)
    {
        LogMessages.ProcessingOrder(_logger, order.Id, order.CustomerId);

        try
        {
            // Process order
        }
        catch (Exception ex)
        {
            LogMessages.OrderProcessingFailed(_logger, order.Id, ex);
            throw;
        }
    }
}
```

### Correlation and Scoped Logging

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString()
        }))
        {
            await _next(context);
        }
    }
}
```
