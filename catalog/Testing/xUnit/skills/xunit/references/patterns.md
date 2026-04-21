# xUnit Testing Patterns

## Arrange-Act-Assert (AAA)

Structure every test method in three distinct phases:

```csharp
public class CalculatorTests
{
    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        var calculator = new Calculator();
        var a = 5;
        var b = 3;

        // Act
        var result = calculator.Add(a, b);

        // Assert
        Assert.Equal(8, result);
    }
}
```

Keep the phases visually separated. Avoid mixing setup, execution, and verification in the same block.

## Class Fixtures

Use class fixtures to share expensive setup across all tests in a class. Primary constructors inject the fixture directly:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public IDbConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        await Connection.OpenAsync();
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // seed shared test data
    }
}

public class UserRepositoryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task GetUser_ExistingId_ReturnsUser()
    {
        // Arrange
        var repository = new UserRepository(fixture.Connection);

        // Act
        var user = await repository.GetUserAsync(1);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("TestUser", user.Name);
    }
}
```

## Collection Fixtures

Share expensive resources across multiple test classes:

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>;

[Collection("Integration")]
public class OrdersApiTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetOrders_ReturnsOk()
    {
        // Act
        var response = await fixture.Client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}

[Collection("Integration")]
public class ProductsApiTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        // Act
        var response = await fixture.Client.GetAsync("/api/products");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```

## Theory Data Patterns

### InlineData for Simple Cases

```csharp
public class ValidationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("valid-password-123", true)]
    public void IsValidPassword_VariousInputs_ReturnsExpected(string password, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator(minLength: 3);

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.Equal(expected, result);
    }
}
```

### MemberData for Complex Objects

```csharp
public class OrderProcessorTests
{
    public static TheoryData<Order, decimal> OrderDiscountData => new()
    {
        { new Order { Total = 100m, CustomerTier = CustomerTier.Standard }, 0m },
        { new Order { Total = 100m, CustomerTier = CustomerTier.Silver }, 5m },
        { new Order { Total = 100m, CustomerTier = CustomerTier.Gold }, 10m },
        { new Order { Total = 500m, CustomerTier = CustomerTier.Gold }, 75m }
    };

    [Theory]
    [MemberData(nameof(OrderDiscountData))]
    public void CalculateDiscount_VariousOrders_ReturnsExpectedDiscount(Order order, decimal expectedDiscount)
    {
        // Arrange
        var processor = new OrderProcessor();

        // Act
        var discount = processor.CalculateDiscount(order);

        // Assert
        Assert.Equal(expectedDiscount, discount);
    }
}
```

### ClassData for Reusable Data Sets

```csharp
public class EdgeCaseStringData : TheoryData<string?>
{
    public EdgeCaseStringData()
    {
        Add(null);
        Add("");
        Add(" ");
        Add("\t");
        Add("\n");
        Add("   \t\n   ");
    }
}

public class StringUtilityTests
{
    [Theory]
    [ClassData(typeof(EdgeCaseStringData))]
    public void IsNullOrWhitespace_EdgeCases_ReturnsTrue(string? input)
    {
        // Act
        var result = string.IsNullOrWhiteSpace(input);

        // Assert
        Assert.True(result);
    }
}
```

## Mocking with NSubstitute

Prefer NSubstitute for readable substitute configuration:

```csharp
public class OrderServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PlaceOrder_ValidOrder_SendsNotification()
    {
        // Arrange
        var notificationService = Substitute.For<INotificationService>();
        var orderRepository = Substitute.For<IOrderRepository>();
        orderRepository.SaveAsync(Arg.Any<Order>()).Returns(Task.FromResult(true));

        var service = new OrderService(orderRepository, notificationService);
        var order = new Order { CustomerId = 1, Items = [new OrderItem { ProductId = 1, Quantity = 2 }] };

        // Act
        await service.PlaceOrderAsync(order);

        // Assert
        await notificationService.Received(1).SendOrderConfirmationAsync(Arg.Is<Order>(o => o.CustomerId == 1));
        output.WriteLine("Order placed and notification sent");
    }

    [Fact]
    public async Task PlaceOrder_RepositoryFails_ThrowsException()
    {
        // Arrange
        var notificationService = Substitute.For<INotificationService>();
        var orderRepository = Substitute.For<IOrderRepository>();
        orderRepository.SaveAsync(Arg.Any<Order>()).ThrowsAsync(new DataException("Connection failed"));

        var service = new OrderService(orderRepository, notificationService);
        var order = new Order { CustomerId = 1 };

        // Act & Assert
        await Assert.ThrowsAsync<DataException>(() => service.PlaceOrderAsync(order));
        await notificationService.DidNotReceive().SendOrderConfirmationAsync(Arg.Any<Order>());
    }
}
```

## Mocking with Moq

Use Moq when the project already depends on it:

```csharp
public class PaymentProcessorTests
{
    [Fact]
    public async Task ProcessPayment_ValidCard_ReturnsSuccess()
    {
        // Arrange
        var gatewayMock = new Mock<IPaymentGateway>();
        gatewayMock
            .Setup(g => g.ChargeAsync(It.IsAny<string>(), It.Is<decimal>(d => d > 0)))
            .ReturnsAsync(new PaymentResult { Success = true, TransactionId = "TX123" });

        var processor = new PaymentProcessor(gatewayMock.Object);

        // Act
        var result = await processor.ProcessPaymentAsync("4111111111111111", 99.99m);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("TX123", result.TransactionId);
        gatewayMock.Verify(g => g.ChargeAsync("4111111111111111", 99.99m), Times.Once);
    }
}
```

## Output and Diagnostics

Use `ITestOutputHelper` for test diagnostics:

```csharp
public class DiagnosticTests(ITestOutputHelper output)
{
    [Fact]
    public void ComplexCalculation_LargeInput_CompletesWithinTimeout()
    {
        // Arrange
        var calculator = new ComplexCalculator();
        var input = GenerateLargeInput();
        output.WriteLine($"Testing with input size: {input.Length}");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = calculator.Process(input);

        // Assert
        stopwatch.Stop();
        output.WriteLine($"Completed in {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Calculation took too long");
    }

    private static int[] GenerateLargeInput() => Enumerable.Range(0, 100_000).ToArray();
}
```

## Async Test Patterns

xUnit handles async tests natively:

```csharp
public class AsyncServiceTests
{
    [Fact]
    public async Task FetchData_ValidEndpoint_ReturnsData()
    {
        // Arrange
        var service = new DataService();

        // Act
        var data = await service.FetchDataAsync("/api/items");

        // Assert
        Assert.NotEmpty(data);
    }

    [Fact]
    public async Task FetchData_Timeout_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new DataService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.FetchDataAsync("/api/slow-endpoint", cts.Token));
    }
}
```

## Trait-Based Organization

Use traits sparingly for CI filtering:

```csharp
public class IntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task CreateUser_ValidData_PersistsToDatabase()
    {
        // integration test implementation
    }
}

public class UnitTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateEmail_InvalidFormat_ReturnsFalse()
    {
        // unit test implementation
    }
}
```

Run filtered:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category!=Integration"
```

## Sources

- [xUnit.net Theory Data](https://xunit.net/docs/getting-started/v3/theory-data)
- [xUnit.net Shared Context](https://xunit.net/docs/shared-context)
- [NSubstitute Documentation](https://nsubstitute.github.io/help/getting-started/)
