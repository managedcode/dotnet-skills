# xUnit Anti-Patterns

Avoid these common testing mistakes.

## Test Interdependence

Tests must not depend on execution order or shared mutable state.

Bad:

```csharp
public class UserServiceTests
{
    private static int _testUserId;

    [Fact]
    public void CreateUser_ValidData_ReturnsId()
    {
        var service = new UserService();
        _testUserId = service.CreateUser("test@example.com"); // sets static state
        Assert.True(_testUserId > 0);
    }

    [Fact]
    public void GetUser_ExistingId_ReturnsUser()
    {
        var service = new UserService();
        var user = service.GetUser(_testUserId); // depends on other test running first
        Assert.NotNull(user);
    }
}
```

Good:

```csharp
public class UserServiceTests
{
    [Fact]
    public void CreateUser_ValidData_ReturnsId()
    {
        // Arrange
        var service = new UserService();

        // Act
        var userId = service.CreateUser("test@example.com");

        // Assert
        Assert.True(userId > 0);
    }

    [Fact]
    public void GetUser_ExistingId_ReturnsUser()
    {
        // Arrange
        var service = new UserService();
        var userId = service.CreateUser("test@example.com"); // each test creates its own data

        // Act
        var user = service.GetUser(userId);

        // Assert
        Assert.NotNull(user);
    }
}
```

## Excessive Mocking

Over-mocking creates brittle tests that verify implementation rather than behavior.

Bad:

```csharp
public class OrderServiceTests
{
    [Fact]
    public void PlaceOrder_Valid_CallsAllMethods()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<OrderService>>();
        var repoMock = new Mock<IOrderRepository>();
        var validatorMock = new Mock<IOrderValidator>();
        var pricingMock = new Mock<IPricingService>();
        var inventoryMock = new Mock<IInventoryService>();
        var notificationMock = new Mock<INotificationService>();

        validatorMock.Setup(v => v.Validate(It.IsAny<Order>())).Returns(true);
        pricingMock.Setup(p => p.CalculateTotal(It.IsAny<Order>())).Returns(100m);
        inventoryMock.Setup(i => i.Reserve(It.IsAny<Order>())).Returns(true);
        repoMock.Setup(r => r.Save(It.IsAny<Order>())).Returns(1);

        var service = new OrderService(loggerMock.Object, repoMock.Object,
            validatorMock.Object, pricingMock.Object, inventoryMock.Object, notificationMock.Object);

        // Act
        service.PlaceOrder(new Order());

        // Assert - verifying every internal call creates coupling to implementation
        validatorMock.Verify(v => v.Validate(It.IsAny<Order>()), Times.Once);
        pricingMock.Verify(p => p.CalculateTotal(It.IsAny<Order>()), Times.Once);
        inventoryMock.Verify(i => i.Reserve(It.IsAny<Order>()), Times.Once);
        repoMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Once);
        notificationMock.Verify(n => n.Send(It.IsAny<string>()), Times.Once);
    }
}
```

Good:

```csharp
public class OrderServiceTests
{
    [Fact]
    public void PlaceOrder_ValidOrder_ReturnsOrderId()
    {
        // Arrange - use real implementations where cheap, mock only external boundaries
        var repository = new InMemoryOrderRepository();
        var notificationService = Substitute.For<INotificationService>();
        var service = new OrderService(repository, notificationService);
        var order = new Order { CustomerId = 1, Items = [new OrderItem { ProductId = 1, Quantity = 1 }] };

        // Act
        var orderId = service.PlaceOrder(order);

        // Assert - verify observable outcomes
        Assert.True(orderId > 0);
        var savedOrder = repository.GetById(orderId);
        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.Placed, savedOrder.Status);
    }
}
```

## Testing Implementation Instead of Behavior

Tests should verify what the code does, not how it does it.

Bad:

```csharp
public class CacheTests
{
    [Fact]
    public void Get_CacheMiss_CallsUnderlyingStore()
    {
        // Arrange
        var storeMock = new Mock<IDataStore>();
        storeMock.Setup(s => s.Get("key")).Returns("value");
        var cache = new Cache(storeMock.Object);

        // Act
        cache.Get("key");
        cache.Get("key");

        // Assert - tests internal caching logic, not behavior
        storeMock.Verify(s => s.Get("key"), Times.Once);
    }
}
```

Good:

```csharp
public class CacheTests
{
    [Fact]
    public void Get_SameKey_ReturnsSameValue()
    {
        // Arrange
        var store = new InMemoryDataStore();
        store.Set("key", "value");
        var cache = new Cache(store);

        // Act
        var result1 = cache.Get("key");
        var result2 = cache.Get("key");

        // Assert - verifies behavior: caching returns consistent results
        Assert.Equal("value", result1);
        Assert.Equal("value", result2);
        Assert.Same(result1, result2); // if reference equality matters
    }

    [Fact]
    public void Get_AfterExpiry_ReturnsUpdatedValue()
    {
        // Arrange
        var store = new InMemoryDataStore();
        store.Set("key", "original");
        var timeProvider = new FakeTimeProvider();
        var cache = new Cache(store, timeProvider, ttl: TimeSpan.FromMinutes(5));

        // Act
        var result1 = cache.Get("key");
        store.Set("key", "updated");
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        var result2 = cache.Get("key");

        // Assert
        Assert.Equal("original", result1);
        Assert.Equal("updated", result2);
    }
}
```

