# EF Core Anti-Patterns

## N+1 Query Problem

### The Problem

Loading related data in a loop causes one query per iteration:

```csharp
// BAD: N+1 queries - 1 for orders + N for items
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    // Each iteration executes a new query
    order.Items = await db.OrderItems
        .Where(i => i.OrderId == order.Id)
        .ToListAsync();
}
```

### The Solution

Use eager loading, projection, or explicit loading:

```csharp
// GOOD: Single query with Include
var orders = await db.Orders
    .Include(o => o.Items)
    .ToListAsync();

// GOOD: Split query for large graphs
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSplitQuery()
    .ToListAsync();

// GOOD: Projection to DTO
var orderDtos = await db.Orders
    .Select(o => new OrderDto
    {
        Id = o.Id,
        Items = o.Items.Select(i => new OrderItemDto { ... }).ToList()
    })
    .ToListAsync();
```

## Large DbContext with Too Many DbSets

### The Problem

A single DbContext with dozens of DbSets becomes hard to maintain and test:

```csharp
// BAD: Monolithic context
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Inventory> Inventory { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    // ... 50 more DbSets
}
```

### The Solution

Split into bounded contexts:

```csharp
// GOOD: Bounded contexts
public class OrderingDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
}

public class InventoryDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<StockLevel> StockLevels { get; set; }
}
```

## Loading Full Entities When You Need Subsets

### The Problem

Fetching entire entity graphs when only a few properties are needed:

```csharp
// BAD: Loads everything including large blobs
var products = await db.Products
    .Include(p => p.Images)
    .Include(p => p.Reviews)
    .Include(p => p.Specifications)
    .ToListAsync();

// Then only using name and price
var displayList = products.Select(p => $"{p.Name}: {p.Price}");
```

### The Solution

Project to DTOs or anonymous types:

```csharp
// GOOD: Only fetch what you need
var products = await db.Products
    .Select(p => new { p.Name, p.Price })
    .ToListAsync();

var displayList = products.Select(p => $"{p.Name}: {p.Price}");
```

## Client-Side Evaluation Without Awareness

### The Problem

Filtering happens in memory instead of database:

```csharp
// BAD: Custom method forces client evaluation
var activeProducts = await db.Products
    .Where(p => IsProductActive(p))  // Cannot translate to SQL
    .ToListAsync();

// BAD: Complex string operations may not translate
var products = await db.Products
    .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
    .ToListAsync();
```

### The Solution

Use translatable expressions or explicit client evaluation:

```csharp
// GOOD: Use EF.Functions for database operations
var products = await db.Products
    .Where(p => EF.Functions.Like(p.Name, $"%{searchTerm}%"))
    .ToListAsync();

// GOOD: Explicit about client evaluation
var allProducts = await db.Products.ToListAsync();
var activeProducts = allProducts.Where(p => IsProductActive(p));
```

## SaveChanges in Loops

### The Problem

Calling SaveChanges repeatedly causes many database roundtrips:

```csharp
// BAD: N roundtrips
foreach (var product in products)
{
    product.Price *= 1.1m;
    await db.SaveChangesAsync();  // Roundtrip each iteration
}
```

### The Solution

Batch changes and save once:

```csharp
// GOOD: Single roundtrip
foreach (var product in products)
{
    product.Price *= 1.1m;
}
await db.SaveChangesAsync();

// BETTER: Use ExecuteUpdate for bulk operations (EF Core 7+)
await db.Products
    .Where(p => p.Category == category)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => p.Price * 1.1m));
```

## Incorrect DbContext Lifetime

### The Problem

Long-lived or singleton DbContext causes memory leaks and stale data:

```csharp
// BAD: Singleton - accumulates tracked entities
services.AddSingleton<AppDbContext>();

// BAD: Static or field-level context
public class ProductService
{
    private static readonly AppDbContext _db = new AppDbContext();
}
```

### The Solution

Use scoped lifetime aligned with unit of work:

```csharp
// GOOD: Scoped lifetime (default for AddDbContext)
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// GOOD: Pooled for better performance
services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// For background services, create scope explicitly
public class BackgroundProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Use db within this scope
    }
}
```

## Generic Repository Anti-Pattern

### The Problem

Wrapping DbContext in a generic repository hides EF Core's power:

```csharp
// BAD: Generic repository loses query composition
public interface IRepository<T>
{
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

// Usage - can't compose queries efficiently
var products = await _productRepo.GetAllAsync();
var filtered = products.Where(p => p.Price > 100);  // Client-side!
```

### The Solution

Use DbContext directly or create specific query methods:

```csharp
// GOOD: Direct DbContext usage
var products = await db.Products
    .Where(p => p.Price > 100)
    .OrderBy(p => p.Name)
    .Take(20)
    .ToListAsync();

// GOOD: Specific repository with meaningful methods
public class ProductRepository
{
    private readonly AppDbContext _db;

    public async Task<List<Product>> GetExpensiveProductsAsync(decimal minPrice)
    {
        return await _db.Products
            .Where(p => p.Price >= minPrice)
            .OrderByDescending(p => p.Price)
            .ToListAsync();
    }
}
```

## Ignoring Query Translation

### The Problem

Not verifying that LINQ translates to efficient SQL:

