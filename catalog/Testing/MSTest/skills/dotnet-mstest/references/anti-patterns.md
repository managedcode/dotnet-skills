# MSTest Anti-Patterns Reference

## 1. Duplicated Test Methods Instead of DataRow

### Bad

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        var calc = new Calculator();
        Assert.AreEqual(5, calc.Add(2, 3));
    }

    [TestMethod]
    public void Add_TwoNegativeNumbers_ReturnsSum()
    {
        var calc = new Calculator();
        Assert.AreEqual(-5, calc.Add(-2, -3));
    }

    [TestMethod]
    public void Add_PositiveAndNegative_ReturnsSum()
    {
        var calc = new Calculator();
        Assert.AreEqual(1, calc.Add(3, -2));
    }

    [TestMethod]
    public void Add_Zeros_ReturnsZero()
    {
        var calc = new Calculator();
        Assert.AreEqual(0, calc.Add(0, 0));
    }
}
```

### Good

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    [DataRow(2, 3, 5, DisplayName = "Two positive numbers")]
    [DataRow(-2, -3, -5, DisplayName = "Two negative numbers")]
    [DataRow(3, -2, 1, DisplayName = "Positive and negative")]
    [DataRow(0, 0, 0, DisplayName = "Zeros")]
    public void Add_WithVariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var calc = new Calculator();
        Assert.AreEqual(expected, calc.Add(a, b));
    }
}
```

## 2. Heavy Work in Lifecycle Hooks

### Bad

```csharp
[TestClass]
public class IntegrationTests
{
    private static SqlConnection _connection = null!;
    private static List<TestUser> _testUsers = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _connection = new SqlConnection("Server=...");
        _connection.Open();

        // Heavy seeding operation
        _testUsers = [];
        for (var i = 0; i < 10000; i++)
        {
            var user = new TestUser($"user{i}@test.com");
            InsertUser(_connection, user);
            _testUsers.Add(user);
        }
    }

    [TestMethod]
    public void GetUser_ReturnsUser()
    {
        // Simple test that doesn't need 10000 users
    }
}
```

### Good

```csharp
[TestClass]
public class IntegrationTests
{
    private SqlConnection _connection = null!;

    [TestInitialize]
    public void Setup()
    {
        _connection = new SqlConnection("Server=...");
        _connection.Open();
    }

    [TestCleanup]
    public void Teardown()
    {
        _connection.Dispose();
    }

    [TestMethod]
    public void GetUser_ReturnsUser()
    {
        // Create only what this test needs
        var user = new TestUser("test@example.com");
        InsertUser(_connection, user);

        var result = GetUserById(_connection, user.Id);
        Assert.IsNotNull(result);
    }
}
```

## 3. Test Order Dependencies

### Bad

```csharp
[TestClass]
public class OrderDependentTests
{
    private static Order? _createdOrder;

    [TestMethod]
    [Priority(1)]
    public void Test1_CreateOrder()
    {
        _createdOrder = new Order("ORD-001");
        // Assumes this runs first
    }

    [TestMethod]
    [Priority(2)]
    public void Test2_UpdateOrder()
    {
        // Breaks if Test1 didn't run first
        _createdOrder!.Status = OrderStatus.Processing;
    }

    [TestMethod]
    [Priority(3)]
    public void Test3_DeleteOrder()
    {
        // Breaks if Test1 and Test2 didn't run
        Assert.IsNotNull(_createdOrder);
    }
}
```

### Good

```csharp
[TestClass]
public class IndependentOrderTests
{
    [TestMethod]
    public void CreateOrder_WithValidData_ReturnsOrder()
    {
        var order = new Order("ORD-001");
        Assert.AreEqual("ORD-001", order.Id);
    }

    [TestMethod]
    public void UpdateOrder_WithExistingOrder_UpdatesStatus()
    {
        var order = new Order("ORD-002"); // Each test creates its own
        order.Status = OrderStatus.Processing;
        Assert.AreEqual(OrderStatus.Processing, order.Status);
    }

    [TestMethod]
    public void DeleteOrder_WithExistingOrder_RemovesOrder()
    {
        var order = new Order("ORD-003");
        var repository = new InMemoryOrderRepository();
        repository.Add(order);

        repository.Delete(order.Id);

        Assert.IsNull(repository.GetById(order.Id));
    }
}
```

## 4. Missing Assert Statements

### Bad

