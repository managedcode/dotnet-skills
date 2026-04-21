# Modern C# Patterns

This reference covers idiomatic patterns for C# 12, C# 13, and C# 14. All examples use primary constructors and modern syntax.

## Primary Constructors (C# 12+)

Primary constructors reduce boilerplate by declaring constructor parameters directly on the type declaration.

### Classes with Primary Constructors

```csharp
// Modern: Primary constructor with dependency injection
public class OrderService(IOrderRepository repository, ILogger<OrderService> logger)
{
    public async Task<Order?> GetOrderAsync(Guid id)
    {
        logger.LogInformation("Fetching order {OrderId}", id);
        return await repository.FindAsync(id);
    }
}

// Primary constructor with field initialization
public class CacheService(IMemoryCache cache, TimeSpan defaultExpiration)
{
    private readonly TimeSpan _expiration = defaultExpiration;

    public void Set<T>(string key, T value) =>
        cache.Set(key, value, _expiration);
}
```

### When to Capture vs. Assign

```csharp
// Capture directly when parameter is readonly and used as-is
public class UserValidator(IUserRepository repository)
{
    public async Task<bool> ExistsAsync(string email) =>
        await repository.ExistsAsync(email);
}

// Assign to field when you need mutability or different visibility
public class Counter(int initialValue)
{
    private int _count = initialValue;

    public int Increment() => ++_count;
}
```

## Records (C# 9+)

Records provide value semantics with minimal ceremony.

### Record Patterns

```csharp
// Immutable data transfer
public record OrderDto(Guid Id, string CustomerName, decimal Total);

// Record with computed property
public record Rectangle(double Width, double Height)
{
    public double Area => Width * Height;
}

// Record struct for high-performance scenarios (C# 10+)
public readonly record struct Point(double X, double Y);

// Record with validation in primary constructor body
public record Email
{
    public string Value { get; }

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format", nameof(value));
        Value = value;
    }
}
```

### With-Expressions

```csharp
var order = new OrderDto(Guid.NewGuid(), "Alice", 99.99m);
var updated = order with { Total = 149.99m };
```

## Pattern Matching (C# 8-14)

### Type Patterns

```csharp
public class PaymentProcessor(ILogger<PaymentProcessor> logger)
{
    public decimal ProcessPayment(Payment payment) => payment switch
    {
        CreditCardPayment { Amount: > 1000 } cc => ProcessLargeCredit(cc),
        CreditCardPayment cc => ProcessCredit(cc),
        BankTransfer bt => ProcessTransfer(bt),
        CryptoPayment { Currency: "BTC" } crypto => ProcessBitcoin(crypto),
        CryptoPayment crypto => ProcessCrypto(crypto),
        null => throw new ArgumentNullException(nameof(payment)),
        _ => throw new NotSupportedException($"Unknown payment type: {payment.GetType()}")
    };
}
```

### Property Patterns

```csharp
public static string DescribeOrder(Order order) => order switch
{
    { Status: OrderStatus.Pending, Total: > 500 } => "Large pending order",
    { Status: OrderStatus.Pending } => "Pending order",
    { Status: OrderStatus.Shipped, TrackingNumber: not null } => "In transit",
    { Status: OrderStatus.Delivered } => "Delivered",
    _ => "Unknown status"
};
```

### List Patterns (C# 11+)

```csharp
public static string AnalyzeArgs(string[] args) => args switch
{
    [] => "No arguments",
    [var single] => $"Single argument: {single}",
    ["--help" or "-h", ..] => "Help requested",
    ["--config", var path, ..] => $"Config file: {path}",
    [var first, .., var last] => $"First: {first}, Last: {last}",
    _ => $"Multiple arguments: {args.Length}"
};
```

### Relational and Logical Patterns

