---
name: managedcode-communication
description: "Use ManagedCode.Communication when a .NET application needs explicit result objects, structured errors, and predictable service or API boundaries instead of exception-driven control flow. USE FOR: integrating ManagedCode.Communication into services or APIs; replacing exception-driven result handling with explicit results; reviewing service boundaries that return. DO NOT USE FOR: unrelated stacks; generic tasks that do not need this specific guidance. INVOKES: inspect the repository context, edit targeted files, and run relevant build, test, lint, or validation commands when changes are made."
compatibility: "Requires a .NET application, service layer, or API boundary that integrates ManagedCode.Communication."
---

# ManagedCode.Communication

## Trigger On

- integrating `ManagedCode.Communication` into services or APIs
- replacing exception-driven result handling with explicit results
- reviewing service boundaries that return success or failure payloads
- documenting result-pattern usage across ASP.NET Core or application services
- mapping application errors to RFC 7807 problem details, Minimal API results, SignalR filters, or Orleans call filters

## Install

Use the package that matches the boundary. Current upstream release reviewed: `v10.2.2`.

```bash
dotnet add package ManagedCode.Communication
dotnet add package ManagedCode.Communication.AspNetCore
dotnet add package ManagedCode.Communication.Extensions
dotnet add package ManagedCode.Communication.Orleans
```

For pinned project files:

```xml
<PackageReference Include="ManagedCode.Communication" Version="10.2.2" />
<PackageReference Include="ManagedCode.Communication.AspNetCore" Version="10.2.2" />
<PackageReference Include="ManagedCode.Communication.Extensions" Version="10.2.2" />
<PackageReference Include="ManagedCode.Communication.Orleans" Version="10.2.2" />
```

## Workflow

1. Confirm the boundary where the library belongs:
   - service result contracts
   - application manager boundaries
   - API endpoints that translate results into HTTP responses
2. Keep result creation and error mapping explicit instead of mixing exceptions, nulls, and ad-hoc tuples.
3. Pattern-match result objects at the boundary that converts them into user-facing responses.
4. Do not hide domain failures behind generic success wrappers.
5. Configure framework integration only where it removes manual translation:
   - `ConfigureCommunication()` for ASP.NET Core logging and converters
   - `WithCommunicationResults()` for Minimal API endpoint or group conversion
   - Orleans or SignalR packages only when those runtime filters are actually in use
6. Validate positive, negative, and error-path handling after integration.

```mermaid
flowchart LR
  A["Domain or service operation"] --> B["ManagedCode.Communication result"]
  B --> C["Application or API boundary"]
  C --> D["HTTP response or caller-visible contract"]
```

## Practical Usage

### Read path with explicit failures

```csharp
public sealed class OrderService(IOrderRepository orders)
{
    public async Task<Result<OrderDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);

        return order is null
            ? Result<OrderDto>.FailNotFound($"Order {id} was not found.")
            : Result<OrderDto>.Succeed(OrderDto.From(order));
    }
}
```

Use this shape when absence, validation, authorization, or domain rejection is an expected outcome. Reserve exceptions for unexpected infrastructure faults.

### Write path through Minimal APIs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureCommunication();

var app = builder.Build();

app.MapGroup("/orders")
   .WithCommunicationResults()
   .MapPost(string.Empty, async (CreateOrder command, OrderService orders, CancellationToken ct) =>
        await orders.CreateAsync(command, ct));
```

Handlers can return `Result` or `Result<T>` and let the extension package map failures to HTTP results and problem details at the API boundary.

### Compose validation and domain steps

```csharp
public async Task<Result<OrderReceipt>> CreateAsync(CreateOrder command, CancellationToken ct)
{
    var validation = Validate(command);
    if (validation.IsFailed)
    {
        return Result<OrderReceipt>.Fail(validation.Problem!);
    }

    return await Result<Order>.From(() => BuildOrder(command))
        .ThenAsync(order => SaveAsync(order, ct))
        .Then(order => Result<OrderReceipt>.Succeed(new OrderReceipt(order.Id, order.CreatedAt)));
}
```

Use railway-style composition when each step can return a result and the caller should stop at the first real failure.

## Options And Constraints

- `Result`, `Result<T>`, `CollectionResult<T>`, and `Problem` are the core surfaces. Keep them at service/API boundaries rather than leaking framework-specific HTTP results into domain code.
- `CollectionResult<T>` plus `PaginationRequest` / `PaginationOptions` should own paged API metadata instead of ad-hoc `(items, total)` tuples.
- Minimal APIs can use `WithCommunicationResults()` on one endpoint or an entire group. Prefer the group form only when every child endpoint follows the same result contract.
- `ManagedCode.Communication.Orleans` is for grain-call integration and serialization boundaries; use it with the Orleans skill when reviewing grain APIs.
- `v10.2.2` adds native `ICommandExecutor` reliability: bounded retry/backoff and jitter, whole-sequence timeouts, circuit breaking, rate limiting, idempotency, OpenTelemetry signals, and an `IHttpClientFactory` resilience handler. Keep one retry owner, honor `Retry-After`, and require idempotent side effects before enabling retries.
- `Task` and `ValueTask` result and railway paths are now symmetric, and primitive failures have dedicated coverage. Use `ValueTask` only for operations that frequently complete synchronously and are consumed once; test both success and failure shapes after upgrading.
- Orleans command execution can use distributed rate limiting and durable idempotency, but it requires the documented `commandStore` grain storage and explicit `UseOrleansCommandExecution()` configuration. Do not silently replace application-owned operation identity or retry policy.

## Deliver

- guidance on where explicit result objects improve clarity
- usage boundaries for translating results into API or caller responses
- validation expectations for success and failure flows
- package and integration choices for the actual runtime boundary

## Validate

- result handling is consistent across the boundary that uses the library
- callers do not fall back to exception-only logic for normal failure cases
- negative and error scenarios are documented and tested
- Minimal API endpoints return expected success and RFC 7807 failure shapes
- pagination metadata is stable when `CollectionResult<T>` is used
- Orleans or SignalR integration is covered by runtime-level tests when those packages are installed