```csharp
[TestClass]
public class ServiceTests
{
    [TestMethod]
    public void ProcessData_DoesNotThrow()
    {
        var service = new DataService();
        service.ProcessData("test"); // No assertion
    }

    [TestMethod]
    public async Task LoadAsync_CompletesSuccessfully()
    {
        var loader = new DataLoader();
        await loader.LoadAsync(); // No assertion
    }
}
```

### Good

```csharp
[TestClass]
public class ServiceTests
{
    [TestMethod]
    public void ProcessData_WithValidInput_ReturnsProcessedResult()
    {
        var service = new DataService();
        var result = service.ProcessData("test");

        Assert.IsNotNull(result);
        Assert.AreEqual("PROCESSED: test", result);
    }

    [TestMethod]
    public async Task LoadAsync_WithValidSource_ReturnsData()
    {
        var loader = new DataLoader();
        var data = await loader.LoadAsync();

        Assert.IsNotNull(data);
        Assert.IsTrue(data.Count > 0);
    }
}
```

## 5. Catching Exceptions Incorrectly

### Bad

```csharp
[TestClass]
public class ExceptionTests
{
    [TestMethod]
    public void Validate_WithNull_ThrowsException()
    {
        try
        {
            var validator = new Validator();
            validator.Validate(null!);
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception ex)
        {
            // Too broad, catches any exception
            Assert.IsNotNull(ex);
        }
    }
}
```

### Good

```csharp
[TestClass]
public class ExceptionTests
{
    [TestMethod]
    public void Validate_WithNull_ThrowsArgumentNullException()
    {
        var validator = new Validator();

        var exception = Assert.ThrowsException<ArgumentNullException>(
            () => validator.Validate(null!));

        Assert.AreEqual("input", exception.ParamName);
    }

    [TestMethod]
    public async Task ValidateAsync_WithNull_ThrowsArgumentNullException()
    {
        var validator = new AsyncValidator();

        var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await validator.ValidateAsync(null!));

        Assert.AreEqual("input", exception.ParamName);
    }
}
```

## 6. Shared Mutable State Between Tests

### Bad

```csharp
[TestClass]
public class SharedStateTests
{
    private static List<string> _items = []; // Static mutable state

    [TestMethod]
    public void AddItem_IncreasesCount()
    {
        _items.Add("item1");
        Assert.AreEqual(1, _items.Count); // May fail if other tests ran
    }

    [TestMethod]
    public void AddTwoItems_CountIsTwo()
    {
        _items.Add("item2");
        _items.Add("item3");
        Assert.AreEqual(2, _items.Count); // Fails because state leaked
    }
}
```

### Good

```csharp
[TestClass]
public class IsolatedStateTests
{
    private List<string> _items = null!;

    [TestInitialize]
    public void Setup()
    {
        _items = []; // Fresh instance per test
    }

    [TestMethod]
    public void AddItem_IncreasesCount()
    {
        _items.Add("item1");
        Assert.AreEqual(1, _items.Count);
    }

    [TestMethod]
    public void AddTwoItems_CountIsTwo()
    {
        _items.Add("item1");
        _items.Add("item2");
        Assert.AreEqual(2, _items.Count);
    }
}
```

## 7. Ignoring Async/Await in Tests

### Bad

```csharp
[TestClass]
public class AsyncTests
{
    [TestMethod]
    public void LoadData_Completes() // Not async
    {
        var service = new DataService();
        var task = service.LoadAsync(); // Fire and forget
        // Test completes before async work finishes
    }

    [TestMethod]
    public void ProcessAsync_WithResult()
    {
        var service = new DataService();
        var result = service.ProcessAsync().Result; // Blocking call, can deadlock
        Assert.IsNotNull(result);
    }
}
```

### Good

```csharp
[TestClass]
public class AsyncTests
{
    [TestMethod]
    public async Task LoadData_CompletesSuccessfully()
    {
        var service = new DataService();
        var data = await service.LoadAsync();
        Assert.IsNotNull(data);
    }

    [TestMethod]
    public async Task ProcessAsync_WithValidInput_ReturnsResult()
    {
        var service = new DataService();
        var result = await service.ProcessAsync();
        Assert.IsNotNull(result);
    }
}
```

## 8. Hardcoded Test Data Paths

### Bad

```csharp
[TestClass]
public class FileTests
{
    [TestMethod]
    public void ReadConfig_ParsesCorrectly()
    {
        var config = ConfigReader.Read(@"C:\Users\john\project\test-data\config.json");
        Assert.IsNotNull(config);
    }
}
```

### Good

