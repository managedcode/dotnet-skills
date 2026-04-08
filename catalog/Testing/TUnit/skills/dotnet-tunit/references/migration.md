# Migrating to TUnit

## From xUnit

### Package Changes

Remove:
```xml
<PackageReference Include="xunit" Version="*" />
<PackageReference Include="xunit.runner.visualstudio" Version="*" />
```

Add:
```xml
<PackageReference Include="TUnit" Version="*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
```

### Attribute Mappings

| xUnit | TUnit |
|-------|-------|
| `[Fact]` | `[Test]` |
| `[Theory]` | `[Test]` with data attributes |
| `[InlineData(...)]` | `[Arguments(...)]` |
| `[MemberData(...)]` | `[MethodDataSource(...)]` |
| `[ClassData(...)]` | `[ClassDataSource<T>]` |
| `[Trait("Category", "...")]` | `[Category("...")]` |
| `[Collection("...")]` | `[NotInParallel("...")]` |

### Constructor Injection

xUnit:
```csharp
public class MyTests
{
    private readonly ITestOutputHelper _output;

    public MyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestMethod()
    {
        _output.WriteLine("Test output");
        Assert.True(true);
    }
}
```

TUnit (with primary constructor):
```csharp
public class MyTests(ITestOutputHelper output)
{
    [Test]
    public async Task TestMethod()
    {
        output.WriteLine("Test output");
        await Assert.That(true).IsTrue();
    }
}
```

### Basic Test Conversion

xUnit:
```csharp
public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSum()
    {
        var calc = new Calculator();
        var result = calc.Add(2, 3);
        Assert.Equal(5, result);
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(-1, 1, 0)]
    public void Add_WithParameters_ReturnsExpected(int a, int b, int expected)
    {
        var calc = new Calculator();
        Assert.Equal(expected, calc.Add(a, b));
    }
}
```

TUnit:
```csharp
public class CalculatorTests
{
    [Test]
    public async Task Add_ReturnsSum()
    {
        var calc = new Calculator();
        var result = calc.Add(2, 3);
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    [Arguments(1, 1, 2)]
    [Arguments(2, 3, 5)]
    [Arguments(-1, 1, 0)]
    public async Task Add_WithParameters_ReturnsExpected(int a, int b, int expected)
    {
        var calc = new Calculator();
        await Assert.That(calc.Add(a, b)).IsEqualTo(expected);
    }
}
```

### MemberData to MethodDataSource

xUnit:
```csharp
public class DataTests
{
    public static IEnumerable<object[]> TestData =>
        new List<object[]>
        {
            new object[] { "hello", 5 },
            new object[] { "world", 5 }
        };

    [Theory]
    [MemberData(nameof(TestData))]
    public void Test_WithMemberData(string input, int expectedLength)
    {
        Assert.Equal(expectedLength, input.Length);
    }
}
```

TUnit:
```csharp
public class DataTests
{
    public static IEnumerable<(string, int)> TestData()
    {
        yield return ("hello", 5);
        yield return ("world", 5);
    }

    [Test]
    [MethodDataSource(nameof(TestData))]
    public async Task Test_WithMethodData(string input, int expectedLength)
    {
        await Assert.That(input.Length).IsEqualTo(expectedLength);
    }
}
```

### Lifecycle Hooks

xUnit:
```csharp
public class LifecycleTests : IDisposable, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Before each test
    }

    public async Task DisposeAsync()
    {
        // After each test
    }

    public void Dispose()
    {
        // Cleanup
    }
}
```

TUnit:
```csharp
public class LifecycleTests : IAsyncDisposable
{
    [Before(Test)]
    public async Task BeforeEachTest()
    {
        // Before each test
    }

    [After(Test)]
    public async Task AfterEachTest()
    {
        // After each test
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup
    }
}
```

### Collection Fixtures (Shared Context)

xUnit:
```csharp
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database")]
public class DatabaseTests
{
    private readonly DatabaseFixture _fixture;

    public DatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
}
```

TUnit:
```csharp
public class DatabaseTests
{
    private static DatabaseFixture _fixture = null!;

    [Before(Class)]
    public static async Task SetupFixture()
    {
        _fixture = new DatabaseFixture();
        await _fixture.InitializeAsync();
    }

    [After(Class)]
    public static async Task TeardownFixture()
    {
        await _fixture.DisposeAsync();
    }

    [Test]
    [NotInParallel("Database")]
    public async Task DatabaseTest()
    {
        // Use _fixture
    }
}
```

