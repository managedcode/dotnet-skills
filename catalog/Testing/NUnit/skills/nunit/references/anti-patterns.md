# NUnit Anti-Patterns Reference

## Test Structure Anti-Patterns

### Multiple Unrelated Assertions

```csharp
// WRONG - Tests multiple unrelated things
[Test]
public void UserService_Works()
{
    var user = _service.CreateUser("test@example.com");
    Assert.That(user, Is.Not.Null);

    var updated = _service.UpdateUser(user.Id, "new@example.com");
    Assert.That(updated.Email, Is.EqualTo("new@example.com"));

    _service.DeleteUser(user.Id);
    var deleted = _service.GetUser(user.Id);
    Assert.That(deleted, Is.Null);
}

// CORRECT - One test per behavior
[Test]
public void CreateUser_ValidEmail_ReturnsUser()
{
    var user = _service.CreateUser("test@example.com");
    Assert.That(user, Is.Not.Null);
}

[Test]
public void UpdateUser_NewEmail_UpdatesEmail()
{
    var user = _service.CreateUser("test@example.com");
    var updated = _service.UpdateUser(user.Id, "new@example.com");
    Assert.That(updated.Email, Is.EqualTo("new@example.com"));
}

[Test]
public void DeleteUser_ExistingUser_RemovesUser()
{
    var user = _service.CreateUser("test@example.com");
    _service.DeleteUser(user.Id);
    Assert.That(_service.GetUser(user.Id), Is.Null);
}
```

### Test Interdependence

```csharp
// WRONG - Tests depend on execution order
[TestFixture]
public class OrderDependentTests
{
    private static int _sharedCounter = 0;

    [Test, Order(1)]
    public void First_IncrementsCounter()
    {
        _sharedCounter++;
        Assert.That(_sharedCounter, Is.EqualTo(1));
    }

    [Test, Order(2)]
    public void Second_DependsOnFirst()
    {
        // Fails if First doesn't run first
        Assert.That(_sharedCounter, Is.EqualTo(1));
    }
}

// CORRECT - Independent tests
[TestFixture]
public class IndependentTests
{
    private int _counter;

    [SetUp]
    public void SetUp()
    {
        _counter = 0; // Fresh state for each test
    }

    [Test]
    public void Increment_FromZero_ReturnsOne()
    {
        _counter++;
        Assert.That(_counter, Is.EqualTo(1));
    }

    [Test]
    public void Increment_Twice_ReturnsTwo()
    {
        _counter++;
        _counter++;
        Assert.That(_counter, Is.EqualTo(2));
    }
}
```

### Classic Assert Instead of Constraint Model

```csharp
// WRONG - Classic assert syntax (less readable, deprecated in NUnit 4)
[Test]
public void ClassicAsserts()
{
    Assert.AreEqual(expected, actual);
    Assert.IsTrue(condition);
    Assert.IsNotNull(obj);
    Assert.IsInstanceOf<MyClass>(obj);
    Assert.Throws<Exception>(() => DoSomething());
}

// CORRECT - Constraint model
[Test]
public void ConstraintAsserts()
{
    Assert.That(actual, Is.EqualTo(expected));
    Assert.That(condition, Is.True);
    Assert.That(obj, Is.Not.Null);
    Assert.That(obj, Is.InstanceOf<MyClass>());
    Assert.That(() => DoSomething(), Throws.TypeOf<Exception>());
}
```

## Async Anti-Patterns

### Async Void Tests

```csharp
// WRONG - async void cannot be awaited
[Test]
public async void GetDataAsync_BadPattern()
{
    var result = await _service.GetDataAsync();
    Assert.That(result, Is.Not.Null);
    // Test might complete before assertion runs!
}

// CORRECT - async Task
[Test]
public async Task GetDataAsync_GoodPattern()
{
    var result = await _service.GetDataAsync();
    Assert.That(result, Is.Not.Null);
}
```

### Blocking on Async Code

```csharp
// WRONG - Blocking can cause deadlocks
[Test]
public void GetData_Blocking()
{
    var result = _service.GetDataAsync().Result; // Deadlock risk
    Assert.That(result, Is.Not.Null);
}

// WRONG - GetAwaiter().GetResult() is just as bad
[Test]
public void GetData_GetAwaiter()
{
    var result = _service.GetDataAsync().GetAwaiter().GetResult();
    Assert.That(result, Is.Not.Null);
}

// CORRECT - Use async/await
[Test]
public async Task GetData_Async()
{
    var result = await _service.GetDataAsync();
    Assert.That(result, Is.Not.Null);
}
```

## Setup/Teardown Anti-Patterns

### Heavy OneTimeSetUp

```csharp
// WRONG - Too much work in OneTimeSetUp
[TestFixture]
public class HeavySetupTests
{
    private List<User> _allUsers;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Creates 10000 users - slow and wasteful
        _allUsers = new List<User>();
        for (int i = 0; i < 10000; i++)
        {
            _allUsers.Add(await _service.CreateUserAsync($"user{i}@test.com"));
        }
    }

    [Test]
    public void TestOne()
    {
        // Only needs 1 user
        Assert.That(_allUsers[0].Email, Does.StartWith("user"));
    }
}

// CORRECT - Create only what each test needs
[TestFixture]
public class EfficientSetupTests
{
    [Test]
    public async Task TestOne()
    {
        var user = await _service.CreateUserAsync("user@test.com");
        Assert.That(user.Email, Does.StartWith("user"));
    }
}
```

### Missing Teardown for Resources

