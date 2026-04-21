# C# Version Migration Guide

This reference provides guidance for migrating codebases from older C# versions to modern C# (12, 13, 14). All examples demonstrate the modern approach using primary constructors and current patterns.

## Migration Strategy

### Assess Before Migrating

1. Identify your current `LangVersion` and `TargetFramework`
2. Review breaking changes for each version jump
3. Prioritize high-value modernizations over cosmetic changes
4. Use analyzers to identify migration opportunities

```bash
# Check current settings
rg -n "TargetFramework|LangVersion|TargetFrameworks" -g '*.csproj' -g 'Directory.Build.*' .
```

### Incremental Approach

Migrate in phases rather than all at once:

1. **Phase 1**: Non-breaking syntax updates (expression bodies, null operators)
2. **Phase 2**: Type modernization (records, primary constructors)
3. **Phase 3**: Collection and pattern updates
4. **Phase 4**: Advanced features (generic math, ref structs)

## Migrating to C# 12

### Constructor to Primary Constructor

**Before (Traditional Constructor)**:
```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository repository, ILogger<OrderService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order?> GetAsync(Guid id)
    {
        _logger.LogInformation("Fetching order {Id}", id);
        return await _repository.FindAsync(id);
    }
}
```

**After (Primary Constructor)**:
```csharp
public class OrderService(IOrderRepository repository, ILogger<OrderService> logger)
{
    public async Task<Order?> GetAsync(Guid id)
    {
        logger.LogInformation("Fetching order {Id}", id);
        return await repository.FindAsync(id);
    }
}
```

### When Primary Constructors Are Not Suitable

Keep traditional constructors when:
- You need multiple constructor overloads with complex logic
- Constructor parameters require significant validation
- You need explicit field visibility modifiers
- The class is a base class and derived classes need constructor chaining

```csharp
// Primary constructor not ideal here - complex validation needed
public class ValidatedEntity
{
    public Guid Id { get; }
    public string Name { get; }

    public ValidatedEntity(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100)
            throw new ArgumentException("Name too long", nameof(name));

        Id = id;
        Name = name.Trim();
    }
}
```

### Array Initialization to Collection Expressions

**Before**:
```csharp
private readonly string[] _allowedExtensions = new[] { ".jpg", ".png", ".gif" };
private readonly List<int> _numbers = new List<int> { 1, 2, 3 };
private readonly HashSet<string> _tags = new HashSet<string>();
```

**After**:
```csharp
private readonly string[] _allowedExtensions = [".jpg", ".png", ".gif"];
private readonly List<int> _numbers = [1, 2, 3];
private readonly HashSet<string> _tags = [];
```

### Spread Operator for Collection Concatenation

**Before**:
```csharp
public string[] GetAllArgs(string[] baseArgs, string outputPath)
{
    var result = new List<string>(baseArgs);
    result.Add("--output");
    result.Add(outputPath);
    return result.ToArray();
}
```

**After**:
```csharp
public string[] GetAllArgs(string[] baseArgs, string outputPath) =>
    [..baseArgs, "--output", outputPath];
```

## Migrating to C# 11

### String Concatenation to Raw String Literals

**Before**:
```csharp
public class QueryGenerator
{
    public string GenerateQuery(string table)
    {
        return "SELECT *\n" +
               "FROM " + table + "\n" +
               "WHERE active = true\n" +
               "ORDER BY created_at DESC";
    }
}
```

**After**:
```csharp
public class QueryGenerator(string defaultSchema)
{
    public string GenerateQuery(string table) =>
        $"""
        SELECT *
        FROM {defaultSchema}.{table}
        WHERE active = true
        ORDER BY created_at DESC
        """;
}
```

### Generic Constraints to Generic Math

**Before**:
```csharp
public static class MathUtils
{
    public static int Sum(IEnumerable<int> values)
    {
        int sum = 0;
        foreach (var v in values) sum += v;
        return sum;
    }

    public static double Sum(IEnumerable<double> values)
    {
        double sum = 0;
        foreach (var v in values) sum += v;
        return sum;
    }
}
```

**After**:
```csharp
public static class MathUtils
{
    public static T Sum<T>(IEnumerable<T> values) where T : INumber<T>
    {
        T sum = T.Zero;
        foreach (var v in values)
            sum += v;
        return sum;
    }
}
```

### Required Members

