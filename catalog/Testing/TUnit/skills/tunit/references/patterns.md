# TUnit Patterns

## Source Generation

TUnit uses source generators to discover and wire up tests at compile time rather than runtime reflection. This means:

- Tests are compiled as static method invocations
- Build errors surface test discovery issues early
- No runtime reflection overhead
- IDE support for test discovery depends on generator output

### Basic Test Structure

```csharp
public class CalculatorTests(ITestOutputHelper output)
{
    [Test]
    public async Task Add_ReturnsCorrectSum()
    {
        var calculator = new Calculator();
        var result = calculator.Add(2, 3);

        await Assert.That(result).IsEqualTo(5);
    }
}
```

### Parameterized Tests with Arguments

```csharp
public class MathTests
{
    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(0, 0, 0)]
    [Arguments(-1, 1, 0)]
    public async Task Add_WithVariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        await Assert.That(result).IsEqualTo(expected);
    }
}
```

### Matrix Tests

Combine multiple argument sources to create test matrices:

```csharp
public class MatrixTests
{
    [Test]
    [MatrixDataSource]
    public async Task ProcessData_HandlesAllCombinations(
        [Matrix("json", "xml", "csv")] string format,
        [Matrix(true, false)] bool compress)
    {
        var processor = new DataProcessor();
        var result = await processor.ProcessAsync(format, compress);

        await Assert.That(result.Success).IsTrue();
    }
}
```

### Method Data Source

```csharp
public class DataDrivenTests
{
    [Test]
    [MethodDataSource(nameof(GetTestCases))]
    public async Task Validate_WithMethodData(string input, bool expected)
    {
        var result = Validator.IsValid(input);
        await Assert.That(result).IsEqualTo(expected);
    }

    public static IEnumerable<(string, bool)> GetTestCases()
    {
        yield return ("valid@email.com", true);
        yield return ("invalid", false);
        yield return ("", false);
    }
}
```

### Class Data Source

```csharp
public sealed class SharedFixture : IAsyncInitializer, IAsyncDisposable
{
    public Task InitializeAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class ClassDataTests
{
    [Test]
    [ClassDataSource<SharedFixture>(Shared = SharedType.PerTestSession)]
    public async Task Process_WithSharedFixture(SharedFixture fixture)
    {
        await Assert.That(fixture).IsNotNull();
    }
}
```

Use `Shared = SharedType.PerTestSession` for expensive integration fixtures such as AppHost boot, `WebApplicationFactory`, or browser setup. For full distributed-app patterns, load `integration-testing.md`.

## Parallel Testing

TUnit runs tests in parallel by default. Design tests for isolation.

### Default Parallel Execution

All tests run in parallel unless explicitly constrained:

```csharp
public class ParallelTests
{
    [Test]
    public async Task Test1() => await Task.Delay(100);

    [Test]
    public async Task Test2() => await Task.Delay(100);

    // Both tests run simultaneously
}
```

### Controlling Parallelism

Disable parallelism for a specific test:

```csharp
public class SharedResourceTests
{
    [Test]
    [NotInParallel]
    public async Task Test_ThatModifiesGlobalState()
    {
        // Runs alone, not in parallel with other [NotInParallel] tests
    }
}
```

Group tests that must not run together:

```csharp
public class DatabaseTests
{
    [Test]
    [NotInParallel("Database")]
    public async Task CreateUser_InsertsRecord()
    {
        // Other tests with [NotInParallel("Database")] wait
    }

    [Test]
    [NotInParallel("Database")]
    public async Task DeleteUser_RemovesRecord()
    {
        // Runs sequentially with other "Database" group tests
    }
}
```

### Parallel Limits

Limit concurrent test execution:

```csharp
[assembly: ParallelLimiter<MaxParallelTests>]

public class MaxParallelTests : IParallelLimit
{
    public int Limit => Environment.ProcessorCount;
}
```

### Class-Level Parallelism Control

```csharp
[NotInParallel]
public class SequentialTestClass
{
    [Test]
    public async Task Test1() { }

    [Test]
    public async Task Test2() { }

    // All tests in this class run sequentially
}
```

## Assertions