```csharp
// WRONG - Resource leak
[TestFixture]
public class LeakyTests
{
    private HttpClient _client;

    [SetUp]
    public void SetUp()
    {
        _client = new HttpClient();
    }

    [Test]
    public async Task Test()
    {
        var response = await _client.GetAsync("https://api.example.com");
        // _client is never disposed!
    }
}

// CORRECT - Proper cleanup
[TestFixture]
public class CleanTests
{
    private HttpClient _client;

    [SetUp]
    public void SetUp()
    {
        _client = new HttpClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
    }

    [Test]
    public async Task Test()
    {
        var response = await _client.GetAsync("https://api.example.com");
    }
}
```

## Assertion Anti-Patterns

### Swallowing Exceptions

```csharp
// WRONG - Exception is caught, test passes incorrectly
[Test]
public void BadExceptionHandling()
{
    try
    {
        _service.DoSomethingThatShouldThrow();
        // Test passes even if exception not thrown!
    }
    catch (Exception)
    {
        // Silently swallowed
    }
}

// CORRECT - Use Assert.Throws
[Test]
public void GoodExceptionHandling()
{
    Assert.Throws<InvalidOperationException>(
        () => _service.DoSomethingThatShouldThrow());
}
```

### Assert.Pass() Abuse

```csharp
// WRONG - Using Assert.Pass to skip actual assertions
[Test]
public void SkippedTest()
{
    if (!FeatureFlag.IsEnabled)
    {
        Assert.Pass("Feature not enabled"); // Hides untested code
    }

    // Never reaches actual test
    var result = _service.NewFeature();
    Assert.That(result, Is.Not.Null);
}

// CORRECT - Use Assume or Ignore
[Test]
public void ConditionalTest()
{
    Assume.That(FeatureFlag.IsEnabled, "Feature must be enabled");

    var result = _service.NewFeature();
    Assert.That(result, Is.Not.Null);
}

// Or use explicit Ignore
[Test]
[Ignore("Feature not yet implemented")]
public void FutureFeatureTest()
{
    var result = _service.NewFeature();
    Assert.That(result, Is.Not.Null);
}
```

### Weak Assertions

```csharp
// WRONG - Assertion passes but doesn't verify behavior
[Test]
public void WeakAssertion()
{
    var result = _service.GetUsers();
    Assert.That(result, Is.Not.Null); // Only checks not null
    // Doesn't verify content, count, or correctness
}

// CORRECT - Specific assertions
[Test]
public void StrongAssertion()
{
    var result = _service.GetActiveUsers();

    Assert.Multiple(() =>
    {
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.GreaterThan(0));
        Assert.That(result, Is.All.Property("IsActive").True);
    });
}
```

## Test Data Anti-Patterns

### Hardcoded Magic Values

```csharp
// WRONG - Magic numbers without context
[Test]
public void ProcessOrder_MagicNumbers()
{
    var order = new Order { Quantity = 5, Price = 10.99m };
    var result = _calculator.CalculateTotal(order);
    Assert.That(result, Is.EqualTo(54.95m)); // Where does 54.95 come from?
}

// CORRECT - Clear calculation or named constants
[Test]
public void ProcessOrder_ClearCalculation()
{
    const int quantity = 5;
    const decimal pricePerUnit = 10.99m;
    const decimal expectedTotal = quantity * pricePerUnit;

    var order = new Order { Quantity = quantity, Price = pricePerUnit };
    var result = _calculator.CalculateTotal(order);

    Assert.That(result, Is.EqualTo(expectedTotal));
}
```

### DateTime.Now in Tests

```csharp
// WRONG - Non-deterministic test
[Test]
public void CreateUser_SetsCreatedAt()
{
    var user = _service.CreateUser("test@example.com");
    Assert.That(user.CreatedAt, Is.EqualTo(DateTime.Now)); // Race condition!
}

// CORRECT - Use tolerance or inject time
[Test]
public void CreateUser_SetsCreatedAt_WithTolerance()
{
    var before = DateTime.UtcNow;
    var user = _service.CreateUser("test@example.com");
    var after = DateTime.UtcNow;

    Assert.That(user.CreatedAt, Is.InRange(before, after));
}

// BETTER - Inject clock abstraction
[Test]
public void CreateUser_SetsCreatedAt_WithClock()
{
    var fixedTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    var clock = Substitute.For<IClock>();
    clock.UtcNow.Returns(fixedTime);

    var service = new UserService(clock);
    var user = service.CreateUser("test@example.com");

    Assert.That(user.CreatedAt, Is.EqualTo(fixedTime));
}
```

## Mocking Anti-Patterns

### Over-Mocking

```csharp
// WRONG - Mocking everything, testing nothing
[Test]
public void OverMockedTest()
{
    var mockRepo = Substitute.For<IRepository>();
    var mockLogger = Substitute.For<ILogger>();
    var mockCache = Substitute.For<ICache>();
    var mockValidator = Substitute.For<IValidator>();

    mockValidator.Validate(Arg.Any<User>()).Returns(true);
    mockRepo.Save(Arg.Any<User>()).Returns(true);

    var service = new UserService(mockRepo, mockLogger, mockCache, mockValidator);
    var result = service.CreateUser("test@example.com");

    Assert.That(result, Is.True);
    // What did we actually test? Just that mocks return what we told them to.
}

// CORRECT - Use real objects where possible
[Test]
public void BetterTest()
{
    var realValidator = new UserValidator();
    var mockRepo = Substitute.For<IRepository>();

    var service = new UserService(mockRepo, NullLogger.Instance, realValidator);
    var result = service.CreateUser("test@example.com");

    // Now we're actually testing validation + service integration
    mockRepo.Received(1).Save(Arg.Is<User>(u => u.Email == "test@example.com"));
}
```
