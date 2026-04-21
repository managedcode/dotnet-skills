# Architectural Anti-Patterns in .NET

This reference documents common architectural mistakes in .NET systems and how to avoid them. Examples use C# 12+ features including primary constructors.

---

## Over-Abstraction

### Problem: Unnecessary Interfaces

Creating interfaces for every class, even when there is only one implementation and no testing or extension need.

```csharp
// Anti-pattern: Interface for the sake of interface
public interface IOrderService
{
    Task<Order> GetOrderAsync(Guid id);
}

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task<Order> GetOrderAsync(Guid id)
        => await db.Orders.FindAsync(id);
}

// Then injected as:
services.AddScoped<IOrderService, OrderService>();
```

### Solution

Use interfaces when there is a real need: multiple implementations, testing seams, or abstraction over infrastructure.

```csharp
// Better: Direct class when no abstraction is needed
public sealed class OrderService(AppDbContext db)
{
    public async Task<Order?> GetOrderAsync(Guid id, CancellationToken ct = default)
        => await db.Orders.FindAsync([id], ct);
}

// Register directly:
services.AddScoped<OrderService>();

// Use interface when there is a reason:
// - Repository abstracts persistence (testable, swappable)
// - External service client abstracts HTTP (mockable)
// - Strategy pattern requires polymorphism
```

---

## Anemic Domain Model

### Problem: Logic in Services, Data in Entities

Entities become data bags while business logic lives in service classes, losing the benefits of encapsulation.

```csharp
// Anti-pattern: Anemic entity
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public string Status { get; set; } = "Draft";
    public decimal Total { get; set; }
}

// Logic scattered in service
public class OrderService(AppDbContext db)
{
    public async Task SubmitOrderAsync(Guid orderId)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstAsync(o => o.Id == orderId);

        if (order.Lines.Count == 0)
            throw new Exception("Cannot submit empty order");

        if (order.Status != "Draft")
            throw new Exception("Order already submitted");

        order.Status = "Submitted";
        order.Total = order.Lines.Sum(l => l.Quantity * l.UnitPrice);

        await db.SaveChangesAsync();
    }
}
```

### Solution

Encapsulate behavior in the entity. Keep invariants protected.

```csharp
// Better: Rich domain model
public sealed class Order
{
    public OrderId Id { get; }
    public CustomerId CustomerId { get; }
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public OrderStatus Status { get; private set; }
    public Money Total => _lines.Aggregate(Money.Zero, (sum, l) => sum + l.Subtotal);

    private Order(OrderId id, CustomerId customerId)
    {
        Id = id;
        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public static Order Create(CustomerId customerId)
        => new(OrderId.New(), customerId);

    public void Submit()
    {
        if (_lines.Count == 0)
            throw new DomainException("Cannot submit an empty order.");

        if (Status != OrderStatus.Draft)
            throw new DomainException("Only draft orders can be submitted.");

        Status = OrderStatus.Submitted;
    }
}
```

---

## Big Ball of Mud

### Problem: No Clear Boundaries

Everything depends on everything. Controllers call repositories directly. Business logic lives in controllers. Database models leak into API responses.

```csharp
// Anti-pattern: Controller doing everything
[ApiController]
public class OrderController(AppDbContext db, IEmailService email) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(OrderDto dto)
    {
        // Validation mixed with persistence
        if (dto.Lines.Count == 0)
            return BadRequest("Order must have lines");

        var customer = await db.Customers.FindAsync(dto.CustomerId);
        if (customer == null)
            return NotFound("Customer not found");

        // Business logic in controller
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            Lines = dto.Lines.Select(l => new OrderLine
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }).ToList(),
            Status = "Created",
            Total = dto.Lines.Sum(l => l.Quantity * l.UnitPrice)
        };

        // Infrastructure concern in controller
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Side effect mixed in
        await email.SendOrderConfirmationAsync(customer.Email, order.Id);

        // Database entity leaked to response
        return Ok(order);
    }
}
```

### Solution

Separate concerns into layers or slices with clear responsibilities.