```csharp
// May not translate as expected
var results = await db.Products
    .Where(p => SomeComplexMethod(p))
    .ToListAsync();
// Could load ALL products to memory!
```

### The Solution

Enable logging and verify SQL:

```csharp
// In development
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors()
           .LogTo(Console.WriteLine, LogLevel.Information));

// In tests - use ToQueryString()
var query = db.Products.Where(p => p.Price > 100);
var sql = query.ToQueryString();
Console.WriteLine(sql);
```

## Lazy Loading Without Understanding

### The Problem

Enabling lazy loading without realizing the N+1 implications:

```csharp
// Configuration enables lazy loading
services.AddDbContext<AppDbContext>(o => o.UseLazyLoadingProxies());

// BAD: Hidden N+1 queries in views/serialization
@foreach (var order in orders)
{
    <p>@order.Customer.Name</p>  // Query per iteration!
    @foreach (var item in order.Items)  // Another query per order!
    {
        <p>@item.Product.Name</p>  // Yet another query!
    }
}
```

### The Solution

Disable lazy loading and use explicit strategies:

```csharp
// GOOD: Explicit eager loading for known needs
var orders = await db.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
    .ToListAsync();

// Or project to view model
var orderViews = await db.Orders
    .Select(o => new OrderViewModel
    {
        CustomerName = o.Customer.Name,
        Items = o.Items.Select(i => new ItemViewModel
        {
            ProductName = i.Product.Name
        }).ToList()
    })
    .ToListAsync();
```

## Missing Indexes

### The Problem

Queries filter on columns without indexes:

```csharp
// If no index on Email, this scans entire table
var user = await db.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// Composite filter without composite index
var orders = await db.Orders
    .Where(o => o.CustomerId == customerId && o.Status == status)
    .ToListAsync();
```

### The Solution

Add indexes for queried columns:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Composite index matches query pattern
        builder.HasIndex(o => new { o.CustomerId, o.Status });

        // Filtered index for common queries
        builder.HasIndex(o => o.CreatedAt)
            .HasFilter("[Status] = 'Pending'");
    }
}
```

## Unbounded Queries

### The Problem

Queries that can return unlimited results:

```csharp
// BAD: Could return millions of rows
var products = await db.Products.ToListAsync();

// BAD: User-controlled search without limits
var results = await db.Products
    .Where(p => p.Name.Contains(searchTerm))
    .ToListAsync();
```

### The Solution

Always apply limits and pagination:

```csharp
// GOOD: Bounded results
var products = await db.Products
    .Take(100)
    .ToListAsync();

// GOOD: Paginated
var results = await db.Products
    .Where(p => p.Name.Contains(searchTerm))
    .OrderBy(p => p.Name)
    .Skip(page * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

## Mixing Tracked and Untracked Entities

### The Problem

Attaching or mixing entities from different tracking contexts:

```csharp
// BAD: Entity from one context used in another
var product = await db1.Products.FindAsync(id);
db2.Products.Update(product);  // Confusion and potential errors
await db2.SaveChangesAsync();

// BAD: Mixing tracked and untracked
var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == id);
product.Price = newPrice;
await db.SaveChangesAsync();  // Nothing saved - not tracked!
```

### The Solution

Be consistent with tracking and context usage:

```csharp
// GOOD: Clear ownership
var product = await db.Products.FindAsync(id);
product.Price = newPrice;
await db.SaveChangesAsync();

// GOOD: Explicit attach for disconnected scenarios
var product = GetProductFromDto(dto);  // Untracked
db.Products.Update(product);  // Marks all as modified
await db.SaveChangesAsync();

// BETTER: Only update changed properties
var product = await db.Products.FindAsync(dto.Id);
product.Price = dto.Price;  // Only this is marked modified
await db.SaveChangesAsync();
```

## Not Using Transactions for Multi-Step Operations

### The Problem

Multiple SaveChanges without transaction can leave data inconsistent:

```csharp
// BAD: Partial failure possible
order.Status = OrderStatus.Completed;
await db.SaveChangesAsync();

inventory.Quantity -= order.Quantity;
await db.SaveChangesAsync();  // If this fails, order status is wrong

payment.Status = PaymentStatus.Captured;
await db.SaveChangesAsync();
```

### The Solution

Use explicit transactions:

```csharp
// GOOD: All-or-nothing
using var transaction = await db.Database.BeginTransactionAsync();
try
{
    order.Status = OrderStatus.Completed;
    await db.SaveChangesAsync();

    inventory.Quantity -= order.Quantity;
    await db.SaveChangesAsync();

    payment.Status = PaymentStatus.Captured;
    await db.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// BETTER: Single SaveChanges when possible
order.Status = OrderStatus.Completed;
inventory.Quantity -= order.Quantity;
payment.Status = PaymentStatus.Captured;
await db.SaveChangesAsync();  // All in one transaction
```

## String-Based Includes

### The Problem

Using string-based includes loses compile-time safety:

```csharp
// BAD: Typos not caught at compile time
var orders = await db.Orders
    .Include("Cusotmer")  // Typo - runtime error
    .Include("Items.Prodcut")  // Another typo
    .ToListAsync();
```

### The Solution

Use strongly-typed lambda expressions:

```csharp
// GOOD: Compile-time safety
var orders = await db.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
    .ToListAsync();
```
