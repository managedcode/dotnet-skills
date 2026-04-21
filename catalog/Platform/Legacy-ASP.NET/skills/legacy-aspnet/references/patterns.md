# Maintenance Patterns for Legacy ASP.NET Code

This document provides patterns for maintaining and stabilizing classic ASP.NET applications on .NET Framework.

## Stabilization Patterns

### Isolate Business Logic

Extract business logic from code-behind and controllers into standalone classes:

```csharp
// Before: Logic embedded in code-behind
public partial class OrderPage : Page
{
    protected void SubmitOrder_Click(object sender, EventArgs e)
    {
        var order = new Order();
        order.CustomerId = int.Parse(CustomerIdField.Text);
        order.Total = CalculateTotal();
        // validation, persistence, notification all inline
    }
}

// After: Logic in a service class
public class OrderService
{
    public OrderResult SubmitOrder(OrderRequest request)
    {
        // validation, persistence, notification encapsulated
    }
}
```

Benefits:
- Testable without Web Forms infrastructure
- Portable to other hosting models
- Clear boundaries for future migration

### Introduce Dependency Injection Gradually

Add a DI container without requiring a full rewrite:

1. Install a container compatible with System.Web (Autofac, Unity, Simple Injector)
2. Configure the container in Global.asax Application_Start
3. Use property injection for Web Forms pages
4. Use constructor injection for new service classes

```csharp
// Global.asax.cs
protected void Application_Start()
{
    var builder = new ContainerBuilder();
    builder.RegisterType<OrderService>().As<IOrderService>();
    builder.RegisterType<OrderRepository>().As<IOrderRepository>();
    var container = builder.Build();

    // Web Forms property injection
    var propertyInjection = new AutofacWebFormsPropertyInjection(container);
    propertyInjection.InjectDependenciesIntoPage(this);
}
```

### Wrap Static Dependencies

Isolate HttpContext.Current and other static dependencies:

```csharp
// Wrapper interface
public interface IHttpContextAccessor
{
    HttpContextBase Current { get; }
}

// Production implementation
public class WebHttpContextAccessor : IHttpContextAccessor
{
    public HttpContextBase Current => new HttpContextWrapper(HttpContext.Current);
}

// Test implementation
public class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContextBase Current { get; set; }
}
```

### Configuration Abstraction

Wrap ConfigurationManager to enable testing and future migration:

```csharp
public interface IAppConfiguration
{
    string GetConnectionString(string name);
    string GetSetting(string key);
    T GetSection<T>(string sectionName) where T : class;
}

public class WebConfigConfiguration : IAppConfiguration
{
    public string GetConnectionString(string name) =>
        ConfigurationManager.ConnectionStrings[name]?.ConnectionString;

    public string GetSetting(string key) =>
        ConfigurationManager.AppSettings[key];

    public T GetSection<T>(string sectionName) where T : class =>
        ConfigurationManager.GetSection(sectionName) as T;
}
```

## Web Forms Patterns

### Reduce ViewState Dependency

Minimize ViewState usage to improve performance and simplify migration:

1. Disable ViewState at page level when not needed: `EnableViewState="false"`
2. Use explicit hidden fields for required state
3. Store complex state in session or database
4. Prefer server-side data retrieval over ViewState round-trips

### Master Page Consolidation

Reduce layout duplication before migration:

1. Consolidate to a single master page hierarchy
2. Extract common scripts and styles to a shared location
3. Use ContentPlaceHolders consistently
4. Document the master page contract for Razor layout conversion

### User Control Inventory

Catalog user controls for component migration planning:

| Control | Dependencies | State | Migration Target |
|---------|--------------|-------|------------------|
| HeaderControl.ascx | Session, Auth | Minimal | Razor partial |
| OrderGrid.ascx | ViewState, DataSource | Heavy | Blazor component |
| SearchBox.ascx | None | None | Tag Helper |

## MVC Patterns

### Area Organization

Organize large MVC applications into areas for incremental migration:

