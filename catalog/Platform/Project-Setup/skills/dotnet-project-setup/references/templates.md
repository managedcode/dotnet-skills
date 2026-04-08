# Common Project Templates

## Overview

.NET provides project templates via `dotnet new`. This reference covers common templates and their typical use cases.

---

## Listing Available Templates

```bash
# List all installed templates
dotnet new list

# Search for templates
dotnet new search webapi

# Install a template pack
dotnet new install Microsoft.AspNetCore.SpaTemplates
```

---

## Console Applications

### Basic Console App

```bash
dotnet new console -n MyApp -o src/MyApp
```

**Generated structure:**

```text
src/MyApp/
├── MyApp.csproj
└── Program.cs
```

**Typical `Program.cs`:**

```csharp
// Minimal hosting for console apps with DI
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<MyService>();

using var host = builder.Build();
await host.RunAsync();
```

---

## Class Libraries

### Standard Library

```bash
dotnet new classlib -n MyProduct.Core -o src/MyProduct.Core
```

### Library with Multi-Targeting

**Modify the generated `.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net9.0;net8.0;netstandard2.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>MyCompany.MyProduct.Core</PackageId>
    <Description>Core library for MyProduct</Description>
  </PropertyGroup>
</Project>
```

---

## ASP.NET Core Web API

### Minimal API

```bash
dotnet new webapi -n MyProduct.Api -o src/MyProduct.Api --use-minimal-apis
```

**Typical structure:**

```text
src/MyProduct.Api/
├── MyProduct.Api.csproj
├── Program.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
└── appsettings.Development.json
```

**Minimal API `Program.cs` example:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }))
   .WithName("HealthCheck")
   .WithOpenApi();

app.Run();
```

### Controller-Based API

```bash
dotnet new webapi -n MyProduct.Api -o src/MyProduct.Api --use-controllers
```

**Controller template:**

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MyProduct.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await _itemService.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> GetById(Guid id, CancellationToken ct)
    {
        var item = await _itemService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ItemDto>> Create(CreateItemRequest request, CancellationToken ct)
    {
        var item = await _itemService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }
}
```

---

## Worker Services

### Background Service

```bash
dotnet new worker -n MyProduct.Worker -o src/MyProduct.Worker
```

**Worker template:**

```csharp
namespace MyProduct.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMyService>();
            await service.ProcessAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

---

## Web Applications

### Blazor Server

```bash
dotnet new blazor -n MyProduct.Web -o src/MyProduct.Web --interactivity Server
```

### Blazor WebAssembly

```bash
dotnet new blazor -n MyProduct.Web -o src/MyProduct.Web --interactivity WebAssembly
```

### Razor Pages

```bash
dotnet new webapp -n MyProduct.Web -o src/MyProduct.Web
```

---

## Test Projects

### xUnit Test Project

```bash
dotnet new xunit -n MyProduct.Core.Tests -o tests/MyProduct.Core.Tests
```

**Test project `.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Moq" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyProduct.Core\MyProduct.Core.csproj" />
  </ItemGroup>
</Project>
```

**Test class template:**

```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace MyProduct.Core.Tests;

public class ItemServiceTests
{
    private readonly Mock<IItemRepository> _repositoryMock;
    private readonly ItemService _sut;

