# NUnit Patterns Reference

## Test Organization Patterns

### Fixture Per Feature

```csharp
[TestFixture]
public class UserRegistrationTests
{
    [Test]
    public void Register_ValidUser_CreatesAccount() { }

    [Test]
    public void Register_DuplicateEmail_ReturnsError() { }
}

[TestFixture]
public class UserAuthenticationTests
{
    [Test]
    public void Login_ValidCredentials_ReturnsToken() { }

    [Test]
    public void Login_InvalidPassword_ReturnsUnauthorized() { }
}
```

### Nested Test Fixtures

```csharp
[TestFixture]
public class OrderServiceTests
{
    [TestFixture]
    public class WhenCreatingOrder
    {
        [Test]
        public void WithValidItems_CreatesOrder() { }

        [Test]
        public void WithEmptyCart_ThrowsException() { }
    }

    [TestFixture]
    public class WhenCancellingOrder
    {
        [Test]
        public void BeforeShipment_RefundsPayment() { }

        [Test]
        public void AfterShipment_ThrowsException() { }
    }
}
```

## Data-Driven Testing Patterns

### Values Attribute for Simple Types

```csharp
[Test]
public void IsPositive_VariousNumbers_ReturnsExpected(
    [Values(1, 5, 100, int.MaxValue)] int positive,
    [Values(-1, -100, int.MinValue)] int negative)
{
    Assert.That(MathHelper.IsPositive(positive), Is.True);
    Assert.That(MathHelper.IsPositive(negative), Is.False);
}

[Test]
public void ParseBoolean_VariousStrings(
    [Values("true", "True", "TRUE", "1", "yes")] string trueValue)
{
    Assert.That(Parser.ParseBoolean(trueValue), Is.True);
}
```

### Range Attribute

```csharp
[Test]
public void CalculateDiscount_QuantityRange(
    [Range(1, 10)] int quantity)
{
    var discount = _service.CalculateDiscount(quantity);
    Assert.That(discount, Is.GreaterThanOrEqualTo(0));
}
```

### Combinatorial and Pairwise

```csharp
// Tests all combinations (3 x 3 x 2 = 18 tests)
[Test, Combinatorial]
public void TestAllCombinations(
    [Values("small", "medium", "large")] string size,
    [Values("red", "green", "blue")] string color,
    [Values(true, false)] bool express)
{
    var order = CreateOrder(size, color, express);
    Assert.That(order, Is.Not.Null);
}

// Tests pairwise combinations (fewer tests, still good coverage)
[Test, Pairwise]
public void TestPairwiseCombinations(
    [Values("small", "medium", "large")] string size,
    [Values("red", "green", "blue")] string color,
    [Values(true, false)] bool express)
{
    var order = CreateOrder(size, color, express);
    Assert.That(order, Is.Not.Null);
}
```

### TestCaseSource with Named Tests

```csharp
private static IEnumerable<TestCaseData> EdgeCaseTestData()
{
    yield return new TestCaseData(null)
        .SetName("NullInput")
        .SetDescription("Verifies null handling");

    yield return new TestCaseData("")
        .SetName("EmptyString")
        .SetDescription("Verifies empty string handling");

    yield return new TestCaseData("   ")
        .SetName("WhitespaceOnly")
        .SetDescription("Verifies whitespace handling");
}

[TestCaseSource(nameof(EdgeCaseTestData))]
public void Validate_EdgeCases_HandlesGracefully(string input)
{
    Assert.DoesNotThrow(() => _validator.Validate(input));
}
```

## Assertion Patterns

### Multiple Assertions

```csharp
[Test]
public void CreateUser_ValidInput_SetsAllProperties()
{
    var user = _service.CreateUser("john@example.com", "John Doe");

    Assert.Multiple(() =>
    {
        Assert.That(user.Email, Is.EqualTo("john@example.com"));
        Assert.That(user.Name, Is.EqualTo("John Doe"));
        Assert.That(user.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(1).Seconds);
        Assert.That(user.IsActive, Is.True);
        Assert.That(user.Id, Is.Not.EqualTo(Guid.Empty));
    });
}
```