```
/Areas
  /Orders
    /Controllers
    /Views
    /Models
  /Customers
    /Controllers
    /Views
    /Models
  /Legacy
    /Controllers (unmigrated controllers)
    /Views
```

Each area can be migrated independently.

### Filter Standardization

Consolidate action filters before migration:

```csharp
// Standardized exception filter
public class StandardExceptionFilter : IExceptionFilter
{
    private readonly ILogger _logger;

    public StandardExceptionFilter(ILogger logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext filterContext)
    {
        _logger.Error(filterContext.Exception, "Unhandled exception in {Controller}/{Action}",
            filterContext.RouteData.Values["controller"],
            filterContext.RouteData.Values["action"]);

        filterContext.Result = new ViewResult { ViewName = "Error" };
        filterContext.ExceptionHandled = true;
    }
}
```

### Route Consolidation

Document and simplify routing before migration:

```csharp
// Before: Scattered route definitions
routes.MapRoute("OrderDetails", "orders/{id}", new { controller = "Orders", action = "Details" });
routes.MapRoute("OrderList", "orders", new { controller = "Orders", action = "Index" });
routes.MapRoute("CustomerOrders", "customers/{customerId}/orders", new { controller = "Orders", action = "ByCustomer" });

// After: Consistent attribute routing
[RoutePrefix("orders")]
public class OrdersController : Controller
{
    [Route("")]
    public ActionResult Index() { }

    [Route("{id:int}")]
    public ActionResult Details(int id) { }

    [Route("~/customers/{customerId:int}/orders")]
    public ActionResult ByCustomer(int customerId) { }
}
```

## Session and Caching Patterns

### Session Abstraction

Wrap session access for testing and migration:

```csharp
public interface ISessionStore
{
    T Get<T>(string key);
    void Set<T>(string key, T value);
    void Remove(string key);
}

public class AspNetSessionStore : ISessionStore
{
    private readonly HttpSessionStateBase _session;

    public AspNetSessionStore(HttpSessionStateBase session)
    {
        _session = session;
    }

    public T Get<T>(string key) => (T)_session[key];
    public void Set<T>(string key, T value) => _session[key] = value;
    public void Remove(string key) => _session.Remove(key);
}
```

### Cache Abstraction

Wrap System.Web.Caching for migration:

```csharp
public interface ICacheStore
{
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan expiration);
    void Remove(string key);
}

public class WebCacheStore : ICacheStore
{
    public T Get<T>(string key) => (T)HttpRuntime.Cache.Get(key);

    public void Set<T>(string key, T value, TimeSpan expiration)
    {
        HttpRuntime.Cache.Insert(key, value, null,
            DateTime.UtcNow.Add(expiration),
            Cache.NoSlidingExpiration);
    }

    public void Remove(string key) => HttpRuntime.Cache.Remove(key);
}
```

## Authentication Patterns

### Authentication Abstraction

Wrap Forms Authentication for flexibility:

```csharp
public interface IAuthenticationService
{
    void SignIn(string username, bool persistent);
    void SignOut();
    string GetCurrentUser();
    bool IsAuthenticated { get; }
}

public class FormsAuthenticationService : IAuthenticationService
{
    public void SignIn(string username, bool persistent)
    {
        FormsAuthentication.SetAuthCookie(username, persistent);
    }

    public void SignOut()
    {
        FormsAuthentication.SignOut();
    }

    public string GetCurrentUser() => HttpContext.Current.User?.Identity?.Name;

    public bool IsAuthenticated => HttpContext.Current.User?.Identity?.IsAuthenticated ?? false;
}
```

### Role-Based Authorization Consolidation

Standardize role checks before migration:

```csharp
public interface IAuthorizationService
{
    bool HasPermission(string permission);
    bool IsInRole(string role);
    IEnumerable<string> GetRoles();
}

public class WebAuthorizationService : IAuthorizationService
{
    public bool HasPermission(string permission)
    {
        // Map permissions to roles or custom logic
        return Roles.IsUserInRole(permission);
    }

    public bool IsInRole(string role) => Roles.IsUserInRole(role);

    public IEnumerable<string> GetRoles() => Roles.GetRolesForUser();
}
```