```csharp
// Better: Clear separation
[ApiController]
public class OrderController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateOrderCommand(request), ct);

        return result.Match(
            success: id => CreatedAtAction(nameof(GetOrder), new { id }, new { id }),
            failure: errors => BadRequest(errors));
    }
}

// Handler owns the use case
public sealed class CreateOrderHandler(
    IOrderRepository orders,
    ICustomerRepository customers,
    IEventPublisher events) : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(command.CustomerId, ct);
        if (customer is null)
            return Result<Guid>.NotFound("Customer not found.");

        var order = Order.Create(customer.Id);

        foreach (var line in command.Lines)
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);

        await orders.AddAsync(order, ct);
        await orders.SaveChangesAsync(ct);

        await events.PublishAsync(new OrderCreatedEvent(order.Id, customer.Id), ct);

        return Result<Guid>.Success(order.Id.Value);
    }
}
```

---

## Premature Microservices

### Problem: Distributed Monolith

Splitting into microservices before understanding domain boundaries. Services are tightly coupled, require synchronous calls, and share databases.

```csharp
// Anti-pattern: Synchronous cross-service calls in critical path
public sealed class OrderService(
    HttpClient customerClient,
    HttpClient inventoryClient,
    HttpClient pricingClient)
{
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // Synchronous dependency on customer service
        var customer = await customerClient.GetFromJsonAsync<CustomerDto>(
            $"/customers/{request.CustomerId}", ct);

        if (customer is null)
            throw new Exception("Customer not found");

        // Synchronous dependency on inventory
        foreach (var item in request.Items)
        {
            var available = await inventoryClient.GetFromJsonAsync<bool>(
                $"/inventory/{item.ProductId}/available?quantity={item.Quantity}", ct);

            if (!available)
                throw new Exception($"Product {item.ProductId} not available");
        }

        // Synchronous dependency on pricing
        var prices = await pricingClient.PostAsJsonAsync("/pricing/calculate", request.Items, ct);

        // If any service is down, order creation fails completely
        // ...
    }
}
```

### Solution

Start with a modular monolith. Extract services only when there is a clear ownership, scale, or deployment boundary need.

```csharp
// Better: Modular monolith with clear contracts
public sealed class OrderService(
    ISalesModule sales,
    IInventoryModule inventory,
    IEventBus events)
{
    public async Task<Result<Guid>> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // In-process call to customer module
        var customer = await sales.GetCustomerAsync(request.CustomerId, ct);
        if (customer is null)
            return Result<Guid>.NotFound("Customer not found.");

        // Reserve inventory (can be made async with events if needed)
        var reservation = await inventory.ReserveAsync(request.Items, ct);
        if (!reservation.IsSuccess)
            return Result<Guid>.Failure("Inventory unavailable.");

        var order = Order.Create(customer.Id, reservation.Items);
        await sales.SaveOrderAsync(order, ct);

        // Async event for downstream processing
        await events.PublishAsync(new OrderCreatedEvent(order.Id), ct);

        return Result<Guid>.Success(order.Id.Value);
    }
}

// Module boundary is explicit and can be extracted later
public interface IInventoryModule
{
    Task<ReservationResult> ReserveAsync(IEnumerable<OrderItem> items, CancellationToken ct);
    Task ReleaseReservationAsync(Guid reservationId, CancellationToken ct);
}
```

---

## Repository Explosion

### Problem: One Repository Per Entity

Creating a repository for every entity, leading to dozens of nearly identical classes with slight variations.

```csharp
// Anti-pattern: Repository per entity
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task AddAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Guid id);
}

public interface IOrderLineRepository
{
    Task<OrderLine?> GetByIdAsync(Guid id);
    Task AddAsync(OrderLine line);
    // ... same methods
}

public interface ICustomerRepository { /* same pattern */ }
public interface IProductRepository { /* same pattern */ }
// ... 30 more repositories
```

### Solution

Repository per aggregate root, not per entity. Use the DbContext directly for simple queries.

```csharp
// Better: Repository per aggregate
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<Order?> GetWithLinesAsync(OrderId id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// Order is the aggregate root; OrderLine is part of the aggregate
// No separate OrderLineRepository needed

// For simple read queries, use DbContext directly or a query service
public sealed class OrderQueryService(AppDbContext db)
{
    public async Task<List<OrderSummaryDto>> GetRecentOrdersAsync(
        CustomerId customerId,
        int count = 10,
        CancellationToken ct = default)
    {
        return await db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .Select(o => new OrderSummaryDto(o.Id, o.Status, o.Total, o.CreatedAt))
            .ToListAsync(ct);
    }
}
```

---

## God Object / God Service

### Problem: One Class Does Everything

A single service class grows to handle all operations for a domain area, becoming unmaintainable.