```csharp
public static string GetDiscount(int quantity) => quantity switch
{
    <= 0 => throw new ArgumentOutOfRangeException(nameof(quantity)),
    < 10 => "No discount",
    >= 10 and < 50 => "10% discount",
    >= 50 and < 100 => "20% discount",
    >= 100 => "30% discount"
};
```

## Collection Expressions (C# 12+)

Collection expressions provide a unified syntax for creating collections.

### Basic Collection Expressions

```csharp
// Arrays
int[] numbers = [1, 2, 3, 4, 5];

// Lists
List<string> names = ["Alice", "Bob", "Charlie"];

// Immutable collections
ImmutableArray<int> immutable = [1, 2, 3];

// Empty collections
List<Order> orders = [];

// HashSets
HashSet<string> tags = ["dotnet", "csharp", "modern"];
```

### Spread Operator

```csharp
int[] first = [1, 2, 3];
int[] second = [4, 5, 6];
int[] combined = [..first, ..second]; // [1, 2, 3, 4, 5, 6]

// Useful for building commands or arguments
string[] baseArgs = ["--verbose", "--format=json"];
string[] allArgs = [..baseArgs, "--output", outputPath];
```

### Collection Expressions in Methods

```csharp
public class ConfigurationBuilder(string environment)
{
    public IReadOnlyList<string> GetSearchPaths() =>
    [
        $"/etc/app/{environment}",
        $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.app",
        "./config"
    ];
}
```

## Required Members (C# 11+)

```csharp
public class CreateUserRequest
{
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? PhoneNumber { get; init; }
}

// Usage
var request = new CreateUserRequest
{
    Email = "user@example.com",
    Name = "John Doe"
};
```

## File-Scoped Types (C# 11+)

```csharp
// Internal helper visible only within this file
file class TemporaryBuffer(int capacity)
{
    private readonly byte[] _buffer = new byte[capacity];

    public Span<byte> GetSpan() => _buffer;
}
```

## Raw String Literals (C# 11+)

```csharp
public class QueryBuilder(string tableName)
{
    public string BuildSelectQuery(IEnumerable<string> columns) =>
        $"""
        SELECT {string.Join(", ", columns)}
        FROM {tableName}
        WHERE deleted_at IS NULL
        ORDER BY created_at DESC
        """;

    public string BuildJsonTemplate() =>
        """
        {
            "type": "object",
            "properties": {
                "id": { "type": "string" },
                "name": { "type": "string" }
            }
        }
        """;
}
```

## Generic Math (C# 11+)

```csharp
public static class MathExtensions
{
    public static T Sum<T>(this IEnumerable<T> source) where T : INumber<T>
    {
        T result = T.Zero;
        foreach (var item in source)
            result += item;
        return result;
    }

    public static T Average<T>(this IEnumerable<T> source)
        where T : INumber<T>, IDivisionOperators<T, int, T>
    {
        T sum = T.Zero;
        int count = 0;
        foreach (var item in source)
        {
            sum += item;
            count++;
        }
        return count == 0 ? T.Zero : sum / count;
    }
}
```

## ref Fields and scoped (C# 11+)

```csharp
public ref struct SpanReader(ReadOnlySpan<byte> buffer)
{
    private ReadOnlySpan<byte> _buffer = buffer;
    private int _position = 0;

    public bool TryReadByte(out byte value)
    {
        if (_position >= _buffer.Length)
        {
            value = 0;
            return false;
        }
        value = _buffer[_position++];
        return true;
    }
}
```

## Inline Arrays (C# 12+)

```csharp
[InlineArray(16)]
public struct Buffer16
{
    private byte _element0;
}

// Usage in performance-critical code
public ref struct SmallBuffer(Buffer16 storage)
{
    private Buffer16 _storage = storage;

    public Span<byte> AsSpan() => _storage;
}
```

## Interceptors (C# 12+ Preview)

Interceptors are a preview feature for compile-time method interception, primarily used by source generators.