### Assertion Mappings

| xUnit | TUnit |
|-------|-------|
| `Assert.Equal(expected, actual)` | `await Assert.That(actual).IsEqualTo(expected)` |
| `Assert.NotEqual(unexpected, actual)` | `await Assert.That(actual).IsNotEqualTo(unexpected)` |
| `Assert.True(condition)` | `await Assert.That(condition).IsTrue()` |
| `Assert.False(condition)` | `await Assert.That(condition).IsFalse()` |
| `Assert.Null(obj)` | `await Assert.That(obj).IsNull()` |
| `Assert.NotNull(obj)` | `await Assert.That(obj).IsNotNull()` |
| `Assert.Empty(collection)` | `await Assert.That(collection).IsEmpty()` |
| `Assert.NotEmpty(collection)` | `await Assert.That(collection).IsNotEmpty()` |
| `Assert.Contains(item, collection)` | `await Assert.That(collection).Contains(item)` |
| `Assert.Throws<T>(action)` | `await Assert.That(action).ThrowsException().OfType<T>()` |
| `await Assert.ThrowsAsync<T>(func)` | `await Assert.That(func).ThrowsException().OfType<T>()` |

---

## From NUnit

### Package Changes

Remove:
```xml
<PackageReference Include="NUnit" Version="*" />
<PackageReference Include="NUnit3TestAdapter" Version="*" />
```

Add:
```xml
<PackageReference Include="TUnit" Version="*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
```

### Attribute Mappings

| NUnit | TUnit |
|-------|-------|
| `[Test]` | `[Test]` |
| `[TestCase(...)]` | `[Test]` with `[Arguments(...)]` |
| `[TestCaseSource(...)]` | `[MethodDataSource(...)]` |
| `[Category("...")]` | `[Category("...")]` |
| `[SetUp]` | `[Before(Test)]` |
| `[TearDown]` | `[After(Test)]` |
| `[OneTimeSetUp]` | `[Before(Class)]` |
| `[OneTimeTearDown]` | `[After(Class)]` |
| `[Ignore("...")]` | `[Skip("...")]` |
| `[Timeout(...)]` | `[Timeout(...)]` |
| `[Retry(...)]` | `[Retry(...)]` |
| `[Order(...)]` | `[DependsOn(...)]` |
| `[NonParallelizable]` | `[NotInParallel]` |

### Basic Test Conversion

NUnit:
```csharp
[TestFixture]
public class CalculatorTests
{
    private Calculator _calculator;

    [SetUp]
    public void Setup()
    {
        _calculator = new Calculator();
    }

    [Test]
    public void Add_ReturnsSum()
    {
        var result = _calculator.Add(2, 3);
        Assert.That(result, Is.EqualTo(5));
    }

    [TestCase(1, 1, 2)]
    [TestCase(2, 3, 5)]
    [TestCase(-1, 1, 0)]
    public void Add_WithParameters_ReturnsExpected(int a, int b, int expected)
    {
        Assert.That(_calculator.Add(a, b), Is.EqualTo(expected));
    }
}
```

TUnit:
```csharp
public class CalculatorTests
{
    private Calculator _calculator = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _calculator = new Calculator();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Add_ReturnsSum()
    {
        var result = _calculator.Add(2, 3);
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    [Arguments(1, 1, 2)]
    [Arguments(2, 3, 5)]
    [Arguments(-1, 1, 0)]
    public async Task Add_WithParameters_ReturnsExpected(int a, int b, int expected)
    {
        await Assert.That(_calculator.Add(a, b)).IsEqualTo(expected);
    }
}
```

### TestCaseSource to MethodDataSource

NUnit:
```csharp
public class DataTests
{
    private static IEnumerable<TestCaseData> TestCases()
    {
        yield return new TestCaseData("hello", 5).SetName("HelloCase");
        yield return new TestCaseData("world", 5).SetName("WorldCase");
    }

    [TestCaseSource(nameof(TestCases))]
    public void Test_WithTestCaseSource(string input, int expectedLength)
    {
        Assert.That(input.Length, Is.EqualTo(expectedLength));
    }
}
```

TUnit:
```csharp
public class DataTests
{
    public static IEnumerable<(string, int)> TestCases()
    {
        yield return ("hello", 5);
        yield return ("world", 5);
    }

    [Test]
    [MethodDataSource(nameof(TestCases))]
    public async Task Test_WithMethodData(string input, int expectedLength)
    {
        await Assert.That(input.Length).IsEqualTo(expectedLength);
    }
}
```

