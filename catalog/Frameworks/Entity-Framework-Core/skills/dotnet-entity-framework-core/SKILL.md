---
name: dotnet-entity-framework-core
version: "1.0.0"
category: "Data"
description: "Design, tune, or review EF Core data access with proper modeling, migrations, query translation, performance, and lifetime management for modern .NET applications."
compatibility: "Requires EF Core 7+ (preferably 8/9 for latest features)."
---

# Entity Framework Core

## Trigger On

- working on `DbContext`, migrations, model configuration, or EF queries
- reviewing tracking, loading, performance, or transaction behavior
- porting data access from EF6 or custom repositories to EF Core
- optimizing slow database queries

## Documentation

- [EF Core Overview](https://learn.microsoft.com/en-us/ef/core/)
- [Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
- [Efficient Querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [What's New in EF Core 9](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/whatsnew)

### References

- [patterns.md](references/patterns.md) - Query patterns, tracking strategies, loading strategies, projections, compiled queries, pagination, and temporal tables
- [anti-patterns.md](references/anti-patterns.md) - Common EF Core mistakes including N+1 queries, large contexts, generic repositories, and missing indexes

## Workflow

1. **Prefer EF Core for new development** unless a documented gap requires Dapper or raw SQL
2. **Keep `DbContext` lifetime scoped** — align with unit of work
3. **Review query translation** — check generated SQL, avoid N+1
4. **Treat migrations as first-class** — reviewable, not throwaway
5. **Be deliberate about provider behavior** — cross-provider but not identical
6. **Validate with query inspection** — not just in-memory mental model

## DbContext Patterns

### Basic Configuration
```csharp
public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

// Entity Configuration (Fluent API)
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasMany(p => p.OrderItems).WithOne(oi => oi.Product);
    }
}
```

### Registration with DI
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .EnableSensitiveDataLogging()  // Dev only
           .EnableDetailedErrors());      // Dev only

// Or with pooling (better performance)
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

## Query Patterns

### Use AsNoTracking for Read-Only
```csharp
// Bad - tracks entities unnecessarily
var products = await db.Products.ToListAsync();

// Good - no tracking overhead
var products = await db.Products
    .AsNoTracking()
    .ToListAsync();
```

### Project to DTOs
```csharp
// Bad - loads entire entity graph
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Customer)
    .ToListAsync();

// Good - loads only needed data
var orders = await db.Orders
    .Select(o => new OrderDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name,
        ItemCount = o.Items.Count,
        Total = o.Items.Sum(i => i.Price)
    })
    .ToListAsync();
```

### Avoid N+1 Queries
```csharp
// Bad - N+1 problem
foreach (var order in orders)
{
    var items = await db.OrderItems
        .Where(i => i.OrderId == order.Id)
        .ToListAsync();
}

// Good - eager loading
var orders = await db.Orders
    .Include(o => o.Items)
    .ToListAsync();

// Good - split query for large graphs
var orders = await db.Orders
    .Include(o => o.Items)
    .AsSplitQuery()
    .ToListAsync();
```

### Compiled Queries (EF Core 9)
```csharp
// Pre-compiled for frequently used queries
private static readonly Func<AppDbContext, int, Task<Product?>> GetProductById =
    EF.CompileAsyncQuery((AppDbContext db, int id) =>
        db.Products.FirstOrDefault(p => p.Id == id));

// Usage
var product = await GetProductById(db, productId);
```

## Migration Patterns

### Creating Migrations
```bash
# Add migration
dotnet ef migrations add AddProductIndex

# Apply to database
dotnet ef database update

# Generate SQL script
dotnet ef migrations script --idempotent -o migrate.sql
```

### Data Migrations
```csharp
public partial class AddProductIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Products_Sku",
            table: "Products",
            column: "Sku",
            unique: true);

        // Data migration (if needed)
        migrationBuilder.Sql(@"
            UPDATE Products
            SET NormalizedName = UPPER(Name)
            WHERE NormalizedName IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_Sku",
            table: "Products");
    }
}
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| `ToList()` then filter | Loads all data to memory | Filter in query |
| Multiple DbContext per request | Transaction issues | Scoped lifetime |
| Lazy loading everywhere | N+1 queries | Explicit Include |
| Generic repository wrapper | Removes query power | Use DbContext directly |
| Ignoring generated SQL | Hidden performance issues | Log and review |
| `SaveChanges()` in loops | Many roundtrips | Batch then save |

## Performance Best Practices

1. **Index frequently queried columns:**
   ```csharp
   builder.HasIndex(p => p.CreatedAt);
   builder.HasIndex(p => new { p.Category, p.Status });
   ```

2. **Use pagination:**
   ```csharp
   var page = await db.Products
       .OrderBy(p => p.Id)
       .Skip(pageSize * pageNumber)
       .Take(pageSize)
       .ToListAsync();
   ```

3. **Batch updates (EF Core 7+):**
   ```csharp
   await db.Products
       .Where(p => p.Category == "Obsolete")
       .ExecuteDeleteAsync();

   await db.Products
       .Where(p => p.Category == "Sale")
       .ExecuteUpdateAsync(p => p.SetProperty(x => x.Price, x => x.Price * 0.9m));
   ```

4. **Minimize network roundtrips:**
   ```csharp
   // Bad - 3 roundtrips
   var product = await db.Products.FindAsync(id);
   var reviews = await db.Reviews.Where(r => r.ProductId == id).ToListAsync();
   var related = await db.Products.Where(p => p.Category == product.Category).ToListAsync();

   // Good - 1 roundtrip
   var data = await db.Products
       .Where(p => p.Id == id)
       .Select(p => new
       {
           Product = p,
           Reviews = p.Reviews,
           Related = db.Products.Where(r => r.Category == p.Category).Take(5)
       })
       .FirstOrDefaultAsync();
   ```

## Concurrency Patterns

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; }

    // Or use RowVersion
    [Timestamp]
    public byte[] RowVersion { get; set; }
}

// Handle concurrency conflicts
try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var databaseValues = await entry.GetDatabaseValuesAsync();
    // Resolve conflict...
}
```

## Deliver

- EF Core models and queries that match the domain
- safer migrations and lifetime management
- performance-aware data access decisions
- proper indexing and query optimization

## Validate

- query behavior is intentional (check SQL logs)
- migrations are reviewable and correct
- no N+1 queries in common paths
- indexes exist for filtered/sorted columns
- DbContext lifetime is scoped properly
- concurrency is handled for critical entities