## Ignoring Test Isolation

Each test must run in isolation without affecting others.

Bad:

```csharp
public class ConfigurationTests
{
    [Fact]
    public void SetGlobalConfig_UpdatesEnvironment()
    {
        Environment.SetEnvironmentVariable("APP_MODE", "test"); // pollutes other tests
        var config = new AppConfiguration();
        Assert.Equal("test", config.Mode);
    }
}
```

Good:

```csharp
public class ConfigurationTests : IDisposable
{
    private readonly string? _originalValue;

    public ConfigurationTests()
    {
        _originalValue = Environment.GetEnvironmentVariable("APP_MODE");
    }

    [Fact]
    public void SetGlobalConfig_UpdatesEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("APP_MODE", "test");
        var config = new AppConfiguration();

        // Assert
        Assert.Equal("test", config.Mode);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("APP_MODE", _originalValue);
    }
}
```

Even better - inject configuration:

```csharp
public class ConfigurationTests
{
    [Fact]
    public void AppConfiguration_TestMode_ReturnsTestBehavior()
    {
        // Arrange
        var settings = new Dictionary<string, string?> { ["APP_MODE"] = "test" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var config = new AppConfiguration(configuration);

        // Assert
        Assert.Equal("test", config.Mode);
    }
}
```

## Non-Deterministic Tests

Tests must produce the same result every time.

Bad:

```csharp
public class TimestampTests
{
    [Fact]
    public void CreateRecord_SetsTimestamp()
    {
        // Arrange
        var service = new RecordService();

        // Act
        var record = service.CreateRecord("data");

        // Assert - fails intermittently when clock rolls over
        Assert.Equal(DateTime.Now.Date, record.CreatedAt.Date);
    }
}

public class RandomTests
{
    [Fact]
    public void GenerateId_ReturnsUniqueId()
    {
        var service = new IdGenerator();
        var id1 = service.Generate();
        var id2 = service.Generate();
        Assert.NotEqual(id1, id2); // usually passes, sometimes fails
    }
}
```

Good:

```csharp
public class TimestampTests
{
    [Fact]
    public void CreateRecord_SetsTimestampFromProvider()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var service = new RecordService(timeProvider);

        // Act
        var record = service.CreateRecord("data");

        // Assert
        Assert.Equal(fixedTime, record.CreatedAt);
    }
}

public class RandomTests
{
    [Fact]
    public void GenerateId_WithSeed_ReturnsExpectedId()
    {
        // Arrange
        var randomProvider = new SeededRandomProvider(42);
        var service = new IdGenerator(randomProvider);

        // Act
        var id = service.Generate();

        // Assert
        Assert.Equal("expected-seeded-value", id);
    }

    [Fact]
    public void GenerateId_ReturnsValidFormat()
    {
        // Arrange
        var service = new IdGenerator();

        // Act
        var id = service.Generate();

        // Assert - verify format rather than specific value
        Assert.Matches(@"^[a-f0-9]{32}$", id);
    }
}
```

## Swallowing Exceptions

Tests must not catch exceptions without re-throwing or asserting.

Bad:

```csharp
public class FileServiceTests
{
    [Fact]
    public void ReadFile_MissingFile_HandlesGracefully()
    {
        try
        {
            var service = new FileService();
            var content = service.ReadFile("/nonexistent/path");
            Assert.NotNull(content);
        }
        catch
        {
            // test passes either way - hides real failures
        }
    }
}
```

Good:

```csharp
public class FileServiceTests
{
    [Fact]
    public void ReadFile_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var service = new FileService();

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(
            () => service.ReadFile("/nonexistent/path"));
        Assert.Contains("nonexistent", exception.FileName);
    }

    [Fact]
    public void TryReadFile_MissingFile_ReturnsFalse()
    {
        // Arrange
        var service = new FileService();

        // Act
        var success = service.TryReadFile("/nonexistent/path", out var content);

        // Assert
        Assert.False(success);
        Assert.Null(content);
    }
}
```

## Magic Numbers and Strings

Unexplained literals reduce test clarity.

Bad:

```csharp
public class DiscountTests
{
    [Fact]
    public void CalculateDiscount_ReturnsExpected()
    {
        var service = new DiscountService();
        var discount = service.Calculate(150, 3, true);
        Assert.Equal(22.5m, discount);
    }
}
```

Good:

