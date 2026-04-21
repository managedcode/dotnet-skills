# Migration Paths from ASP.NET to ASP.NET Core

This document outlines practical migration strategies for moving classic ASP.NET applications to ASP.NET Core.

## Migration Strategy Selection

### Incremental Migration (Recommended for Large Applications)

Use incremental migration when:
- The application has significant business logic that must remain stable
- Full rewrites carry unacceptable risk
- The team needs to maintain production stability during migration

Approach:
1. Identify natural seams in the application architecture
2. Extract shared business logic into .NET Standard libraries
3. Create a new ASP.NET Core host alongside the legacy application
4. Use YARP or a reverse proxy to route traffic between old and new endpoints
5. Migrate one vertical slice at a time
6. Decommission legacy endpoints as Core equivalents stabilize

### Strangler Fig Pattern

Use the strangler fig approach when:
- The application can tolerate running both stacks in parallel
- You can route at the edge (load balancer, reverse proxy)
- Migration will span multiple release cycles

Implementation:
1. Deploy an ASP.NET Core application alongside the legacy application
2. Configure routing rules to send new or migrated endpoints to Core
3. Gradually move functionality from legacy to Core
4. Remove legacy application once all endpoints are migrated

### Full Rewrite

Consider full rewrite only when:
- The legacy application is small and well-understood
- Business requirements have changed significantly
- Technical debt makes incremental migration more expensive than rebuilding
- The team has capacity for parallel development and testing

## Component Migration Paths

### Web Forms to ASP.NET Core

Web Forms has no direct equivalent in ASP.NET Core. Migration options:

1. **Razor Pages**: Best fit for page-centric workflows with code-behind patterns
   - Map ASPX pages to Razor Pages
   - Replace server controls with Tag Helpers or Razor components
   - Move code-behind logic to PageModel handlers

2. **Blazor Server**: Best fit when preserving stateful component behavior
   - Recreate server controls as Blazor components
   - Maintain server-side rendering with interactive updates
   - Preserve familiar event-driven programming model

3. **MVC**: Best fit when moving toward clean separation of concerns
   - Extract business logic into services
   - Map postback handlers to controller actions
   - Replace ViewState with explicit state management

### MVC 5 to ASP.NET Core MVC

MVC migration is more straightforward but still requires attention:

1. **Routing**: Replace RouteConfig with endpoint routing
   - `routes.MapRoute` becomes `endpoints.MapControllerRoute`
   - Attribute routing works similarly but uses different base classes

2. **Filters**: Migrate action filters to ASP.NET Core filter contracts
   - `IActionFilter` interface signature changes
   - `FilterContext` replaces `ActionExecutingContext` patterns

3. **Dependency Injection**: Move from third-party containers to built-in DI
   - Register services in `Program.cs` or `Startup.cs`
   - Replace service locator patterns with constructor injection

4. **Configuration**: Replace Web.config with appsettings.json
   - Move connection strings to configuration providers
   - Replace ConfigurationManager calls with IConfiguration injection

5. **Authentication**: Replace Forms Authentication or ASP.NET Identity
   - Cookie authentication middleware replaces FormsAuthentication
   - ASP.NET Core Identity replaces legacy ASP.NET Identity

### Session State

1. **In-Process Session**: Direct migration to ASP.NET Core session middleware
2. **SQL Server Session**: Use distributed session with SQL Server provider
3. **Redis Session**: Use distributed session with Redis provider
4. **Custom Providers**: Implement IDistributedCache or ITicketStore

### Caching

1. **System.Web.Caching**: Replace with IMemoryCache or IDistributedCache
2. **Output Caching**: Use Response Caching middleware
3. **HttpRuntime.Cache**: Inject IMemoryCache instead

### HTTP Modules and Handlers

1. **HTTP Modules**: Convert to middleware
   - `BeginRequest` maps to middleware pipeline position
   - `AuthenticateRequest` maps to authentication middleware
   - `AuthorizeRequest` maps to authorization middleware

2. **HTTP Handlers**: Convert to middleware or endpoint handlers
   - ASHX handlers become middleware or minimal API endpoints
   - Map specific routes to handler logic

## Configuration Migration

### Web.config to appsettings.json

| Web.config | ASP.NET Core |
|------------|--------------|
| `<connectionStrings>` | `ConnectionStrings` section in appsettings.json |
| `<appSettings>` | Configuration sections or direct values |
| `<system.web>` | Middleware and service configuration |
| `<httpRuntime>` | Kestrel limits and middleware options |
| `<customErrors>` | Exception handling middleware |
| `<authorization>` | Authorization middleware and policies |

### Machine.config Dependencies

Identify machine-level configuration dependencies:
- Connection strings in machine.config
- Custom configuration sections
- GAC assemblies

Migrate these to:
- Environment variables or secure configuration providers
- NuGet package references
- Application-level configuration

## Testing Migration

1. **Unit Tests**: Often portable with namespace changes
   - Update test framework packages to .NET-compatible versions
   - Replace HttpContext mocks with ASP.NET Core test infrastructure

2. **Integration Tests**: Require significant rework
   - Use WebApplicationFactory for in-memory testing
   - Replace SystemWeb test utilities with Microsoft.AspNetCore.Mvc.Testing

3. **End-to-End Tests**: Often require minimal changes
   - Update URLs if routing changes
   - Adjust authentication flows if mechanisms change

## Risk Mitigation

1. **Feature Parity Checklist**: Document all legacy features before migration
2. **Parallel Running**: Run both stacks in production during migration
3. **Rollback Plan**: Maintain ability to route back to legacy
4. **Monitoring**: Implement equivalent logging and metrics in both stacks
5. **Performance Baseline**: Capture legacy performance metrics for comparison

## Common Pitfalls

1. **Assuming API compatibility**: ASP.NET Core is not a drop-in replacement
2. **Ignoring IIS dependencies**: Classic pipeline behaviors do not exist in Core
3. **Session affinity assumptions**: Default session is not sticky in Core
4. **ViewState replacement**: There is no automatic state preservation
5. **Global.asax lifecycle**: Application events work differently
6. **HttpContext.Current**: Static context access does not exist in Core
