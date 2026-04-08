# EF Core Query Patterns

## Query Tracking Strategies

### No-Tracking Queries

Use `AsNoTracking()` for read-only scenarios to reduce memory overhead and improve performance:

```csharp
// Single query
var products = await db.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToListAsync();

// Context-wide default (useful for read-heavy contexts)
db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
```

### No-Tracking with Identity Resolution

When you need consistent object references without change tracking:

```csharp
var orders = await db.Orders
    .AsNoTrackingWithIdentityResolution()
    .Include(o => o.Customer)
    .Include(o => o.Items)
    .ToListAsync();
// Same customer object returned for orders with same customer
```

### Tracking Queries

Use tracking only when you intend to modify entities:

```csharp
var product = await db.Products.FindAsync(id);
product.Price = newPrice;
await db.SaveChangesAsync();
```

## Loading Strategies

### Eager Loading

Load related data in a single query using `Include()`:

```csharp
// Single level
var orders = await db.Orders
    .Include(o => o.Customer)
    .ToListAsync();

// Nested includes
var orders = await db.Orders
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Category)
    .ToListAsync();

// Multiple includes
var orders = await db.Orders
    .Include(o => o.Customer)
    .Include(o => o.ShippingAddress)
    .Include(o => o.Items)
    .ToListAsync();
```

### Filtered Includes (EF Core 5+)

Include only specific related entities:

```csharp
var orders = await db.Orders
    .Include(o => o.Items.Where(i => i.Quantity > 0))
    .ToListAsync();

// With ordering and limiting
var customers = await db.Customers
    .Include(c => c.Orders
        .OrderByDescending(o => o.CreatedAt)
        .Take(5))
    .ToListAsync();
```

### Split Queries

Avoid cartesian explosion with large entity graphs:

```csharp
// Without split - one large query with cartesian product
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .ToListAsync();

// With split - multiple smaller queries
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSplitQuery()
    .ToListAsync();

// Configure as default
optionsBuilder.UseSqlServer(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
```

### Explicit Loading

Load related data on demand after the principal entity:

```csharp
var order = await db.Orders.FindAsync(orderId);

// Load collection
await db.Entry(order)
    .Collection(o => o.Items)
    .LoadAsync();

// Load reference
await db.Entry(order)
    .Reference(o => o.Customer)
    .LoadAsync();

// Query into loaded navigation
var highValueItems = await db.Entry(order)
    .Collection(o => o.Items)
    .Query()
    .Where(i => i.Price > 100)
    .ToListAsync();
```

### Lazy Loading

Requires proxy setup and virtual navigation properties:

```csharp
// Setup
services.AddDbContext<AppDbContext>(options =>
    options.UseLazyLoadingProxies()
           .UseSqlServer(connectionString));

// Entity
public class Order
{
    public int Id { get; set; }
    public virtual Customer Customer { get; set; }  // virtual required
    public virtual ICollection<OrderItem> Items { get; set; }
}
```

**Warning:** Lazy loading often leads to N+1 queries. Prefer explicit strategies.

## Projection Patterns

### Project to DTOs

Always project when you need a subset of data:

```csharp
var orderSummaries = await db.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .Select(o => new OrderSummaryDto
    {
        OrderId = o.Id,
        CustomerName = o.Customer.Name,
        ItemCount = o.Items.Count,
        Total = o.Items.Sum(i => i.Quantity * i.UnitPrice),
        CreatedAt = o.CreatedAt
    })
    .ToListAsync();
```

### Anonymous Projections

For internal use without creating DTOs:

```csharp
var stats = await db.Products
    .GroupBy(p => p.Category)
    .Select(g => new
    {
        Category = g.Key,
        Count = g.Count(),
        AvgPrice = g.Average(p => p.Price),
        MaxPrice = g.Max(p => p.Price)
    })
    .ToListAsync();
```

### Conditional Projection

```csharp
var products = await db.Products
    .Select(p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        DisplayPrice = p.IsOnSale ? p.SalePrice : p.RegularPrice,
        StockStatus = p.Stock > 10 ? "In Stock" : p.Stock > 0 ? "Low Stock" : "Out of Stock"
    })
    .ToListAsync();
```

## Compiled Queries

Pre-compile frequently used queries for better performance:

```csharp
public static class CompiledQueries
{
    public static readonly Func<AppDbContext, int, Task<Product?>> GetProductById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Products.FirstOrDefault(p => p.Id == id));

    public static readonly Func<AppDbContext, string, IAsyncEnumerable<Product>> GetProductsByCategory =
        EF.CompileAsyncQuery((AppDbContext db, string category) =>
            db.Products.Where(p => p.Category == category));

    public static readonly Func<AppDbContext, decimal, int, IAsyncEnumerable<Product>> GetExpensiveProducts =
        EF.CompileAsyncQuery((AppDbContext db, decimal minPrice, int take) =>
            db.Products
                .Where(p => p.Price >= minPrice)
                .OrderByDescending(p => p.Price)
                .Take(take));
}

// Usage
var product = await CompiledQueries.GetProductById(db, productId);

await foreach (var p in CompiledQueries.GetProductsByCategory(db, "Electronics"))
{
    // Process product
}
```

## Raw SQL Patterns

### FromSql for Entity Queries

```csharp
var products = await db.Products
    .FromSql($"SELECT * FROM Products WHERE Price > {minPrice}")
    .ToListAsync();

// Composable - can add LINQ operators
var products = await db.Products
    .FromSql($"SELECT * FROM Products WHERE Category = {category}")
    .Where(p => p.IsActive)
    .OrderBy(p => p.Name)
    .ToListAsync();
```

### SqlQuery for Arbitrary Results (EF Core 8+)

```csharp
var totals = await db.Database
    .SqlQuery<decimal>($"SELECT SUM(Price) FROM Products WHERE Category = {category}")
    .ToListAsync();

var stats = await db.Database
    .SqlQuery<CategoryStats>($@"
        SELECT Category, COUNT(*) as ProductCount, AVG(Price) as AvgPrice
        FROM Products
        GROUP BY Category")
    .ToListAsync();
```

### ExecuteSql for Non-Query Operations

```csharp
var affected = await db.Database
    .ExecuteSqlAsync($"UPDATE Products SET Price = Price * {multiplier} WHERE Category = {category}");
```

## Pagination Patterns

### Offset-Based Pagination

```csharp
public async Task<PagedResult<T>> GetPageAsync<T>(
    IQueryable<T> query,
    int pageNumber,
    int pageSize)
{
    var totalCount = await query.CountAsync();
    var items = await query
        .Skip(pageNumber * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<T>
    {
        Items = items,
        TotalCount = totalCount,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    };
}
```

### Keyset Pagination (Better for Large Datasets)

```csharp
// More efficient for deep pages - uses index instead of offset
public async Task<List<Product>> GetNextPageAsync(int lastId, int pageSize)
{
    return await db.Products
        .Where(p => p.Id > lastId)
        .OrderBy(p => p.Id)
        .Take(pageSize)
        .ToListAsync();
}

// Bidirectional keyset pagination
public async Task<List<Product>> GetPreviousPageAsync(int firstId, int pageSize)
{
    return await db.Products
        .Where(p => p.Id < firstId)
        .OrderByDescending(p => p.Id)
        .Take(pageSize)
        .OrderBy(p => p.Id)  // Restore ascending order
        .ToListAsync();
}
```

## Global Query Filters

Apply filters automatically to all queries:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Soft delete filter
    modelBuilder.Entity<Product>()
        .HasQueryFilter(p => !p.IsDeleted);

    // Multi-tenant filter
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => o.TenantId == _tenantId);
}

// Bypass when needed
var allProducts = await db.Products
    .IgnoreQueryFilters()
    .ToListAsync();
```

## Temporal Tables (EF Core 6+)

Query historical data:

```csharp
// Configure temporal table
modelBuilder.Entity<Product>()
    .ToTable("Products", b => b.IsTemporal());

// Query as of specific time
var historicalProducts = await db.Products
    .TemporalAsOf(specificDateTime)
    .ToListAsync();

// Query between time range
var productHistory = await db.Products
    .TemporalBetween(startDate, endDate)
    .Where(p => p.Id == productId)
    .ToListAsync();

// Get all changes
var allChanges = await db.Products
    .TemporalAll()
    .Where(p => p.Id == productId)
    .OrderBy(p => EF.Property<DateTime>(p, "PeriodStart"))
    .ToListAsync();
```