**Before**:
```csharp
public class UserRequest
{
    public string Email { get; set; } = null!; // Suppress warning, hope caller sets it
    public string Name { get; set; } = null!;
}
```

**After**:
```csharp
public class UserRequest
{
    public required string Email { get; init; }
    public required string Name { get; init; }
}
```

### List Patterns

**Before**:
```csharp
public string ParseCommand(string[] args)
{
    if (args.Length == 0)
        return "No command";
    if (args.Length == 1 && args[0] == "--help")
        return "Show help";
    if (args.Length >= 2 && args[0] == "--config")
        return $"Config: {args[1]}";
    return "Unknown";
}
```

**After**:
```csharp
public string ParseCommand(string[] args) => args switch
{
    [] => "No command",
    ["--help"] => "Show help",
    ["--config", var path, ..] => $"Config: {path}",
    _ => "Unknown"
};
```

## Migrating to C# 10

### Record Structs

**Before**:
```csharp
public struct Point : IEquatable<Point>
{
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y) { X = x; Y = y; }

    public bool Equals(Point other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}
```

**After**:
```csharp
public readonly record struct Point(double X, double Y);
```

### Global Usings

**Before (every file)**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
```

**After (GlobalUsings.cs or Directory.Build.props)**:
```csharp
// GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
```

### File-Scoped Namespaces

**Before**:
```csharp
namespace MyApp.Services
{
    public class OrderService
    {
        // Implementation
    }
}
```

**After**:
```csharp
namespace MyApp.Services;

public class OrderService(IOrderRepository repository)
{
    // Implementation
}
```

### Null Parameter Checks

**Before**:
```csharp
public void Process(string value)
{
    if (value == null)
        throw new ArgumentNullException(nameof(value));
    if (string.IsNullOrWhiteSpace(value))
        throw new ArgumentException("Value cannot be empty", nameof(value));
    // Process
}
```

**After**:
```csharp
public void Process(string value)
{
    ArgumentNullException.ThrowIfNull(value);
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    // Process
}
```

## Migrating to C# 9

### Classes to Records

**Before**:
```csharp
public class OrderDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is OrderDto dto &&
               Id == dto.Id &&
               CustomerName == dto.CustomerName &&
               Total == dto.Total;
    }

    public override int GetHashCode() => HashCode.Combine(Id, CustomerName, Total);
}
```

**After**:
```csharp
public record OrderDto(Guid Id, string CustomerName, decimal Total);
```

### Init-Only Properties

**Before**:
```csharp
public class Config
{
    public string ConnectionString { get; }

    public Config(string connectionString)
    {
        ConnectionString = connectionString;
    }
}
```

**After**:
```csharp
public class Config
{
    public required string ConnectionString { get; init; }
}
```

### Target-Typed New

**Before**:
```csharp
private readonly Dictionary<string, List<Order>> _ordersByCustomer =
    new Dictionary<string, List<Order>>();
```

**After**:
```csharp
private readonly Dictionary<string, List<Order>> _ordersByCustomer = [];
```

### Pattern Matching Improvements

**Before**:
```csharp
public decimal CalculateDiscount(object customer)
{
    if (customer is PremiumCustomer premium && premium.YearsActive > 5)
        return 0.20m;
    if (customer is PremiumCustomer)
        return 0.10m;
    if (customer is Customer c && c.OrderCount > 100)
        return 0.05m;
    return 0m;
}
```

**After**:
```csharp
public decimal CalculateDiscount(object customer) => customer switch
{
    PremiumCustomer { YearsActive: > 5 } => 0.20m,
    PremiumCustomer => 0.10m,
    Customer { OrderCount: > 100 } => 0.05m,
    _ => 0m
};
```

## Migrating to C# 8

### Nullable Reference Types

Enable in project file:
```xml
<PropertyGroup>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

**Before**:
```csharp
public class UserService
{
    public User GetUser(string id)
    {
        // May return null, caller doesn't know
        return _repository.Find(id);
    }
}
```

**After**:
```csharp
public class UserService(IUserRepository repository)
{
    public User? GetUser(string id) =>
        repository.Find(id);

    public User GetUserOrThrow(string id) =>
        repository.Find(id) ?? throw new UserNotFoundException(id);
}
```

### Switch Expressions

