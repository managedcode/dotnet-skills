# Architectural Patterns for .NET

This reference covers practical implementations of common architectural patterns in modern .NET, using C# 12+ features including primary constructors.

---

## Clean Architecture

Clean Architecture organizes code into concentric layers with dependencies pointing inward. The domain layer has no external dependencies; infrastructure concerns live at the outer edge.

### Layer Structure

```text
src/
  Domain/           # Entities, value objects, domain events, interfaces
  Application/      # Use cases, DTOs, validators, command/query handlers
  Infrastructure/   # EF Core, external APIs, messaging, file storage
  WebApi/           # Controllers, middleware, composition root
```

### Domain Layer

```csharp
// Domain/Entities/Order.cs
public sealed class Order
{
    public OrderId Id { get; }
    public CustomerId CustomerId { get; }
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public OrderStatus Status { get; private set; }
    public Money Total => _lines.Aggregate(Money.Zero, (sum, line) => sum + line.Subtotal);

    private Order(OrderId id, CustomerId customerId)
    {
        Id = id;
        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public static Order Create(CustomerId customerId)
        => new(OrderId.New(), customerId);

    public void AddLine(Product product, Quantity quantity)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot modify a submitted order.");

        var existing = _lines.Find(l => l.ProductId == product.Id);
        if (existing is not null)
            existing.IncreaseQuantity(quantity);
        else
            _lines.Add(new OrderLine(product.Id, product.Price, quantity));
    }

    public void Submit()
    {
        if (_lines.Count == 0)
            throw new DomainException("Cannot submit an empty order.");

        Status = OrderStatus.Submitted;
    }
}

// Domain/ValueObjects/Money.cs
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero => new(0, "USD");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Currency mismatch.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }
}

// Domain/Interfaces/IOrderRepository.cs
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### Application Layer

```csharp
// Application/Orders/Commands/SubmitOrder/SubmitOrderCommand.cs
public sealed record SubmitOrderCommand(Guid OrderId) : IRequest<Result>;

// Application/Orders/Commands/SubmitOrder/SubmitOrderHandler.cs
public sealed class SubmitOrderHandler(
    IOrderRepository orders,
    IEventPublisher events) : IRequestHandler<SubmitOrderCommand, Result>
{
    public async Task<Result> Handle(SubmitOrderCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(new OrderId(command.OrderId), ct);
        if (order is null)
            return Result.NotFound("Order not found.");

        order.Submit();
        await orders.SaveChangesAsync(ct);
        await events.PublishAsync(new OrderSubmittedEvent(order.Id, order.Total), ct);

        return Result.Success();
    }
}
```

### Infrastructure Layer

```csharp
// Infrastructure/Persistence/OrderRepository.cs
public sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
        => await db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await db.Orders.AddAsync(order, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
```

---

## Vertical Slice Architecture

Vertical slices organize code by feature instead of by layer. Each slice owns its request, handler, validator, and data access. This reduces cross-cutting changes and keeps related code together.

### Folder Structure

```text
src/
  Features/
    Orders/
      CreateOrder/
        CreateOrderEndpoint.cs
        CreateOrderHandler.cs
        CreateOrderRequest.cs
        CreateOrderValidator.cs
      GetOrder/
        GetOrderEndpoint.cs
        GetOrderHandler.cs
    Products/
      ...
  Shared/
    Infrastructure/
    Extensions/
```

### Feature Implementation

```csharp
// Features/Orders/CreateOrder/CreateOrderRequest.cs
public sealed record CreateOrderRequest(
    Guid CustomerId,
    List<OrderLineDto> Lines);

public sealed record OrderLineDto(Guid ProductId, int Quantity);

// Features/Orders/CreateOrder/CreateOrderValidator.cs
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

// Features/Orders/CreateOrder/CreateOrderHandler.cs
public sealed class CreateOrderHandler(
    AppDbContext db,
    IValidator<CreateOrderRequest> validator) : IRequestHandler<CreateOrderRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<Guid>.Invalid(validation.Errors);

        var order = Order.Create(new CustomerId(request.CustomerId));

        foreach (var line in request.Lines)
        {
            var product = await db.Products.FindAsync([line.ProductId], ct);
            if (product is null)
                return Result<Guid>.NotFound($"Product {line.ProductId} not found.");

            order.AddLine(product, new Quantity(line.Quantity));
        }

        await db.Orders.AddAsync(order, ct);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(order.Id.Value);
    }
}

