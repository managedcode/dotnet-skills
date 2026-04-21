# NetArchTest Common Architecture Rules

This reference provides ready-to-use architecture test examples for common .NET project structures.

## Clean Architecture / Onion Architecture

### Domain Layer Independence

The domain layer should have no dependencies on infrastructure or application layers:

```csharp
[Fact]
public void Domain_ShouldNotDependOn_Infrastructure()
{
    var result = Types
        .InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("MyApp.Infrastructure")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"Domain depends on Infrastructure: {FormatFailures(result)}");
}

[Fact]
public void Domain_ShouldNotDependOn_Application()
{
    var result = Types
        .InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("MyApp.Application")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"Domain depends on Application: {FormatFailures(result)}");
}
```

### Application Layer Allowed Dependencies

```csharp
[Fact]
public void Application_ShouldOnlyDependOn_AllowedLayers()
{
    var result = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .That()
        .ResideInNamespace("MyApp.Application")
        .ShouldNot()
        .HaveDependencyOnAny(
            "MyApp.Infrastructure",
            "MyApp.Web",
            "MyApp.Api")
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"Application layer violation: {FormatFailures(result)}");
}
```

### Infrastructure Implements Domain Interfaces

```csharp
[Fact]
public void Repositories_ShouldImplement_DomainInterfaces()
{
    var result = Types
        .InAssembly(typeof(InfrastructureAssemblyMarker).Assembly)
        .That()
        .HaveNameEndingWith("Repository")
        .Should()
        .ImplementInterface(typeof(IRepository<>))
        .GetResult();

    Assert.True(result.IsSuccessful,
        $"Repositories missing interface: {FormatFailures(result)}");
}
```

## Layered Architecture (N-Tier)

### Layer Dependency Direction

```csharp
[Fact]
public void DataLayer_ShouldNotDependOn_BusinessLayer()
{
    var result = Types
        .InAssembly(typeof(DataLayerMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("MyApp.Business")
        .GetResult();

    Assert.True(result.IsSuccessful);
}

[Fact]
public void BusinessLayer_ShouldNotDependOn_PresentationLayer()
{
    var result = Types
        .InAssembly(typeof(BusinessLayerMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("MyApp.Web")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

## CQRS Pattern

### Command Handlers Location

```csharp
[Fact]
public void CommandHandlers_ShouldResideIn_ApplicationLayer()
{
    var result = Types
        .InCurrentDomain()
        .That()
        .HaveNameEndingWith("CommandHandler")
        .Should()
        .ResideInNamespaceStartingWith("MyApp.Application")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### Commands Should Be Immutable

```csharp
[Fact]
public void Commands_ShouldBe_Sealed()
{
    var result = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .That()
        .ImplementInterface(typeof(ICommand))
        .Should()
        .BeSealed()
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### Queries Should Not Modify State

```csharp
[Fact]
public void QueryHandlers_ShouldNotDependOn_WriteRepositories()
{
    var result = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .That()
        .HaveNameEndingWith("QueryHandler")
        .ShouldNot()
        .HaveDependencyOnAny(
            "MyApp.Infrastructure.Persistence.Write",
            "MyApp.Application.Commands")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

## Domain-Driven Design

### Aggregates Encapsulation

```csharp
[Fact]
public void Entities_ShouldNotExpose_PublicSetters()
{
    // Use custom rule for property analysis
    var result = Types
        .InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .That()
        .Inherit(typeof(Entity))
        .And()
        .AreNotAbstract()
        .Should()
        .MeetCustomRule(new NoPublicSettersRule())
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### Value Objects Are Immutable

```csharp
[Fact]
public void ValueObjects_ShouldBe_Sealed()
{
    var result = Types
        .InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .That()
        .Inherit(typeof(ValueObject))
        .Should()
        .BeSealed()
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### Domain Events Location

```csharp
[Fact]
public void DomainEvents_ShouldResideIn_DomainLayer()
{
    var result = Types
        .InCurrentDomain()
        .That()
        .ImplementInterface(typeof(IDomainEvent))
        .Should()
        .ResideInNamespaceStartingWith("MyApp.Domain")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

## API / Web Layer

### Controllers Naming Convention

```csharp
[Fact]
public void Controllers_ShouldHave_CorrectSuffix()
{
    var result = Types
        .InAssembly(typeof(WebAssemblyMarker).Assembly)
        .That()
        .Inherit(typeof(ControllerBase))
        .Should()
        .HaveNameEndingWith("Controller")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### Controllers Should Be In Controllers Namespace

```csharp
[Fact]
public void Controllers_ShouldResideIn_ControllersNamespace()
{
    var result = Types
        .InAssembly(typeof(WebAssemblyMarker).Assembly)
        .That()
        .Inherit(typeof(ControllerBase))
        .Should()
        .ResideInNamespaceContaining("Controllers")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

## Service Registration

### Services Should Have Interfaces

```csharp
[Fact]
public void Services_ShouldImplement_Interface()
{
    var result = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .That()
        .HaveNameEndingWith("Service")
        .And()
        .AreClasses()
        .Should()
        .ImplementInterface(typeof(object)) // Any interface
        .GetResult();

    // Better: check for matching I{Name} interface pattern
    var services = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .That()
        .HaveNameEndingWith("Service")
        .And()
        .AreClasses()
        .GetTypes();

    foreach (var service in services)
    {
        var expectedInterface = $"I{service.Name}";
        Assert.True(
            service.GetInterfaces().Any(i => i.Name == expectedInterface),
            $"{service.Name} should implement {expectedInterface}");
    }
}
```

## No Circular Dependencies

### Namespace Slices Should Not Have Cycles

```csharp
[Fact]
public void Namespaces_ShouldNotHave_CircularDependencies()
{
    var slices = Types
        .InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .Slice()
        .ByNamespacePrefix("MyApp.Application.Features");

    var result = slices.Should().NotHaveDependenciesBetweenSlices();

    Assert.True(result.IsSuccessful);
}
```

## Utility Helpers

### Format Failure Message

```csharp
private static string FormatFailures(TestResult result)
{
    if (result.FailingTypeNames == null || !result.FailingTypeNames.Any())
        return "No details available";

    return string.Join(Environment.NewLine,
        result.FailingTypeNames.Select(t => $"  - {t}"));
}
```

### Assembly Marker Pattern

Create empty marker interfaces in each project for easy assembly reference:

```csharp
// In MyApp.Domain project
namespace MyApp.Domain;
public interface IDomainAssemblyMarker { }

// In tests
var domainAssembly = typeof(IDomainAssemblyMarker).Assembly;
```

## Test Organization

### Recommended Test Class Structure

```csharp
public class ArchitectureTests
{
    private readonly Assembly _domainAssembly;
    private readonly Assembly _applicationAssembly;
    private readonly Assembly _infrastructureAssembly;

    public ArchitectureTests()
    {
        _domainAssembly = typeof(DomainMarker).Assembly;
        _applicationAssembly = typeof(ApplicationMarker).Assembly;
        _infrastructureAssembly = typeof(InfrastructureMarker).Assembly;
    }

    [Fact]
    public void DomainLayer_HasNoDependencyOn_OtherLayers()
    {
        var result = Types
            .InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MyApp.Application",
                "MyApp.Infrastructure",
                "MyApp.Web")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer violations:{Environment.NewLine}{FormatFailures(result)}");
    }
}
```
