# MSTest Patterns Reference

## Data-Driven Testing Patterns

### DataRow for Inline Test Data

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    [DataRow(2, 3, 5)]
    [DataRow(0, 0, 0)]
    [DataRow(-1, 1, 0)]
    [DataRow(int.MaxValue, 0, int.MaxValue)]
    public void Add_WithVariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var calculator = new Calculator();
        var result = calculator.Add(a, b);
        Assert.AreEqual(expected, result);
    }
}
```

### DataRow with Display Names

```csharp
[TestClass]
public class ValidationTests
{
    [TestMethod]
    [DataRow("", false, DisplayName = "Empty string is invalid")]
    [DataRow("  ", false, DisplayName = "Whitespace is invalid")]
    [DataRow("valid@email.com", true, DisplayName = "Valid email passes")]
    [DataRow("invalid", false, DisplayName = "Missing @ is invalid")]
    public void IsValidEmail_WithVariousInputs_ReturnsExpectedResult(string email, bool expected)
    {
        var validator = new EmailValidator();
        Assert.AreEqual(expected, validator.IsValid(email));
    }
}
```

### DynamicData for Complex Test Data

```csharp
[TestClass]
public class OrderProcessorTests
{
    public static IEnumerable<object[]> OrderTestData =>
    [
        [new Order { Items = [], Total = 0 }, OrderStatus.Empty],
        [new Order { Items = [new("Item1", 10)], Total = 10 }, OrderStatus.Valid],
        [new Order { Items = [new("Item1", -5)], Total = -5 }, OrderStatus.Invalid]
    ];

    [TestMethod]
    [DynamicData(nameof(OrderTestData))]
    public void ProcessOrder_WithVariousOrders_ReturnsCorrectStatus(Order order, OrderStatus expected)
    {
        var processor = new OrderProcessor();
        var result = processor.Process(order);
        Assert.AreEqual(expected, result);
    }
}
```

### DynamicData from Method

```csharp
[TestClass]
public class ParserTests
{
    public static IEnumerable<object[]> GetParseTestCases()
    {
        yield return ["123", 123];
        yield return ["456", 456];
        yield return ["-789", -789];
    }

    [TestMethod]
    [DynamicData(nameof(GetParseTestCases), DynamicDataSourceType.Method)]
    public void Parse_WithValidInput_ReturnsExpectedValue(string input, int expected)
    {
        var result = int.Parse(input);
        Assert.AreEqual(expected, result);
    }
}
```

## Lifecycle Hook Patterns

### TestInitialize and TestCleanup

```csharp
[TestClass]
public class DatabaseTests
{
    private TestDatabaseContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        _context = new TestDatabaseContext();
        _context.Database.EnsureCreated();
    }

    [TestCleanup]
    public void Teardown()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task CreateUser_WithValidData_PersistsToDatabase()
    {
        var user = new User("test@example.com", "TestUser");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        Assert.AreEqual(1, await _context.Users.CountAsync());
    }
}
```

### ClassInitialize and ClassCleanup

```csharp
[TestClass]
public class IntegrationTests
{
    private static HttpClient _client = null!;
    private static WebApplicationFactory<Program> _factory = null!;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [ClassCleanup]
    public static void ClassTeardown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestMethod]
    public async Task GetEndpoint_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### AssemblyInitialize and AssemblyCleanup

```csharp
[TestClass]
public class GlobalTestSetup
{
    [AssemblyInitialize]
    public static void AssemblySetup(TestContext context)
    {
        // One-time setup for entire test assembly
        Environment.SetEnvironmentVariable("TEST_MODE", "true");
    }

    [AssemblyCleanup]
    public static void AssemblyTeardown()
    {
        Environment.SetEnvironmentVariable("TEST_MODE", null);
    }
}
```

## Parallel Testing Patterns

### Parallel Execution at Class Level

```csharp
// Enable parallel execution in .runsettings or project file
// <Parallelize Workers="4" Scope="ClassLevel" />

[TestClass]
public class IndependentTestsA
{
    [TestMethod]
    public void TestA1() { /* ... */ }

    [TestMethod]
    public void TestA2() { /* ... */ }
}

[TestClass]
public class IndependentTestsB
{
    [TestMethod]
    public void TestB1() { /* ... */ }
}
```

### Disabling Parallelism for Specific Tests

```csharp
[TestClass]
[DoNotParallelize]
public class SequentialDatabaseTests
{
    // Tests in this class run sequentially
    [TestMethod]
    public void Test1_CreateRecord() { /* ... */ }

    [TestMethod]
    public void Test2_UpdateRecord() { /* ... */ }
}
```

### Thread-Safe Test Fixtures

```csharp
[TestClass]
public class ThreadSafeTests
{
    private static readonly Lock _lock = new();
    private static int _sharedCounter;

    [TestMethod]
    public void IncrementCounter_ThreadSafe()
    {
        lock (_lock)
        {
            _sharedCounter++;
            Assert.IsTrue(_sharedCounter > 0);
        }
    }
}
```

## Async Testing Patterns

### Basic Async Test

```csharp
[TestClass]
public class AsyncServiceTests
{
    [TestMethod]
    public async Task GetDataAsync_WhenCalled_ReturnsData()
    {
        var service = new DataService();
        var result = await service.GetDataAsync();
        Assert.IsNotNull(result);
    }
}
```