// Features/Orders/CreateOrder/CreateOrderEndpoint.cs
public static class CreateOrderEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders", async (
            CreateOrderRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(request, ct);
            return result.Match(
                success: id => Results.Created($"/orders/{id}", new { id }),
                failure: errors => Results.BadRequest(errors));
        })
        .WithName("CreateOrder")
        .WithTags("Orders")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem();
}
```

---

## Domain-Driven Design (DDD)

DDD applies when business rules are complex and need explicit modeling. Avoid forcing DDD into simple CRUD scenarios.

### Aggregate Root

```csharp
// Domain/Aggregates/Booking/Booking.cs
public sealed class Booking : AggregateRoot<BookingId>
{
    public RoomId RoomId { get; }
    public GuestId GuestId { get; }
    public DateRange Period { get; private set; }
    public BookingStatus Status { get; private set; }

    private Booking(BookingId id, RoomId roomId, GuestId guestId, DateRange period)
        : base(id)
    {
        RoomId = roomId;
        GuestId = guestId;
        Period = period;
        Status = BookingStatus.Pending;
    }

    public static Booking Create(RoomId roomId, GuestId guestId, DateRange period)
    {
        var booking = new Booking(BookingId.New(), roomId, guestId, period);
        booking.AddDomainEvent(new BookingCreatedEvent(booking.Id, roomId, period));
        return booking;
    }

    public Result Confirm()
    {
        if (Status != BookingStatus.Pending)
            return Result.Failure("Only pending bookings can be confirmed.");

        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmedEvent(Id));
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == BookingStatus.Cancelled)
            return Result.Failure("Booking is already cancelled.");

        if (Status == BookingStatus.Confirmed && Period.Start <= DateTime.UtcNow.AddDays(1))
            return Result.Failure("Cannot cancel confirmed booking within 24 hours.");

        Status = BookingStatus.Cancelled;
        AddDomainEvent(new BookingCancelledEvent(Id, reason));
        return Result.Success();
    }
}

// Domain/ValueObjects/DateRange.cs
public readonly record struct DateRange(DateTime Start, DateTime End)
{
    public int Nights => (End - Start).Days;

    public bool Overlaps(DateRange other)
        => Start < other.End && End > other.Start;

    public static DateRange Create(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new DomainException("End date must be after start date.");
        return new DateRange(start, end);
    }
}
```

### Domain Events

```csharp
// Domain/Events/BookingConfirmedEvent.cs
public sealed record BookingConfirmedEvent(BookingId BookingId) : IDomainEvent;

// Application/Handlers/BookingConfirmedHandler.cs
public sealed class BookingConfirmedHandler(
    IEmailService email,
    IBookingRepository bookings) : IDomainEventHandler<BookingConfirmedEvent>
{
    public async Task Handle(BookingConfirmedEvent @event, CancellationToken ct)
    {
        var booking = await bookings.GetByIdAsync(@event.BookingId, ct);
        if (booking is null) return;

        await email.SendConfirmationAsync(booking.GuestId, booking.Period, ct);
    }
}
```

### Domain Service

```csharp
// Domain/Services/BookingPolicyService.cs
public sealed class BookingPolicyService(IRoomRepository rooms)
{
    public async Task<Result> CanBookAsync(
        RoomId roomId,
        DateRange period,
        CancellationToken ct = default)
    {
        var room = await rooms.GetByIdAsync(roomId, ct);
        if (room is null)
            return Result.Failure("Room not found.");

        if (!room.IsAvailable)
            return Result.Failure("Room is not available for booking.");

        var conflicts = await rooms.GetOverlappingBookingsAsync(roomId, period, ct);
        if (conflicts.Count > 0)
            return Result.Failure("Room is already booked for the requested period.");

        if (period.Nights > room.MaxStayNights)
            return Result.Failure($"Maximum stay is {room.MaxStayNights} nights.");

        return Result.Success();
    }
}
```

---

## CQRS (Command Query Responsibility Segregation)

CQRS separates write operations (commands) from read operations (queries). Apply it when read and write models have different optimization needs.

### Command Side

```csharp
// Application/Commands/PlaceOrder/PlaceOrderCommand.cs
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    List<OrderItemDto> Items) : ICommand<Result<Guid>>;