### Custom Constraints

```csharp
public class ValidEmailConstraint : Constraint
{
    public override ConstraintResult ApplyTo<TActual>(TActual actual)
    {
        var email = actual as string;
        var isValid = !string.IsNullOrEmpty(email)
            && email.Contains("@")
            && email.Contains(".");

        return new ConstraintResult(this, actual, isValid);
    }

    public override string Description => "a valid email address";
}

public static class Is2
{
    public static ValidEmailConstraint ValidEmail => new ValidEmailConstraint();
}

[Test]
public void User_Email_IsValid()
{
    var user = new User { Email = "test@example.com" };
    Assert.That(user.Email, Is2.ValidEmail);
}
```

### Fluent Collection Assertions

```csharp
[Test]
public void GetActiveUsers_ReturnsCorrectCollection()
{
    var users = _service.GetActiveUsers();

    Assert.That(users, Has.Count.GreaterThan(0)
        .And.All.Property("IsActive").EqualTo(true)
        .And.None.Property("Email").Null
        .And.Some.Property("Role").EqualTo("Admin"));
}

[Test]
public void GetProducts_SortedByPrice()
{
    var products = _service.GetProductsSortedByPrice();

    Assert.That(products.Select(p => p.Price), Is.Ordered.Ascending);
}
```

## Setup and Lifecycle Patterns

### Dependency Injection in Tests

```csharp
[TestFixture]
public class ServiceTests
{
    private ServiceProvider _serviceProvider;
    private IMyService _service;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMyService, MyService>();
        services.AddSingleton<ILogger, TestLogger>();
        _serviceProvider = services.BuildServiceProvider();
    }

    [SetUp]
    public void SetUp()
    {
        _service = _serviceProvider.GetRequiredService<IMyService>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider?.Dispose();
    }
}
```

### Test Context Access

```csharp
[TestFixture]
public class ContextAwareTests
{
    [SetUp]
    public void SetUp()
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var testDirectory = TestContext.CurrentContext.TestDirectory;

        Console.WriteLine($"Running: {testName}");
    }

    [TearDown]
    public void TearDown()
    {
        var result = TestContext.CurrentContext.Result.Outcome.Status;

        if (result == TestStatus.Failed)
        {
            // Capture screenshot, logs, etc.
            CaptureFailureArtifacts();
        }
    }
}
```

## Parallel Execution Patterns

### Fixture-Level Parallelism

```csharp
[TestFixture]
[Parallelizable(ParallelScope.Fixtures)]
public class ParallelFixtureTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}
```

### Test-Level Parallelism

```csharp
[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class ParallelTestsWithinFixture
{
    // Each test runs in parallel
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }

    [Test]
    public void Test3() { }
}
```

### Non-Parallelizable Tests

```csharp
[TestFixture]
[NonParallelizable]
public class SequentialTests
{
    // Tests that must run sequentially (shared resource, etc.)
}
```

## Retry and Timeout Patterns

### Retry Flaky Tests

```csharp
[Test]
[Retry(3)]
public void FlakyNetworkTest()
{
    // Will retry up to 3 times if fails
    var result = _httpClient.GetAsync("https://api.example.com").Result;
    Assert.That(result.IsSuccessStatusCode, Is.True);
}
```

### Test Timeout

```csharp
[Test]
[Timeout(5000)] // 5 seconds
public void LongRunningTest()
{
    // Test will fail if takes longer than 5 seconds
    _service.ProcessLargeDataSet();
}
```

## Theory Tests (NUnit 4)

```csharp
[TestFixture]
public class TheoryTests
{
    [Theory]
    public void Sqrt_PositiveNumber_ReturnsPositive(double value)
    {
        Assume.That(value > 0);
        var result = Math.Sqrt(value);
        Assert.That(result, Is.GreaterThan(0));
    }

    [Datapoint] public double Zero = 0;
    [Datapoint] public double One = 1;
    [Datapoint] public double Pi = Math.PI;
    [Datapoint] public double Negative = -1;
}
```