### OneTimeSetUp/OneTimeTearDown

NUnit:
```csharp
[TestFixture]
public class DatabaseTests
{
    private static Database _db;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _db = new Database();
        _db.Connect();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _db.Disconnect();
    }

    [Test]
    public void QueryTest()
    {
        var result = _db.Query("SELECT 1");
        Assert.That(result, Is.Not.Null);
    }
}
```

TUnit:
```csharp
public class DatabaseTests
{
    private static Database _db = null!;

    [Before(Class)]
    public static async Task OneTimeSetup()
    {
        _db = new Database();
        await _db.ConnectAsync();
    }

    [After(Class)]
    public static async Task OneTimeTeardown()
    {
        await _db.DisconnectAsync();
    }

    [Test]
    public async Task QueryTest()
    {
        var result = await _db.QueryAsync("SELECT 1");
        await Assert.That(result).IsNotNull();
    }
}
```

### Constraint-Based Assertions

| NUnit | TUnit |
|-------|-------|
| `Assert.That(x, Is.EqualTo(y))` | `await Assert.That(x).IsEqualTo(y)` |
| `Assert.That(x, Is.Not.EqualTo(y))` | `await Assert.That(x).IsNotEqualTo(y)` |
| `Assert.That(x, Is.Null)` | `await Assert.That(x).IsNull()` |
| `Assert.That(x, Is.Not.Null)` | `await Assert.That(x).IsNotNull()` |
| `Assert.That(x, Is.True)` | `await Assert.That(x).IsTrue()` |
| `Assert.That(x, Is.False)` | `await Assert.That(x).IsFalse()` |
| `Assert.That(x, Is.GreaterThan(y))` | `await Assert.That(x).IsGreaterThan(y)` |
| `Assert.That(x, Is.LessThan(y))` | `await Assert.That(x).IsLessThan(y)` |
| `Assert.That(list, Has.Count.EqualTo(n))` | `await Assert.That(list).HasCount(n)` |
| `Assert.That(list, Contains.Item(x))` | `await Assert.That(list).Contains(x)` |
| `Assert.That(list, Is.Empty)` | `await Assert.That(list).IsEmpty()` |
| `Assert.That(s, Does.StartWith("x"))` | `await Assert.That(s).StartsWith("x")` |
| `Assert.That(s, Does.Contain("x"))` | `await Assert.That(s).Contains("x")` |
| `Assert.That(() => x, Throws.TypeOf<T>())` | `await Assert.That(() => x).ThrowsException().OfType<T>()` |
| `Assert.Multiple(() => { ... })` | `await Assert.Multiple(() => { ... })` |

### Parallelism Control

NUnit:
```csharp
[TestFixture]
[NonParallelizable]
public class SequentialTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class AnotherSequentialTests { }
```

TUnit:
```csharp
[NotInParallel]
public class SequentialTests
{
    [Test]
    public async Task Test1() { }

    [Test]
    public async Task Test2() { }
}

// Or at the test level:
public class MixedParallelTests
{
    [Test]
    [NotInParallel]
    public async Task SequentialTest() { }

    [Test]
    public async Task ParallelTest() { }
}
```

---

## Common Migration Steps

1. **Update packages** in the project file
2. **Replace attributes** using the mapping tables above
3. **Convert assertions** to async TUnit assertions
4. **Update lifecycle hooks** from interfaces to attributes
5. **Add async/await** to test methods (TUnit assertions are async)
6. **Review parallelism** - TUnit is parallel by default
7. **Run tests** and fix any remaining compilation errors

### Automated Find-Replace Patterns

```
// xUnit
[Fact] -> [Test]
[Theory] -> [Test]
[InlineData( -> [Arguments(
Assert.Equal( -> await Assert.That(
Assert.True( -> await Assert.That(
Assert.NotNull( -> await Assert.That(

// NUnit
[TestCase( -> [Arguments(
[SetUp] -> [Before(Test)]
[TearDown] -> [After(Test)]
[OneTimeSetUp] -> [Before(Class)]
[OneTimeTearDown] -> [After(Class)]
Assert.That(x, Is.EqualTo(y)) -> await Assert.That(x).IsEqualTo(y)
```

### Post-Migration Checklist

- [ ] All tests compile
- [ ] All tests pass with `dotnet test`
- [ ] Parallel execution works correctly
- [ ] Shared state is properly isolated
- [ ] CI pipeline updated if needed
- [ ] TUnit analyzers enabled and warnings addressed