// Application/Commands/PlaceOrder/PlaceOrderHandler.cs
public sealed class PlaceOrderHandler(
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IEventBus events) : ICommandHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PlaceOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(new CustomerId(command.CustomerId));

        foreach (var item in command.Items)
        {
            order.AddItem(new ProductId(item.ProductId), item.Quantity, item.UnitPrice);
        }

        await orders.AddAsync(order, ct);
        await unitOfWork.CommitAsync(ct);

        await events.PublishAsync(new OrderPlacedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.Total.Amount), ct);

        return Result<Guid>.Success(order.Id.Value);
    }
}
```

### Query Side with Dedicated Read Model

```csharp
// Application/Queries/GetOrderSummary/GetOrderSummaryQuery.cs
public sealed record GetOrderSummaryQuery(Guid OrderId) : IQuery<OrderSummaryDto?>;

// Application/Queries/GetOrderSummary/OrderSummaryDto.cs
public sealed record OrderSummaryDto(
    Guid Id,
    string CustomerName,
    DateTime OrderDate,
    string Status,
    decimal Total,
    List<OrderItemSummaryDto> Items);

public sealed record OrderItemSummaryDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

// Application/Queries/GetOrderSummary/GetOrderSummaryHandler.cs
public sealed class GetOrderSummaryHandler(
    IDbConnection db) : IQueryHandler<GetOrderSummaryQuery, OrderSummaryDto?>
{
    public async Task<OrderSummaryDto?> Handle(GetOrderSummaryQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, c.Name AS CustomerName, o.OrderDate, o.Status, o.Total
            FROM Orders o
            JOIN Customers c ON o.CustomerId = c.Id
            WHERE o.Id = @OrderId;

            SELECT oi.ProductName, oi.Quantity, oi.UnitPrice, oi.Subtotal
            FROM OrderItems oi
            WHERE oi.OrderId = @OrderId;
            """;

        await using var multi = await db.QueryMultipleAsync(sql, new { query.OrderId });

        var order = await multi.ReadSingleOrDefaultAsync<OrderSummaryDto>();
        if (order is null) return null;

        var items = (await multi.ReadAsync<OrderItemSummaryDto>()).ToList();
        return order with { Items = items };
    }
}
```

### Event Sourcing Integration

```csharp
// Infrastructure/EventStore/OrderEventStore.cs
public sealed class OrderEventStore(EventStoreClient client) : IOrderEventStore
{
    public async Task AppendAsync(OrderId id, IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        var streamName = $"order-{id.Value}";
        var eventData = events.Select(e => new EventData(
            Uuid.NewUuid(),
            e.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(e),
            null)).ToArray();

        await client.AppendToStreamAsync(streamName, StreamState.Any, eventData, cancellationToken: ct);
    }

    public async Task<Order> RehydrateAsync(OrderId id, CancellationToken ct)
    {
        var streamName = $"order-{id.Value}";
        var events = new List<IDomainEvent>();

        await foreach (var resolved in client.ReadStreamAsync(
            Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: ct))
        {
            var eventType = Type.GetType($"Domain.Events.{resolved.Event.EventType}");
            var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(
                resolved.Event.Data.Span, eventType!)!;
            events.Add(domainEvent);
        }

        return Order.FromHistory(id, events);
    }
}
```

---

## Modular Monolith

A modular monolith keeps code in one deployable unit while enforcing module boundaries. Modules communicate through defined contracts, enabling future extraction.

### Module Structure

```text
src/
  Modules/
    Sales/
      Sales.Api/
      Sales.Application/
      Sales.Domain/
      Sales.Infrastructure/
      Sales.Contracts/          # Public contracts other modules can depend on
    Inventory/
      Inventory.Api/
      Inventory.Application/
      Inventory.Domain/
      Inventory.Infrastructure/
      Inventory.Contracts/
  Host/                         # Composition root, startup, shared infrastructure
```

### Module Contracts

```csharp
// Modules/Sales/Sales.Contracts/Events/OrderPlacedIntegrationEvent.cs
public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    List<OrderedProduct> Products,
    DateTime OccurredAt) : IIntegrationEvent;

public sealed record OrderedProduct(Guid ProductId, int Quantity);

// Modules/Sales/Sales.Contracts/Services/ISalesModule.cs
public interface ISalesModule
{
    Task<OrderSummaryDto?> GetOrderSummaryAsync(Guid orderId, CancellationToken ct = default);
}
```

### Inter-Module Communication

```csharp
// Modules/Inventory/Inventory.Application/Handlers/OrderPlacedHandler.cs
public sealed class OrderPlacedHandler(
    IInventoryRepository inventory,
    IUnitOfWork unitOfWork) : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task Handle(OrderPlacedIntegrationEvent @event, CancellationToken ct)
    {
        foreach (var product in @event.Products)
        {
            var item = await inventory.GetByProductIdAsync(product.ProductId, ct);
            if (item is null) continue;

            item.Reserve(product.Quantity);
        }

        await unitOfWork.CommitAsync(ct);
    }
}

// Host/Program.cs - Module registration
builder.Services
    .AddSalesModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddSharedInfrastructure(builder.Configuration);
```

---

## Microservices Boundaries

When microservices are justified, define clear ownership and communication patterns.

### Service Contract

```csharp
// Contracts/OrderService/IOrderServiceClient.cs
public interface IOrderServiceClient
{
    Task<OrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<Guid> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);
}

// Infrastructure/Clients/OrderServiceClient.cs
public sealed class OrderServiceClient(
    HttpClient http,
    ILogger<OrderServiceClient> logger) : IOrderServiceClient
{
    public async Task<OrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<OrderDto>($"/orders/{orderId}", ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Guid> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/orders", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(ct);
        return result!.OrderId;
    }
}
```

### Saga / Process Manager

```csharp
// Application/Sagas/OrderFulfillmentSaga.cs
public sealed class OrderFulfillmentSaga(
    IOrderRepository orders,
    IInventoryServiceClient inventory,
    IPaymentServiceClient payments,
    IShippingServiceClient shipping,
    IEventBus events) : ISaga<OrderFulfillmentState>
{
    public async Task<OrderFulfillmentState> HandleAsync(
        OrderPlacedEvent @event,
        OrderFulfillmentState state,
        CancellationToken ct)
    {
        state = state with { OrderId = @event.OrderId, Status = FulfillmentStatus.Started };

        // Reserve inventory
        var reserved = await inventory.ReserveAsync(@event.OrderId, @event.Items, ct);
        if (!reserved.IsSuccess)
        {
            await CompensateAsync(state, ct);
            return state with { Status = FulfillmentStatus.Failed, FailureReason = "Inventory unavailable" };
        }
        state = state with { InventoryReserved = true };

        // Process payment
        var paid = await payments.ChargeAsync(@event.OrderId, @event.Total, ct);
        if (!paid.IsSuccess)
        {
            await CompensateAsync(state, ct);
            return state with { Status = FulfillmentStatus.Failed, FailureReason = "Payment failed" };
        }
        state = state with { PaymentProcessed = true };

        // Create shipment
        var shipment = await shipping.CreateShipmentAsync(@event.OrderId, @event.ShippingAddress, ct);
        state = state with { ShipmentId = shipment.Id, Status = FulfillmentStatus.Completed };

        await events.PublishAsync(new OrderFulfilledEvent(@event.OrderId, shipment.Id), ct);
        return state;
    }

    private async Task CompensateAsync(OrderFulfillmentState state, CancellationToken ct)
    {
        if (state.PaymentProcessed)
            await payments.RefundAsync(state.OrderId, ct);

        if (state.InventoryReserved)
            await inventory.ReleaseReservationAsync(state.OrderId, ct);

        await orders.MarkFailedAsync(state.OrderId, state.FailureReason, ct);
    }
}
```

---

## Pattern Selection Guide

| Scenario | Recommended Pattern |
|----------|---------------------|
| New project, small team, unclear requirements | Simple layered or modular monolith |
| Complex domain with many business rules | DDD with aggregates and domain events |
| High read/write ratio disparity | CQRS with separate read models |
| Independent team deployments required | Microservices with clear contracts |
| Rapid feature development focus | Vertical slices |
| Need future extraction flexibility | Modular monolith with explicit contracts |

---

## References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Vertical Slice Architecture by Jimmy Bogard](https://www.jimmybogard.com/vertical-slice-architecture/)
- [Domain-Driven Design Reference by Eric Evans](https://www.domainlanguage.com/ddd/reference/)
- [CQRS by Martin Fowler](https://martinfowler.com/bliki/CQRS.html)
- [Microsoft .NET Application Architecture Guide](https://learn.microsoft.com/en-us/dotnet/architecture/)