```csharp
// Anti-pattern: God service
public class OrderService(
    AppDbContext db,
    IEmailService email,
    IPaymentGateway payments,
    IInventoryService inventory,
    IShippingService shipping,
    IPricingEngine pricing,
    IDiscountCalculator discounts,
    ITaxCalculator taxes,
    ILogger<OrderService> logger)
{
    public async Task<Order> CreateOrderAsync(...) { /* 200 lines */ }
    public async Task SubmitOrderAsync(...) { /* 150 lines */ }
    public async Task CancelOrderAsync(...) { /* 100 lines */ }
    public async Task RefundOrderAsync(...) { /* 180 lines */ }
    public async Task UpdateShippingAsync(...) { /* 80 lines */ }
    public async Task ApplyDiscountAsync(...) { /* 90 lines */ }
    public async Task RecalculateTotalsAsync(...) { /* 120 lines */ }
    // ... 20 more methods, 3000+ lines total
}
```

### Solution

Split by use case or subdomain. Use vertical slices or focused handlers.

```csharp
// Better: Focused handlers per use case
public sealed class CreateOrderHandler(
    IOrderRepository orders,
    ICustomerRepository customers) : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // Focused, testable, ~50 lines
    }
}

public sealed class SubmitOrderHandler(
    IOrderRepository orders,
    IPaymentService payments,
    IEventPublisher events) : IRequestHandler<SubmitOrderCommand, Result>
{
    public async Task<Result> Handle(SubmitOrderCommand command, CancellationToken ct)
    {
        // Focused, testable, ~60 lines
    }
}

public sealed class CancelOrderHandler(
    IOrderRepository orders,
    IRefundService refunds,
    IInventoryService inventory) : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand command, CancellationToken ct)
    {
        // Focused, testable, ~70 lines
    }
}
```

---

## Leaky Abstractions

### Problem: Infrastructure Concerns Leak Upward

Database implementation details, HTTP concerns, or framework specifics appear in business logic.

```csharp
// Anti-pattern: EF Core specifics in domain
public class Order
{
    // EF navigation property concerns in domain entity
    public virtual Customer Customer { get; set; }
    public virtual ICollection<OrderLine> Lines { get; set; }

    // EF change tracking awareness
    public void AddLine(OrderLine line)
    {
        Lines ??= new List<OrderLine>();
        Lines.Add(line);
    }
}

// Anti-pattern: HTTP concerns in application layer
public sealed class OrderHandler(HttpClient http)
{
    public async Task<Order> GetOrderAsync(Guid id)
    {
        var response = await http.GetAsync($"/api/orders/{id}");

        // HTTP status codes in business logic
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new OrderNotFoundException(id);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedException();

        // JSON parsing in handler
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Order>(json);
    }
}
```

### Solution

Keep infrastructure concerns in infrastructure layer. Use domain-appropriate abstractions.

```csharp
// Better: Clean domain entity
public sealed class Order
{
    public OrderId Id { get; }
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public void AddLine(ProductId productId, Quantity quantity, Money unitPrice)
    {
        _lines.Add(new OrderLine(productId, quantity, unitPrice));
    }
}

// Better: Infrastructure handles HTTP, exposes domain result
public sealed class OrderServiceClient(HttpClient http) : IOrderServiceClient
{
    public async Task<Result<Order>> GetOrderAsync(OrderId id, CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"/api/orders/{id.Value}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Result<Order>.NotFound();

            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<OrderDto>(ct);
            return Result<Order>.Success(dto!.ToDomain());
        }
        catch (HttpRequestException)
        {
            return Result<Order>.Failure("Order service unavailable.");
        }
    }
}

// Application layer uses domain abstractions
public sealed class OrderQueryHandler(IOrderServiceClient client)
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery query, CancellationToken ct)
    {
        var result = await client.GetOrderAsync(query.OrderId, ct);
        return result.Map(order => OrderDto.FromDomain(order));
    }
}
```

---

## Cargo Cult Architecture

### Problem: Copying Patterns Without Understanding

Implementing patterns because "that's how it's done" without understanding the problem they solve.