    public ItemServiceTests()
    {
        _repositoryMock = new Mock<IItemRepository>();
        _sut = new ItemService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemExists_ReturnsItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var expected = new Item { Id = itemId, Name = "Test" };
        _repositoryMock
            .Setup(r => r.GetByIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetByIdAsync(itemId, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_WhenNameInvalid_ThrowsArgumentException(string? name)
    {
        // Arrange
        var request = new CreateItemRequest { Name = name! };

        // Act
        var act = () => _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

### NUnit Test Project

```bash
dotnet new nunit -n MyProduct.Core.Tests -o tests/MyProduct.Core.Tests
```

### MSTest Test Project

```bash
dotnet new mstest -n MyProduct.Core.Tests -o tests/MyProduct.Core.Tests
```

### Integration Test Project

```csharp
// WebApplicationFactory-based integration tests
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyProduct.Api.IntegrationTests;

public class ItemsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ItemsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace services for testing
                    services.AddSingleton<IItemRepository, InMemoryItemRepository>();
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/items");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```

---

## Solution Setup Commands

### Create Solution and Projects

```bash
# Create solution
dotnet new sln -n MyProduct

# Create projects
dotnet new classlib -n MyProduct.Core -o src/MyProduct.Core
dotnet new classlib -n MyProduct.Infrastructure -o src/MyProduct.Infrastructure
dotnet new webapi -n MyProduct.Api -o src/MyProduct.Api --use-minimal-apis
dotnet new xunit -n MyProduct.Core.Tests -o tests/MyProduct.Core.Tests
dotnet new xunit -n MyProduct.Api.IntegrationTests -o tests/MyProduct.Api.IntegrationTests

# Add projects to solution
dotnet sln add src/MyProduct.Core/MyProduct.Core.csproj
dotnet sln add src/MyProduct.Infrastructure/MyProduct.Infrastructure.csproj
dotnet sln add src/MyProduct.Api/MyProduct.Api.csproj
dotnet sln add tests/MyProduct.Core.Tests/MyProduct.Core.Tests.csproj
dotnet sln add tests/MyProduct.Api.IntegrationTests/MyProduct.Api.IntegrationTests.csproj

# Add project references
dotnet add src/MyProduct.Infrastructure/MyProduct.Infrastructure.csproj reference src/MyProduct.Core/MyProduct.Core.csproj
dotnet add src/MyProduct.Api/MyProduct.Api.csproj reference src/MyProduct.Core/MyProduct.Core.csproj
dotnet add src/MyProduct.Api/MyProduct.Api.csproj reference src/MyProduct.Infrastructure/MyProduct.Infrastructure.csproj
dotnet add tests/MyProduct.Core.Tests/MyProduct.Core.Tests.csproj reference src/MyProduct.Core/MyProduct.Core.csproj
dotnet add tests/MyProduct.Api.IntegrationTests/MyProduct.Api.IntegrationTests.csproj reference src/MyProduct.Api/MyProduct.Api.csproj
```

---

## .NET Aspire Application

### Aspire App Host

```bash
dotnet new aspire -n MyProduct
```

**Creates:**

```text
MyProduct/
├── MyProduct.AppHost/
│   ├── MyProduct.AppHost.csproj
│   └── Program.cs
├── MyProduct.ServiceDefaults/
│   ├── MyProduct.ServiceDefaults.csproj
│   └── Extensions.cs
└── MyProduct.sln
```

**AppHost `Program.cs`:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("ordersdb");

var api = builder.AddProject<Projects.MyProduct_Api>("api")
    .WithReference(cache)
    .WithReference(postgres);

builder.AddProject<Projects.MyProduct_Web>("web")
    .WithReference(api);

builder.Build().Run();
```

### Add Service to Existing Aspire Solution

```bash
# Add a new API project
dotnet new webapi -n MyProduct.OrdersApi -o src/MyProduct.OrdersApi
dotnet sln add src/MyProduct.OrdersApi/MyProduct.OrdersApi.csproj

# Reference ServiceDefaults
dotnet add src/MyProduct.OrdersApi/MyProduct.OrdersApi.csproj reference src/MyProduct.ServiceDefaults/MyProduct.ServiceDefaults.csproj
```

---

## gRPC Services

```bash
dotnet new grpc -n MyProduct.GrpcService -o src/MyProduct.GrpcService
```

**Proto file template:**

```protobuf
syntax = "proto3";

option csharp_namespace = "MyProduct.GrpcService";

package orders;

service OrderService {
  rpc GetOrder (GetOrderRequest) returns (OrderResponse);
  rpc CreateOrder (CreateOrderRequest) returns (OrderResponse);
  rpc ListOrders (ListOrdersRequest) returns (stream OrderResponse);
}

message GetOrderRequest {
  string order_id = 1;
}

message CreateOrderRequest {
  string customer_id = 1;
  repeated OrderItem items = 2;
}

message OrderItem {
  string product_id = 1;
  int32 quantity = 2;
}

message OrderResponse {
  string order_id = 1;
  string customer_id = 2;
  repeated OrderItem items = 3;
  string status = 4;
}

message ListOrdersRequest {
  string customer_id = 1;
}
```

---

## Tool Manifest

### Create Tool Manifest

```bash
dotnet new tool-manifest
```

**Creates `.config/dotnet-tools.json`:**

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {}
}
```

### Install Local Tools

```bash
dotnet tool install dotnet-ef
dotnet tool install dotnet-format
dotnet tool install dotnet-reportgenerator-globaltool
```

**Updated manifest:**

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "9.0.0",
      "commands": ["dotnet-ef"]
    },
    "dotnet-format": {
      "version": "5.1.250801",
      "commands": ["dotnet-format"]
    },
    "dotnet-reportgenerator-globaltool": {
      "version": "5.3.10",
      "commands": ["reportgenerator"]
    }
  }
}
```

### Restore Tools

```bash
dotnet tool restore
```

---

## Quick Reference: Common Template Options

| Template | Command | Key Options |
|----------|---------|-------------|
| Console | `dotnet new console` | `--use-program-main` |
| Class Library | `dotnet new classlib` | `--framework` |
| Web API | `dotnet new webapi` | `--use-controllers`, `--use-minimal-apis`, `--auth` |
| Blazor | `dotnet new blazor` | `--interactivity`, `--empty` |
| Worker | `dotnet new worker` | `--framework` |
| xUnit | `dotnet new xunit` | `--framework` |
| Solution | `dotnet new sln` | - |
| gitignore | `dotnet new gitignore` | - |
| editorconfig | `dotnet new editorconfig` | - |
| global.json | `dotnet new globaljson` | `--sdk-version`, `--roll-forward` |