## Logging and Monitoring Patterns

### Structured Logging Introduction

Add structured logging without changing existing code:

```csharp
// Logging abstraction
public interface IAppLogger
{
    void Info(string message, params object[] args);
    void Warn(string message, params object[] args);
    void Error(Exception ex, string message, params object[] args);
}

// Implementation with structured logging
public class SerilogAppLogger : IAppLogger
{
    private readonly ILogger _logger;

    public SerilogAppLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Info(string message, params object[] args) =>
        _logger.Information(message, args);

    public void Warn(string message, params object[] args) =>
        _logger.Warning(message, args);

    public void Error(Exception ex, string message, params object[] args) =>
        _logger.Error(ex, message, args);
}
```

### Health Check Endpoint

Add a health check endpoint for monitoring:

```csharp
// HealthController.cs
public class HealthController : Controller
{
    private readonly IHealthCheckService _healthCheck;

    public HealthController(IHealthCheckService healthCheck)
    {
        _healthCheck = healthCheck;
    }

    [Route("health")]
    public ActionResult Index()
    {
        var result = _healthCheck.Check();
        Response.StatusCode = result.IsHealthy ? 200 : 503;
        return Json(result, JsonRequestBehavior.AllowGet);
    }
}
```

## Testing Patterns

### Test Seams for Legacy Code

Introduce test seams without major refactoring:

1. **Extract and Override**: Make methods virtual and override in tests
2. **Subclass and Override**: Create testable subclasses
3. **Wrap Static Calls**: Create instance wrappers around static dependencies

```csharp
// Extract and Override pattern
public class OrderProcessor
{
    public virtual DateTime GetCurrentTime() => DateTime.UtcNow;

    public bool IsOrderExpired(Order order)
    {
        return order.ExpirationDate < GetCurrentTime();
    }
}

// Test subclass
public class TestableOrderProcessor : OrderProcessor
{
    public DateTime CurrentTime { get; set; }
    public override DateTime GetCurrentTime() => CurrentTime;
}
```

### Integration Test Infrastructure

Create test infrastructure that mirrors production:

```csharp
[TestFixture]
public class OrderControllerTests
{
    private TestServer _server;

    [SetUp]
    public void Setup()
    {
        _server = new TestServer(new WebHostBuilder()
            .UseStartup<TestStartup>());
    }

    [Test]
    public async Task GetOrders_ReturnsOrders()
    {
        var client = _server.CreateClient();
        var response = await client.GetAsync("/api/orders");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

## Deployment Patterns

### Configuration Transform Management

Organize configuration transforms for maintainability:

```
/Web.config
/Web.Debug.config
/Web.Release.config
/Web.Staging.config
/Web.Production.config
```

Use consistent transform patterns:

```xml
<!-- Web.Production.config -->
<configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="#{DatabaseConnectionString}#"
         xdt:Transform="SetAttributes" xdt:Locator="Match(name)"/>
  </connectionStrings>
  <appSettings>
    <add key="Environment" value="Production"
         xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
  </appSettings>
</configuration>
```

### IIS Configuration as Code

Document IIS configuration for reproducibility:

```powershell
# Create application pool
New-WebAppPool -Name "LegacyAppPool"
Set-ItemProperty "IIS:\AppPools\LegacyAppPool" -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\LegacyAppPool" -Name "enable32BitAppOnWin64" -Value $false

# Create website
New-Website -Name "LegacyApp" -Port 80 -PhysicalPath "C:\inetpub\wwwroot\LegacyApp" -ApplicationPool "LegacyAppPool"

# Configure authentication
Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/anonymousAuthentication" -Name "enabled" -Value $true -PSPath "IIS:\Sites\LegacyApp"
Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/windowsAuthentication" -Name "enabled" -Value $false -PSPath "IIS:\Sites\LegacyApp"
```