```csharp
// Anti-pattern: CQRS + Event Sourcing for a simple CRUD app
public sealed class UpdateCustomerEmailHandler(
    IEventStore eventStore,
    IEventBus eventBus,
    IReadModelProjector projector)
{
    public async Task Handle(UpdateCustomerEmailCommand command, CancellationToken ct)
    {
        // Load entire event history
        var events = await eventStore.GetEventsAsync(command.CustomerId, ct);
        var customer = Customer.FromHistory(events);

        // Apply change
        customer.UpdateEmail(command.NewEmail);

        // Save event
        var newEvent = new CustomerEmailUpdatedEvent(command.CustomerId, command.NewEmail);
        await eventStore.AppendAsync(command.CustomerId, newEvent, ct);

        // Publish for projections
        await eventBus.PublishAsync(newEvent, ct);

        // Update read model
        await projector.ProjectAsync(newEvent, ct);
    }
}

// All this for: UPDATE Customers SET Email = @Email WHERE Id = @Id
```

### Solution

Match architecture complexity to problem complexity. Simple problems deserve simple solutions.

```csharp
// Better: Simple solution for simple problem
public sealed class UpdateCustomerEmailHandler(
    AppDbContext db,
    IValidator<UpdateCustomerEmailCommand> validator) : IRequestHandler<UpdateCustomerEmailCommand, Result>
{
    public async Task<Result> Handle(UpdateCustomerEmailCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Invalid(validation.Errors);

        var customer = await db.Customers.FindAsync([command.CustomerId], ct);
        if (customer is null)
            return Result.NotFound();

        customer.Email = command.NewEmail;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// Use CQRS/Event Sourcing when you actually need:
// - Audit trail of all changes
// - Temporal queries (state at point in time)
// - Complex event-driven workflows
// - Different read/write scaling requirements
```

---

## Missing Error Handling Strategy

### Problem: Inconsistent Error Handling

Errors handled differently across the codebase. Some throw exceptions, some return null, some return result objects.

```csharp
// Anti-pattern: Inconsistent error handling
public class OrderService
{
    public async Task<Order> GetOrderAsync(Guid id)
    {
        var order = await db.Orders.FindAsync(id);
        return order; // Returns null, caller must check
    }

    public async Task SubmitOrderAsync(Guid id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order == null)
            throw new NotFoundException("Order not found"); // Throws exception
    }

    public async Task<bool> CancelOrderAsync(Guid id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order == null)
            return false; // Returns bool

        order.Cancel();
        await db.SaveChangesAsync();
        return true;
    }
}
```

### Solution

Adopt a consistent result pattern across the application.

```csharp
// Better: Consistent result type
public readonly struct Result<T>
{
    public T? Value { get; }
    public ResultError? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(ResultError error) => Error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string message) => new(new ResultError(message));
    public static Result<T> NotFound(string? message = null) => new(new ResultError(message ?? "Not found", ResultErrorType.NotFound));

    public TResult Match<TResult>(Func<T, TResult> success, Func<ResultError, TResult> failure)
        => IsSuccess ? success(Value!) : failure(Error!);
}

// Consistent usage across handlers
public sealed class GetOrderHandler(IOrderRepository orders) : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery query, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(query.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.NotFound();

        return Result<OrderDto>.Success(OrderDto.FromDomain(order));
    }
}

public sealed class SubmitOrderHandler(IOrderRepository orders) : IRequestHandler<SubmitOrderCommand, Result>
{
    public async Task<Result> Handle(SubmitOrderCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct);
        if (order is null)
            return Result.NotFound();

        var submitResult = order.Submit();
        if (!submitResult.IsSuccess)
            return submitResult;

        await orders.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

---

## Summary: Pattern Selection Guidance

| Smell | Question to Ask | If Yes | If No |
|-------|-----------------|--------|-------|
| Adding interface for every class | Is there more than one implementation or a testing need? | Keep interface | Remove interface |
| Logic in services, data in entities | Are there business invariants to protect? | Move logic to entity | Service is fine for orchestration |
| Considering microservices | Do teams need independent deployment? Different scale needs? | Consider microservices | Stay with modular monolith |
| Adding event sourcing | Need audit trail, temporal queries, or complex workflows? | Event sourcing may help | Simple persistence is fine |
| Repository per entity | Is this entity an aggregate root? | Repository is appropriate | Access through aggregate root |
| God service growing | Does this class have multiple reasons to change? | Split by use case | Keep if cohesive |

---

## References

- [Anemic Domain Model by Martin Fowler](https://martinfowler.com/bliki/AnemicDomainModel.html)
- [Big Ball of Mud by Brian Foote and Joseph Yoder](http://www.laputan.org/mud/)
- [Monolith First by Martin Fowler](https://martinfowler.com/bliki/MonolithFirst.html)
- [Don't Start with Microservices by Sam Newman](https://samnewman.io/blog/2015/04/07/microservices-for-greenfield/)
