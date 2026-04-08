---
name: dotnet-nunit
version: "1.0.0"
category: "Testing"
description: "Write, run, or repair .NET tests that use NUnit. Use when a repo uses `NUnit`, `[Test]`, `[TestCase]`, `[TestFixture]`, or NUnit3TestAdapter for VSTest or Microsoft.Testing.Platform execution."
compatibility: "Requires NUnit 3.x or 4.x packages and appropriate test adapter."
---

# NUnit Testing

## Trigger On

- writing or reviewing NUnit tests
- using `[Test]`, `[TestCase]`, `[TestFixture]`, `[SetUp]`, `[TearDown]` attributes
- configuring NUnit3TestAdapter or NUnit.Analyzers
- migrating between NUnit versions
- integrating NUnit with CI pipelines

## Documentation

- [NUnit Documentation](https://docs.nunit.org/)
- [NUnit GitHub](https://github.com/nunit/nunit)
- [NUnit3TestAdapter](https://github.com/nunit/nunit3-vs-adapter)
- [NUnit Analyzers](https://github.com/nunit/nunit.analyzers)

### References

- [patterns.md](references/patterns.md) — Test patterns, assertions, parameterized tests, lifecycle
- [anti-patterns.md](references/anti-patterns.md) — Common NUnit mistakes and fixes

## Package Selection

| Package | Purpose |
|---------|---------|
| `NUnit` | Core testing framework |
| `NUnit3TestAdapter` | VSTest adapter for `dotnet test` |
| `NUnit.Analyzers` | Roslyn analyzers for NUnit best practices |
| `Microsoft.NET.Test.Sdk` | Required for test discovery |

## Project Setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="NUnit" Version="4.*" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.*" />
    <PackageReference Include="NUnit.Analyzers" Version="4.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

## Test Patterns

### Basic Test Structure

```csharp
[TestFixture]
public class CalculatorTests
{
    private Calculator _calculator;

    [SetUp]
    public void SetUp()
    {
        _calculator = new Calculator();
    }

    [TearDown]
    public void TearDown()
    {
        _calculator?.Dispose();
    }

    [Test]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        var result = _calculator.Add(2, 3);

        Assert.That(result, Is.EqualTo(5));
    }

    [Test]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
    }
}
```

### Parameterized Tests with TestCase

```csharp
[TestFixture]
public class ValidationTests
{
    [TestCase("", false)]
    [TestCase("a", false)]
    [TestCase("ab", false)]
    [TestCase("abc", true)]
    [TestCase("valid@email.com", true)]
    public void IsValid_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        var result = Validator.IsValid(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(1, 2, ExpectedResult = 3)]
    [TestCase(-1, 1, ExpectedResult = 0)]
    [TestCase(100, 200, ExpectedResult = 300)]
    public int Add_TestCases_ReturnsExpectedResult(int a, int b)
    {
        return _calculator.Add(a, b);
    }
}
```

### TestCaseSource for Complex Data

```csharp
[TestFixture]
public class OrderTests
{
    private static IEnumerable<TestCaseData> OrderTestCases()
    {
        yield return new TestCaseData(
            new Order { Items = new[] { new Item { Price = 10 }, new Item { Price = 20 } } },
            30m
        ).SetName("TwoItems_CalculatesTotal");

        yield return new TestCaseData(
            new Order { Items = Array.Empty<Item>() },
            0m
        ).SetName("EmptyOrder_ReturnsZero");

        yield return new TestCaseData(
            new Order { Items = new[] { new Item { Price = 100 } }, DiscountPercent = 10 },
            90m
        ).SetName("WithDiscount_AppliesDiscount");
    }

    [TestCaseSource(nameof(OrderTestCases))]
    public void CalculateTotal_VariousOrders_ReturnsExpected(Order order, decimal expected)
    {
        var result = order.CalculateTotal();

        Assert.That(result, Is.EqualTo(expected));
    }
}
```

### Constraint-Based Assertions

```csharp
[Test]
public void AssertionExamples()
{
    // Equality
    Assert.That(actual, Is.EqualTo(expected));
    Assert.That(actual, Is.Not.EqualTo(other));

    // Comparison
    Assert.That(value, Is.GreaterThan(5));
    Assert.That(value, Is.LessThanOrEqualTo(10));
    Assert.That(value, Is.InRange(1, 100));

    // String
    Assert.That(str, Does.StartWith("Hello"));
    Assert.That(str, Does.Contain("world"));
    Assert.That(str, Does.Match(@"\d{3}-\d{4}"));
    Assert.That(str, Is.EqualTo("HELLO").IgnoreCase);

    // Collection
    Assert.That(list, Has.Count.EqualTo(5));
    Assert.That(list, Contains.Item("expected"));
    Assert.That(list, Is.All.GreaterThan(0));
    Assert.That(list, Is.Unique);
    Assert.That(list, Is.Ordered);
    Assert.That(list, Has.Exactly(3).Items.GreaterThan(10));

    // Type
    Assert.That(obj, Is.InstanceOf<MyClass>());
    Assert.That(obj, Is.AssignableTo<IMyInterface>());

    // Null
    Assert.That(obj, Is.Null);
    Assert.That(obj, Is.Not.Null);

    // Boolean
    Assert.That(condition, Is.True);
    Assert.That(condition, Is.False);

    // Exception
    Assert.That(() => DoSomething(), Throws.TypeOf<InvalidOperationException>());
    Assert.That(() => DoSomething(), Throws.Exception.With.Message.Contains("error"));

    // Async
    Assert.That(async () => await DoAsync(), Throws.Nothing);
}
```

### Async Test Support

```csharp
[TestFixture]
public class AsyncServiceTests
{
    private IAsyncService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new AsyncService();
    }

    [Test]
    public async Task GetDataAsync_ValidId_ReturnsData()
    {
        var result = await _service.GetDataAsync(1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(1));
    }

    [Test]
    public void GetDataAsync_InvalidId_ThrowsException()
    {
        Assert.ThrowsAsync<NotFoundException>(
            async () => await _service.GetDataAsync(-1));
    }

    [Test]
    public async Task ProcessAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _service.ProcessAsync(cts.Token));
    }
}
```

### OneTimeSetUp and OneTimeTearDown

```csharp
[TestFixture]
public class IntegrationTests
{
    private static TestServer _server;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Runs once before all tests in fixture
        _server = new TestServer(new WebHostBuilder().UseStartup<Startup>());
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Runs once after all tests in fixture
        _server?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        // Runs before each test
        _client = _server.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        // Runs after each test
        _client?.Dispose();
    }

    [Test]
    public async Task GetEndpoint_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/data");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}
```

## Categories and Filtering

```csharp
[TestFixture]
[Category("Integration")]
public class DatabaseTests
{
    [Test]
    [Category("Slow")]
    public void SlowDatabaseTest() { }

    [Test]
    [Category("Fast")]
    public void FastDatabaseTest() { }
}

// Run specific categories:
// dotnet test --filter "Category=Fast"
// dotnet test --filter "Category!=Slow"
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Multiple asserts without clear purpose | Hard to identify which assertion failed | One logical assertion per test or use `Assert.Multiple` |
| Test interdependence | Tests fail unpredictably | Each test should be independent |
| Hardcoded test data | Brittle tests | Use `TestCase` or `TestCaseSource` |
| Testing implementation details | Breaks on refactoring | Test behavior, not internals |
| Missing `[SetUp]`/`[TearDown]` cleanup | Resource leaks | Always clean up resources |
| Classic Assert syntax | Less readable | Use constraint model (`Assert.That`) |

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --logger "console;verbosity=detailed"

# Filter by name
dotnet test --filter "FullyQualifiedName~CalculatorTests"

# Filter by category
dotnet test --filter "Category=Unit"

# Run in parallel
dotnet test -- NUnit.NumberOfTestWorkers=4
```

## Deliver

- NUnit tests following the Arrange-Act-Assert pattern
- Parameterized tests with `[TestCase]` and `[TestCaseSource]`
- Constraint-based assertions with `Assert.That`
- Proper test lifecycle management

## Validate

- Tests are independent and isolated
- No hardcoded test data where parameterization is appropriate
- Async tests use `async Task` not `async void`
- Resources are properly disposed in `[TearDown]` or `[OneTimeTearDown]`
- NUnit.Analyzers enabled to catch common issues
