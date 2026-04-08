# EF6 Maintenance Patterns

## Repository and Unit of Work

### Basic Repository Pattern

```csharp
public interface IRepository<T> where T : class
{
    T GetById(int id);
    IQueryable<T> Query();
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}

public class EF6Repository<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;

    public EF6Repository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public T GetById(int id) => _dbSet.Find(id);
    public IQueryable<T> Query() => _dbSet;
    public void Add(T entity) => _dbSet.Add(entity);
    public void Update(T entity) => _context.Entry(entity).State = EntityState.Modified;
    public void Delete(T entity) => _dbSet.Remove(entity);
}
```

### Unit of Work Pattern

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    int SaveChanges();
    Task<int> SaveChangesAsync();
}

public class EF6UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public EF6UnitOfWork(DbContext context)
    {
        _context = context;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!_repositories.ContainsKey(typeof(TEntity)))
        {
            _repositories[typeof(TEntity)] = new EF6Repository<TEntity>(_context);
        }
        return (IRepository<TEntity>)_repositories[typeof(TEntity)];
    }

    public int SaveChanges() => _context.SaveChanges();
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
```

## Connection and Context Management

### Scoped Context Lifetime

Always scope DbContext to a logical unit of work:

```csharp
// ASP.NET MVC: use per-request lifetime
public class OrderController : Controller
{
    private readonly MyDbContext _context;

    public OrderController(MyDbContext context)
    {
        _context = context;
    }
}

// Non-web: use explicit using blocks
using (var context = new MyDbContext())
{
    var orders = context.Orders.Where(o => o.Status == "Pending").ToList();
    // process orders
    context.SaveChanges();
}
```

### Avoiding Long-Lived Contexts

Do not:
- Keep a DbContext alive across multiple HTTP requests
- Share a DbContext across threads
- Cache a DbContext in a static field

### Connection Resiliency

Configure retry logic for transient failures:

```csharp
public class MyDbConfiguration : DbConfiguration
{
    public MyDbConfiguration()
    {
        SetExecutionStrategy("System.Data.SqlClient",
            () => new SqlAzureExecutionStrategy(5, TimeSpan.FromSeconds(10)));
    }
}
```

## Query Optimization

### Eager Loading

Use `Include` to avoid N+1 queries:

```csharp
var orders = context.Orders
    .Include(o => o.Customer)
    .Include(o => o.OrderItems.Select(oi => oi.Product))
    .Where(o => o.OrderDate >= startDate)
    .ToList();
```

### Projection

Select only needed columns:

```csharp
var orderSummaries = context.Orders
    .Where(o => o.Status == "Shipped")
    .Select(o => new OrderSummaryDto
    {
        OrderId = o.Id,
        CustomerName = o.Customer.Name,
        TotalAmount = o.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice)
    })
    .ToList();
```

### No-Tracking Queries

Use `AsNoTracking()` for read-only scenarios:

```csharp
var products = context.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToList();
```

### Compiled Queries (LINQ to Entities)

For frequently executed queries:

```csharp
private static readonly Func<MyDbContext, int, Customer> GetCustomerById =
    CompiledQuery.Compile<MyDbContext, int, Customer>(
        (ctx, id) => ctx.Customers.FirstOrDefault(c => c.Id == id));

// Usage
var customer = GetCustomerById(_context, customerId);
```

## Handling Concurrency

### Optimistic Concurrency with RowVersion

```csharp
public class Order
{
    public int Id { get; set; }
    public string Status { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }
}

// Handle concurrency conflict
try
{
    context.SaveChanges();
}
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var databaseValues = entry.GetDatabaseValues();
    if (databaseValues == null)
    {
        // Entity was deleted
    }
    else
    {
        // Resolve conflict: client wins, database wins, or merge
        entry.OriginalValues.SetValues(databaseValues);
    }
}
```

## Auditing and Interception

### SaveChanges Override for Auditing

```csharp
public class AuditableDbContext : DbContext
{
    public override int SaveChanges()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var auditable = (IAuditable)entry.Entity;
            auditable.ModifiedDate = DateTime.UtcNow;
            auditable.ModifiedBy = GetCurrentUser();

            if (entry.State == EntityState.Added)
            {
                auditable.CreatedDate = DateTime.UtcNow;
                auditable.CreatedBy = GetCurrentUser();
            }
        }

        return base.SaveChanges();
    }
}
```

### Command Interception for Logging

```csharp
public class LoggingInterceptor : IDbCommandInterceptor
{
    public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> context)
    {
        LogCommand(command);
    }

    public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> context)
    {
        LogCommand(command);
    }

    public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> context)
    {
        LogCommand(command);
    }

    private void LogCommand(DbCommand command)
    {
        Debug.WriteLine($"SQL: {command.CommandText}");
    }

    // Implement other interface members...
}

// Registration
DbInterception.Add(new LoggingInterceptor());
```

## Stored Procedure Integration

### Mapping CUD Operations

```csharp
modelBuilder.Entity<Order>()
    .MapToStoredProcedures(s =>
        s.Insert(i => i.HasName("usp_InsertOrder"))
         .Update(u => u.HasName("usp_UpdateOrder"))
         .Delete(d => d.HasName("usp_DeleteOrder")));
```

### Calling Stored Procedures Directly

```csharp
// Return entities
var orders = context.Database.SqlQuery<Order>(
    "EXEC GetOrdersByCustomer @customerId",
    new SqlParameter("@customerId", customerId)).ToList();

// Non-query
context.Database.ExecuteSqlCommand(
    "EXEC ArchiveOldOrders @cutoffDate",
    new SqlParameter("@cutoffDate", cutoffDate));
```

## Testing Strategies

### Integration Testing with LocalDB

```csharp
[TestClass]
public class OrderRepositoryTests
{
    private MyDbContext _context;

    [TestInitialize]
    public void Setup()
    {
        var connectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=TestDb;Integrated Security=True";
        _context = new MyDbContext(connectionString);
        _context.Database.CreateIfNotExists();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.Delete();
        _context.Dispose();
    }

    [TestMethod]
    public void CanAddOrder()
    {
        var order = new Order { Status = "New" };
        _context.Orders.Add(order);
        _context.SaveChanges();

        Assert.IsTrue(order.Id > 0);
    }
}
```

### Mocking with Interfaces

Wrap DbContext behind an interface for unit testing:

```csharp
public interface IMyDbContext
{
    IDbSet<Order> Orders { get; }
    int SaveChanges();
}

// In tests, mock IMyDbContext
var mockContext = new Mock<IMyDbContext>();
var mockOrders = new Mock<IDbSet<Order>>();
mockContext.Setup(c => c.Orders).Returns(mockOrders.Object);
```

## Performance Monitoring

### Database Logging

```csharp
context.Database.Log = sql => Debug.WriteLine(sql);
```

### Identifying Slow Queries

Use SQL Server Profiler, Extended Events, or Query Store alongside EF6 logging to correlate slow queries with application code paths.

## Common Anti-Patterns to Avoid

1. **Lazy loading in loops**: Causes N+1 queries; use eager loading or projection
2. **Tracking entities unnecessarily**: Use `AsNoTracking()` for read-only queries
3. **Returning IQueryable from repositories**: Leaks query composition outside the data layer
4. **Ignoring connection management**: Always dispose DbContext properly
5. **Mixing ObjectContext and DbContext**: Pick one API and stick with it
6. **Skipping concurrency handling**: Add RowVersion for entities with concurrent updates