TUnit provides fluent, async-first assertions.

### Basic Assertions

```csharp
[Test]
public async Task BasicAssertions()
{
    var value = 42;
    var text = "Hello";
    var list = new[] { 1, 2, 3 };

    await Assert.That(value).IsEqualTo(42);
    await Assert.That(value).IsGreaterThan(40);
    await Assert.That(value).IsLessThanOrEqualTo(42);

    await Assert.That(text).IsNotNull();
    await Assert.That(text).IsNotEmpty();
    await Assert.That(text).Contains("ell");
    await Assert.That(text).StartsWith("He");

    await Assert.That(list).HasCount(3);
    await Assert.That(list).Contains(2);
}
```

### Exception Assertions

```csharp
[Test]
public async Task ThrowsException()
{
    var action = () => throw new InvalidOperationException("test");

    await Assert.That(action).ThrowsException()
        .OfType<InvalidOperationException>()
        .WithMessage("test");
}

[Test]
public async Task ThrowsAsync()
{
    var asyncAction = async () =>
    {
        await Task.Delay(1);
        throw new ArgumentNullException("param");
    };

    await Assert.That(asyncAction).ThrowsException()
        .OfType<ArgumentNullException>();
}
```

### Collection Assertions

```csharp
[Test]
public async Task CollectionAssertions()
{
    var items = new[] { 1, 2, 3, 4, 5 };

    await Assert.That(items).HasCount(5);
    await Assert.That(items).Contains(3);
    await Assert.That(items).DoesNotContain(6);
    await Assert.That(items).AllSatisfy(x => x > 0);
    await Assert.That(items).IsEquivalentTo([5, 4, 3, 2, 1]);
    await Assert.That(items).IsInAscendingOrder();
}
```

### Object Assertions

```csharp
[Test]
public async Task ObjectAssertions()
{
    var user = new User("John", 30);

    await Assert.That(user).IsNotNull();
    await Assert.That(user.Name).IsEqualTo("John");
    await Assert.That(user.Age).IsGreaterThanOrEqualTo(18);
    await Assert.That(user).IsOfType<User>();
}
```

### Multiple Assertions

Group related assertions to report all failures:

```csharp
[Test]
public async Task MultipleAssertions()
{
    var result = new Result(true, "OK", 200);

    await Assert.Multiple(() =>
    {
        Assert.That(result.Success).IsTrue();
        Assert.That(result.Message).IsEqualTo("OK");
        Assert.That(result.Code).IsEqualTo(200);
    });
}
```

## Test Lifecycle Hooks

### Setup and Teardown

```csharp
public class LifecycleTests : IAsyncDisposable
{
    private HttpClient _client = null!;

    [Before(Test)]
    public async Task BeforeEachTest()
    {
        _client = new HttpClient();
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task AfterEachTest()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    [Test]
    public async Task TestWithHttpClient()
    {
        var response = await _client.GetAsync("https://example.com");
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }
}
```

### Class-Level Hooks

```csharp
public class ClassLevelHooks
{
    private static TestServer _server = null!;

    [Before(Class)]
    public static async Task BeforeAllTests()
    {
        _server = new TestServer();
        await _server.StartAsync();
    }

    [After(Class)]
    public static async Task AfterAllTests()
    {
        await _server.StopAsync();
    }

    [Test]
    public async Task Test1() { }

    [Test]
    public async Task Test2() { }
}
```

### Assembly-Level Hooks

```csharp
public class GlobalSetup
{
    [Before(Assembly)]
    public static async Task GlobalBeforeAll()
    {
        await Database.MigrateAsync();
    }

    [After(Assembly)]
    public static async Task GlobalAfterAll()
    {
        await Database.CleanupAsync();
    }
}
```

## Dependency Injection

### Constructor Injection with Primary Constructors

```csharp
public class ServiceTests(
    ITestOutputHelper output,
    CancellationToken cancellationToken)
{
    [Test]
    public async Task Service_DoesWork()
    {
        output.WriteLine("Starting test");

        var service = new MyService();
        await service.DoWorkAsync(cancellationToken);

        await Assert.That(service.IsComplete).IsTrue();
    }
}
```

### Using Test Context