```csharp
public class DiscountTests
{
    [Fact]
    public void CalculateDiscount_GoldMemberWithBulkOrder_AppliesCombinedDiscount()
    {
        // Arrange
        const decimal orderTotal = 150m;
        const int itemCount = 3;
        const bool isGoldMember = true;
        const decimal expectedBulkDiscount = 15m;      // 10% for 3+ items
        const decimal expectedMemberDiscount = 7.5m;   // 5% gold member bonus
        const decimal expectedTotalDiscount = 22.5m;

        var service = new DiscountService();

        // Act
        var discount = service.Calculate(orderTotal, itemCount, isGoldMember);

        // Assert
        Assert.Equal(expectedTotalDiscount, discount);
    }
}
```

## Asserting Too Much or Too Little

Each test should verify one logical concept.

Bad - too many assertions:

```csharp
public class UserRegistrationTests
{
    [Fact]
    public void RegisterUser_ValidData_EverythingWorks()
    {
        var service = new UserService();
        var result = service.Register("test@example.com", "password123");

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("test@example.com", result.User.Email);
        Assert.True(result.User.IsActive);
        Assert.NotNull(result.User.CreatedAt);
        Assert.True(result.EmailSent);
        Assert.Equal(1, service.GetUserCount());
        Assert.NotNull(service.GetUserByEmail("test@example.com"));
        // continues for 20 more assertions
    }
}
```

Bad - too few assertions:

```csharp
public class UserRegistrationTests
{
    [Fact]
    public void RegisterUser_ValidData_Succeeds()
    {
        var service = new UserService();
        var result = service.Register("test@example.com", "password123");
        Assert.True(result.Success); // what about the user? was it actually created?
    }
}
```

Good:

```csharp
public class UserRegistrationTests
{
    [Fact]
    public void RegisterUser_ValidData_CreatesActiveUser()
    {
        // Arrange
        var service = new UserService();
        const string email = "test@example.com";

        // Act
        var result = service.Register(email, "password123");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User.Email);
        Assert.True(result.User.IsActive);
    }

    [Fact]
    public void RegisterUser_ValidData_SendsWelcomeEmail()
    {
        // Arrange
        var emailService = Substitute.For<IEmailService>();
        var service = new UserService(emailService);

        // Act
        service.Register("test@example.com", "password123");

        // Assert
        emailService.Received(1).SendWelcomeEmail("test@example.com");
    }

    [Fact]
    public void RegisterUser_ValidData_PersistsUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository();
        var service = new UserService(repository);

        // Act
        var result = service.Register("test@example.com", "password123");

        // Assert
        var persistedUser = repository.GetByEmail("test@example.com");
        Assert.NotNull(persistedUser);
        Assert.Equal(result.User.Id, persistedUser.Id);
    }
}
```

## Async Void Tests

xUnit does not properly await `async void` test methods.

Bad:

```csharp
public class AsyncTests
{
    [Fact]
    public async void FetchData_ReturnsData() // async void - xUnit won't wait
    {
        var service = new DataService();
        var data = await service.FetchAsync();
        Assert.NotEmpty(data); // may not execute before test completes
    }
}
```

Good:

```csharp
public class AsyncTests
{
    [Fact]
    public async Task FetchData_ReturnsData() // async Task - xUnit awaits properly
    {
        // Arrange
        var service = new DataService();

        // Act
        var data = await service.FetchAsync();

        // Assert
        Assert.NotEmpty(data);
    }
}
```

## Constructor Abuse

Test constructors are for shared setup, not test-specific logic.

Bad:

```csharp
public class PaymentTests
{
    private readonly PaymentResult _result;

    public PaymentTests()
    {
        var service = new PaymentService();
        _result = service.ProcessPayment(new Payment { Amount = 100 }); // runs for every test
    }

    [Fact]
    public void ProcessPayment_ValidAmount_Succeeds() => Assert.True(_result.Success);

    [Fact]
    public void ProcessPayment_ValidAmount_ReturnsTransactionId() => Assert.NotNull(_result.TransactionId);
}
```

Good:

```csharp
public class PaymentTests(PaymentFixture fixture) : IClassFixture<PaymentFixture>
{
    [Fact]
    public void ProcessPayment_ValidAmount_Succeeds()
    {
        // Arrange
        var service = new PaymentService(fixture.Gateway);
        var payment = new Payment { Amount = 100 };

        // Act
        var result = service.ProcessPayment(payment);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void ProcessPayment_ValidAmount_ReturnsTransactionId()
    {
        // Arrange
        var service = new PaymentService(fixture.Gateway);
        var payment = new Payment { Amount = 100 };

        // Act
        var result = service.ProcessPayment(payment);

        // Assert
        Assert.NotNull(result.TransactionId);
    }
}

public class PaymentFixture
{
    public IPaymentGateway Gateway { get; } = new TestPaymentGateway();
}
```

## Sources

- [xUnit.net Patterns](https://xunit.net/docs/comparisons)
- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