**Before**:
```csharp
public string GetStatusText(OrderStatus status)
{
    switch (status)
    {
        case OrderStatus.Pending:
            return "Waiting for processing";
        case OrderStatus.Processing:
            return "Being prepared";
        case OrderStatus.Shipped:
            return "On the way";
        case OrderStatus.Delivered:
            return "Arrived";
        default:
            throw new ArgumentOutOfRangeException(nameof(status));
    }
}
```

**After**:
```csharp
public string GetStatusText(OrderStatus status) => status switch
{
    OrderStatus.Pending => "Waiting for processing",
    OrderStatus.Processing => "Being prepared",
    OrderStatus.Shipped => "On the way",
    OrderStatus.Delivered => "Arrived",
    _ => throw new ArgumentOutOfRangeException(nameof(status))
};
```

### Using Declarations

**Before**:
```csharp
public async Task<string> ReadFileAsync(string path)
{
    using (var stream = File.OpenRead(path))
    using (var reader = new StreamReader(stream))
    {
        return await reader.ReadToEndAsync();
    }
}
```

**After**:
```csharp
public async Task<string> ReadFileAsync(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}
```

### Async Streams

**Before**:
```csharp
public async Task<List<Order>> GetAllOrdersAsync()
{
    var result = new List<Order>();
    // Load all into memory
    foreach (var batch in await _repository.GetBatchesAsync())
    {
        result.AddRange(batch);
    }
    return result;
}
```

**After**:
```csharp
public class OrderReader(IOrderRepository repository)
{
    public async IAsyncEnumerable<Order> GetAllOrdersAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var batch in repository.GetBatchesAsync(ct))
        {
            foreach (var order in batch)
            {
                yield return order;
            }
        }
    }
}
```

### Indices and Ranges

**Before**:
```csharp
var lastItem = array[array.Length - 1];
var lastThree = array.Skip(array.Length - 3).ToArray();
var middle = array.Skip(1).Take(array.Length - 2).ToArray();
```

**After**:
```csharp
var lastItem = array[^1];
var lastThree = array[^3..];
var middle = array[1..^1];
```

## C# 13 Migration

### params Collections

**Before**:
```csharp
public void Log(params string[] messages)
{
    foreach (var msg in messages)
        Console.WriteLine(msg);
}
```

**After (more flexible)**:
```csharp
public void Log(params IEnumerable<string> messages)
{
    foreach (var msg in messages)
        Console.WriteLine(msg);
}

// Or for zero-allocation
public int Sum(params ReadOnlySpan<int> values)
{
    int sum = 0;
    foreach (var v in values)
        sum += v;
    return sum;
}
```

### Lock Object

**Before**:
```csharp
public class Counter
{
    private readonly object _lock = new();
    private int _count;

    public int Increment()
    {
        lock (_lock)
        {
            return ++_count;
        }
    }
}
```

**After**:
```csharp
public class Counter
{
    private readonly Lock _lock = new();
    private int _count;

    public int Increment()
    {
        lock (_lock)
        {
            return ++_count;
        }
    }
}
```

## C# 14 Migration

### Field Keyword

**Before**:
```csharp
public class Person
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
    }
}
```

**After**:
```csharp
public class Person
{
    public string Name
    {
        get => field;
        set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
    }
}
```

## Common Migration Anti-Patterns

### Do Not Over-Modernize

Avoid changing working code just to use new syntax. Prioritize:
1. Bug fixes and correctness improvements
2. Readability improvements
3. Performance improvements
4. Cosmetic syntax updates (lowest priority)

### Do Not Break Compatibility

Before using new features, verify your target framework supports them:

| Feature | Minimum C# | Minimum .NET |
|---------|------------|--------------|
| Primary Constructors | C# 12 | .NET 8 |
| Collection Expressions | C# 12 | .NET 8 |
| Raw String Literals | C# 11 | .NET 7 |
| Required Members | C# 11 | .NET 7 |
| Records | C# 9 | .NET 5 |
| Nullable Reference Types | C# 8 | .NET Core 3.0 |

### Do Not Ignore Analyzers

Enable and fix analyzer warnings during migration:

```xml
<PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>
</PropertyGroup>
```

## Project File Updates

### Enable Modern Features

```xml
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

### For Libraries Supporting Multiple Frameworks

```xml
<PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

## Validation After Migration

After migrating, verify:

1. **Build succeeds**: `dotnet build`
2. **Tests pass**: `dotnet test`
3. **No new warnings**: Check build output
4. **Analyzer compliance**: Run `dotnet format --verify-no-changes`
5. **Runtime behavior**: Run integration/E2E tests