```csharp
public class ContextTests(TestContext context)
{
    [Test]
    public async Task AccessTestMetadata()
    {
        var testName = context.TestDetails.TestName;
        var className = context.TestDetails.TestClass.Name;

        await Assert.That(testName).IsNotEmpty();
    }
}
```

## Test Dependencies

### Explicit Test Ordering

```csharp
public class OrderedTests
{
    private static int _counter;

    [Test]
    [DependsOn(nameof(First))]
    public async Task Second()
    {
        await Assert.That(_counter).IsEqualTo(1);
        _counter++;
    }

    [Test]
    public async Task First()
    {
        _counter = 1;
        await Task.CompletedTask;
    }

    [Test]
    [DependsOn(nameof(Second))]
    public async Task Third()
    {
        await Assert.That(_counter).IsEqualTo(2);
    }
}
```

## Timeouts

### Test-Level Timeout

```csharp
public class TimeoutTests
{
    [Test]
    [Timeout(5000)] // 5 seconds
    public async Task MustCompleteQuickly()
    {
        await Task.Delay(100);
        await Assert.That(true).IsTrue();
    }
}
```

### Class-Level Timeout

```csharp
[Timeout(10000)]
public class TimeBoundTests
{
    [Test]
    public async Task Test1() { }

    [Test]
    public async Task Test2() { }
}
```

## Categories and Filtering

### Test Categories

```csharp
public class CategorizedTests
{
    [Test]
    [Category("Unit")]
    public async Task UnitTest() { }

    [Test]
    [Category("Integration")]
    public async Task IntegrationTest() { }

    [Test]
    [Category("Unit")]
    [Category("Fast")]
    public async Task FastUnitTest() { }
}
```

### Skipping Tests

```csharp
public class SkipTests
{
    [Test]
    [Skip("Pending implementation")]
    public async Task NotYetImplemented() { }

    [Test]
    [SkipWhen(nameof(ShouldSkip))]
    public async Task ConditionallySkipped() { }

    public static bool ShouldSkip() =>
        !Environment.GetEnvironmentVariable("RUN_SLOW_TESTS")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;
}
```

## Custom Attributes

### Retry Failed Tests

```csharp
public class RetryTests
{
    [Test]
    [Retry(3)]
    public async Task FlakyTest()
    {
        // Will retry up to 3 times on failure
        var result = await ExternalService.CallAsync();
        await Assert.That(result.Success).IsTrue();
    }
}
```

### Repeat Tests

```csharp
public class RepeatTests
{
    [Test]
    [Repeat(5)]
    public async Task RunMultipleTimes()
    {
        // Runs 5 times to check for intermittent issues
        await Assert.That(true).IsTrue();
    }
}
```

## Running Tests

TUnit uses Microsoft.Testing.Platform. Prefer `dotnet run` over `dotnet test` for full CLI flag access.

### Basic Execution

```bash
# Run via test host (recommended)
dotnet run --project Tests.csproj

# Run via dotnet test
dotnet test Tests.csproj
```

### Filtering with --treenode-filter

TUnit uses `--treenode-filter`, not `--filter`. Syntax: `/<Assembly>/<Namespace>/<Class>/<Test>`

```bash
# All tests in a class
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/CalculatorTests/*"

# Specific test method
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/CalculatorTests/Add_ReturnsSum"

# Filter by namespace
dotnet run --project Tests.csproj -- --treenode-filter "/*/MyApp.Tests.Unit/*/*"

# Filter by category
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/*/*[Category=Unit]"

# Exclude category
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/*/*[Category!=Slow]"

# Multiple filters (OR)
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/ClassA/*|/*/*/ClassB/*"

# Combine filters (AND)
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/*/*[Category=Unit][Priority=High]"

# Custom property filter
dotnet run --project Tests.csproj -- --treenode-filter "/*/*/*/*[Owner=TeamA]"
```

### Other CLI Options

```bash
# List available tests
dotnet run --project Tests.csproj -- --list-tests

# Run with specific timeout
dotnet run --project Tests.csproj -- --timeout 60000

# Output detailed results
dotnet run --project Tests.csproj -- --results-directory ./results
```