```csharp
[TestClass]
public class FileTests
{
    [TestMethod]
    public void ReadConfig_ParsesCorrectly()
    {
        var testDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "config.json");

        var config = ConfigReader.Read(testDataPath);
        Assert.IsNotNull(config);
    }

    [TestMethod]
    public void ReadConfig_WithEmbeddedResource_ParsesCorrectly()
    {
        using var stream = GetType().Assembly
            .GetManifestResourceStream("MyTests.TestData.config.json");
        var config = ConfigReader.Read(stream!);
        Assert.IsNotNull(config);
    }
}
```

## 9. Testing Implementation Details

### Bad

```csharp
[TestClass]
public class ImplementationTests
{
    [TestMethod]
    public void Cache_UsesCorrectInternalStructure()
    {
        var cache = new Cache<string>();
        cache.Add("key", "value");

        // Accessing private field through reflection
        var dictionary = typeof(Cache<string>)
            .GetField("_dictionary", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cache) as Dictionary<string, string>;

        Assert.AreEqual(1, dictionary!.Count);
    }
}
```

### Good

```csharp
[TestClass]
public class BehaviorTests
{
    [TestMethod]
    public void Cache_AfterAdd_ReturnsStoredValue()
    {
        var cache = new Cache<string>();
        cache.Add("key", "value");

        var result = cache.Get("key");

        Assert.AreEqual("value", result);
    }

    [TestMethod]
    public void Cache_AfterAdd_ContainsKey()
    {
        var cache = new Cache<string>();
        cache.Add("key", "value");

        Assert.IsTrue(cache.ContainsKey("key"));
    }
}
```

## 10. Overly Strict DateTime Assertions

### Bad

```csharp
[TestClass]
public class TimestampTests
{
    [TestMethod]
    public void CreateOrder_SetsCreatedAt()
    {
        var order = new Order();
        Assert.AreEqual(DateTime.Now, order.CreatedAt); // Flaky, timing issue
    }
}
```

### Good

```csharp
[TestClass]
public class TimestampTests
{
    [TestMethod]
    public void CreateOrder_SetsCreatedAtToApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var order = new Order();
        var after = DateTime.UtcNow;

        Assert.IsTrue(order.CreatedAt >= before && order.CreatedAt <= after);
    }

    [TestMethod]
    public void CreateOrder_WithTimeProvider_SetsExpectedTime()
    {
        var fixedTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var order = new Order(timeProvider);

        Assert.AreEqual(fixedTime, order.CreatedAt);
    }
}
```

## 11. Missing Cleanup for External Resources

### Bad

```csharp
[TestClass]
public class ResourceLeakTests
{
    [TestMethod]
    public void WriteFile_CreatesFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "test content");
        Assert.IsTrue(File.Exists(path));
        // File never deleted, accumulates over test runs
    }

    [TestMethod]
    public void OpenConnection_Succeeds()
    {
        var connection = new SqlConnection("...");
        connection.Open();
        Assert.AreEqual(ConnectionState.Open, connection.State);
        // Connection never disposed
    }
}
```

### Good

```csharp
[TestClass]
public class ProperResourceTests
{
    private readonly List<string> _tempFiles = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [TestMethod]
    public void WriteFile_CreatesFile()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);

        File.WriteAllText(path, "test content");

        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void OpenConnection_Succeeds()
    {
        using var connection = new SqlConnection("...");
        connection.Open();
        Assert.AreEqual(ConnectionState.Open, connection.State);
    }
}
```

## 12. Mixing VSTest and Microsoft.Testing.Platform Patterns

### Bad

```csharp
// Project uses MSTest.Sdk (Microsoft.Testing.Platform by default)
// but test code uses VSTest-specific patterns

[TestClass]
public class MixedRunnerTests
{
    [TestMethod]
    [DeploymentItem("TestData/file.json")] // VSTest-specific, may not work
    public void Test_WithDeploymentItem()
    {
        // ...
    }
}
```

### Good

```csharp
// For MSTest.Sdk projects, use runner-agnostic patterns
[TestClass]
public class RunnerAgnosticTests
{
    [TestMethod]
    public void Test_WithEmbeddedResource()
    {
        using var stream = GetType().Assembly
            .GetManifestResourceStream("MyTests.TestData.file.json");
        // ...
    }

    [TestMethod]
    public void Test_WithTestDataFolder()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "file.json");
        // ...
    }
}
```

## Sources

- [MSTest best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Async testing patterns](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests#test-async-methods)
- [Test isolation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#test-isolation)