### Testing Async Exceptions

```csharp
[TestClass]
public class AsyncExceptionTests
{
    [TestMethod]
    public async Task ProcessAsync_WithInvalidInput_ThrowsArgumentException()
    {
        var service = new ValidationService();

        await Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await service.ProcessAsync(null!));
    }
}
```

### Testing Cancellation

```csharp
[TestClass]
public class CancellationTests
{
    [TestMethod]
    public async Task LongRunningOperation_WhenCancelled_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var service = new LongRunningService();

        var task = service.ProcessAsync(cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => task);
    }
}
```

## Dependency Injection Patterns

### Test Class with Primary Constructor

```csharp
[TestClass]
public class ServiceTests
{
    private readonly Mock<IRepository> _mockRepository = new();
    private readonly Mock<ILogger<OrderService>> _mockLogger = new();

    private OrderService CreateService() =>
        new(_mockRepository.Object, _mockLogger.Object);

    [TestMethod]
    public async Task CreateOrder_WithValidOrder_SavesAndReturnsOrder()
    {
        var order = new Order("ORD-001", 100m);
        _mockRepository.Setup(r => r.SaveAsync(order))
            .ReturnsAsync(order);

        var service = CreateService();
        var result = await service.CreateOrderAsync(order);

        Assert.AreEqual("ORD-001", result.Id);
        _mockRepository.Verify(r => r.SaveAsync(order), Times.Once);
    }
}
```

### Test Base Class with Common Setup

```csharp
public abstract class ServiceTestBase<TService> where TService : class
{
    protected Mock<ILogger<TService>> MockLogger { get; } = new();
    protected Mock<IConfiguration> MockConfiguration { get; } = new();

    [TestInitialize]
    public virtual void BaseSetup()
    {
        MockConfiguration.Setup(c => c["Environment"]).Returns("Test");
    }
}

[TestClass]
public class UserServiceTests : ServiceTestBase<UserService>
{
    private readonly Mock<IUserRepository> _mockRepo = new();

    [TestMethod]
    public async Task GetUser_ReturnsUser()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new User(1, "Test"));

        var service = new UserService(_mockRepo.Object, MockLogger.Object);
        var user = await service.GetUserAsync(1);

        Assert.AreEqual("Test", user.Name);
    }
}
```

## Assertion Patterns

### Collection Assertions

```csharp
[TestClass]
public class CollectionAssertionTests
{
    [TestMethod]
    public void GetUsers_ReturnsExpectedCollection()
    {
        var service = new UserService();
        var users = service.GetActiveUsers();

        CollectionAssert.IsNotNull(users);
        CollectionAssert.AllItemsAreNotNull(users);
        CollectionAssert.AllItemsAreInstancesOfType(users, typeof(User));
        CollectionAssert.AreEqual(new[] { "Alice", "Bob" }, users.Select(u => u.Name).ToList());
    }
}
```

### String Assertions

```csharp
[TestClass]
public class StringAssertionTests
{
    [TestMethod]
    public void FormatMessage_ContainsExpectedParts()
    {
        var formatter = new MessageFormatter();
        var result = formatter.Format("Hello", "World");

        StringAssert.Contains(result, "Hello");
        StringAssert.StartsWith(result, "Message:");
        StringAssert.EndsWith(result, ".");
        StringAssert.Matches(result, new Regex(@"Message: .+ - .+\."));
    }
}
```

### Custom Assert Extensions

```csharp
public static class CustomAssert
{
    public static void IsWithinRange<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new AssertFailedException(
                $"Expected {value} to be between {min} and {max}");
        }
    }
}

[TestClass]
public class RangeTests
{
    [TestMethod]
    public void Calculate_ReturnsValueInExpectedRange()
    {
        var calculator = new Calculator();
        var result = calculator.RandomInRange(1, 100);
        CustomAssert.IsWithinRange(result, 1, 100);
    }
}
```

## Test Organization Patterns

### Arrange-Act-Assert Structure

```csharp
[TestClass]
public class ShoppingCartTests
{
    [TestMethod]
    public void AddItem_WhenCartEmpty_IncreasesItemCount()
    {
        // Arrange
        var cart = new ShoppingCart();
        var item = new CartItem("SKU-001", "Widget", 9.99m);

        // Act
        cart.AddItem(item);

        // Assert
        Assert.AreEqual(1, cart.ItemCount);
        Assert.AreEqual(9.99m, cart.Total);
    }
}
```

### Test Categories

```csharp
[TestClass]
public class MixedTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void UnitTest_FastAndIsolated()
    {
        // Fast, no external dependencies
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task IntegrationTest_UsesDatabase()
    {
        // Requires database
        await Task.CompletedTask;
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public void SmokeTest_BasicFunctionality()
    {
        // Quick sanity check
    }
}
```

### Test Priority

```csharp
[TestClass]
public class PrioritizedTests
{
    [TestMethod]
    [Priority(1)]
    public void CriticalPath_Test()
    {
        // Most important, run first
    }

    [TestMethod]
    [Priority(2)]
    public void Secondary_Test()
    {
        // Run after priority 1
    }
}
```

## Sources

- [DataRow attribute](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests#data-driven-tests)
- [Test lifecycle attributes](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests#test-lifecycle)
- [Parallel test execution](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-configure-execution#parallelize-tests)
- [Assert classes](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests#assertions)