```csharp
// Note: Preview feature, requires <InterceptorsPreviewNamespaces>
[InterceptsLocation("Program.cs", line: 10, column: 5)]
public static void InterceptedMethod() { }
```

## C# 13 Features

### params Collections

```csharp
// params now works with any collection type
public static void LogMessages(params IEnumerable<string> messages)
{
    foreach (var message in messages)
        Console.WriteLine(message);
}

// Can pass spans for zero-allocation scenarios
public static int Sum(params ReadOnlySpan<int> values)
{
    int sum = 0;
    foreach (var value in values)
        sum += value;
    return sum;
}
```

### Lock Object

```csharp
public class ThreadSafeCounter
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

### Escape Sequence \e

```csharp
// ANSI escape for terminal colors
public static string Red(string text) => $"\e[31m{text}\e[0m";
public static string Green(string text) => $"\e[32m{text}\e[0m";
```

### Implicit Index Access in Initializers

```csharp
public class Buffer
{
    public byte[] Data { get; } = new byte[10];
}

var buffer = new Buffer
{
    Data =
    {
        [^1] = 255, // Last element
        [^2] = 128  // Second to last
    }
};
```

## C# 14 Features

### Field Keyword

```csharp
// Direct access to auto-property backing field
public class Person
{
    public string Name
    {
        get => field;
        set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
    }

    public int Age
    {
        get => field;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
```

### Extension Types (Preview)

```csharp
// Extension everything - methods, properties, operators
extension StringExtensions for string
{
    public bool IsValidEmail => this.Contains('@') && this.Contains('.');

    public string Reverse()
    {
        var chars = this.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
}
```

## Async Patterns

### Modern Async with Primary Constructors

```csharp
public class DataSyncService(
    ISourceRepository source,
    IDestinationRepository destination,
    ILogger<DataSyncService> logger)
{
    public async IAsyncEnumerable<SyncResult> SyncAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.GetAllAsync(ct))
        {
            var result = await SyncItemAsync(item, ct);
            yield return result;
        }
    }

    private async Task<SyncResult> SyncItemAsync(DataItem item, CancellationToken ct)
    {
        try
        {
            await destination.UpsertAsync(item, ct);
            return new SyncResult(item.Id, Success: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync item {Id}", item.Id);
            return new SyncResult(item.Id, Success: false, Error: ex.Message);
        }
    }
}

public record SyncResult(string Id, bool Success, string? Error = null);
```

## Null Handling Patterns

### Modern Null Checks

```csharp
public class UserService(IUserRepository repository)
{
    // ArgumentNullException helpers (C# 10+)
    public async Task<User> GetUserAsync(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await repository.FindAsync(id)
            ?? throw new UserNotFoundException(id);
    }

    // Null-coalescing assignment
    public User GetOrCreateDefault(string id)
    {
        _cache[id] ??= new User(id, "Default");
        return _cache[id];
    }

    private readonly Dictionary<string, User> _cache = [];
}
```

### Pattern-Based Null Checks

```csharp
public static string FormatUser(User? user) => user switch
{
    null => "No user",
    { Name: null or "" } => $"User {user.Id} (no name)",
    { Name: var name, Email: var email } => $"{name} <{email}>"
};
```

## LINQ Modernization

### Index and Range in LINQ

```csharp
public class PagedQuery(IQueryable<Order> source)
{
    public IQueryable<Order> GetPage(int pageNumber, int pageSize) =>
        source
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

    // Using ranges with materialized collections
    public Order[] GetLastN(Order[] orders, int n) =>
        orders[^n..];
}
```

### Collection Builders

```csharp
public static class QueryBuilder
{
    public static IEnumerable<T> BuildQuery<T>(
        IEnumerable<T> source,
        Func<T, bool>? filter = null,
        Func<T, object>? orderBy = null)
    {
        var query = source;

        if (filter is not null)
            query = query.Where(filter);

        if (orderBy is not null)
            query = query.OrderBy(orderBy);

        return query;
    }
}
```
