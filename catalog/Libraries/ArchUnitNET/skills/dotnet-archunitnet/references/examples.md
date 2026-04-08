# ArchUnitNET Common Architecture Rules

## Project Setup

### Base Test Class

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

public abstract class ArchitectureTestBase
{
    protected static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Domain.Marker).Assembly,
                typeof(Application.Marker).Assembly,
                typeof(Infrastructure.Marker).Assembly,
                typeof(Web.Marker).Assembly)
            .Build();
}
```

## Clean Architecture Rules

### Domain Layer Independence

The domain layer should have no dependencies on outer layers:

```csharp
[Fact]
public void Domain_Should_Not_Reference_Application()
{
    Types()
        .That().ResideInNamespace("MyApp.Domain")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Application"))
        .Check(Architecture);
}

[Fact]
public void Domain_Should_Not_Reference_Infrastructure()
{
    Types()
        .That().ResideInNamespace("MyApp.Domain")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Infrastructure"))
        .Check(Architecture);
}

[Fact]
public void Domain_Should_Not_Reference_Presentation()
{
    Types()
        .That().ResideInNamespace("MyApp.Domain")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Web"))
        .Check(Architecture);
}
```

### Application Layer Rules

```csharp
[Fact]
public void Application_Should_Not_Reference_Infrastructure()
{
    Types()
        .That().ResideInNamespace("MyApp.Application")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Infrastructure"))
        .Check(Architecture);
}

[Fact]
public void Application_Should_Not_Reference_Presentation()
{
    Types()
        .That().ResideInNamespace("MyApp.Application")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Web"))
        .Check(Architecture);
}
```

### Infrastructure Layer Rules

```csharp
[Fact]
public void Infrastructure_Should_Not_Reference_Presentation()
{
    Types()
        .That().ResideInNamespace("MyApp.Infrastructure")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("MyApp.Web"))
        .Check(Architecture);
}
```

## Naming Convention Rules

### Controllers

```csharp
[Fact]
public void Controllers_Should_Have_Controller_Suffix()
{
    Types()
        .That().ResideInNamespace("Controllers", includeSubNamespaces: true)
        .And().AreClasses()
        .And().AreNotAbstract()
        .Should().HaveNameEndingWith("Controller")
        .Check(Architecture);
}

[Fact]
public void Controllers_Should_Inherit_ControllerBase()
{
    Types()
        .That().HaveNameEndingWith("Controller")
        .And().ResideInNamespace("Controllers")
        .Should().BeAssignableTo(typeof(ControllerBase))
        .Check(Architecture);
}
```

### Services

```csharp
[Fact]
public void Services_Should_Have_Service_Suffix()
{
    Types()
        .That().ResideInNamespace("Services")
        .And().AreClasses()
        .And().AreNotAbstract()
        .Should().HaveNameEndingWith("Service")
        .Check(Architecture);
}

[Fact]
public void Service_Interfaces_Should_Start_With_I()
{
    Types()
        .That().ResideInNamespace("Services")
        .And().AreInterfaces()
        .Should().HaveNameStartingWith("I")
        .Check(Architecture);
}
```

### Repositories

```csharp
[Fact]
public void Repositories_Should_Have_Repository_Suffix()
{
    Types()
        .That().ImplementInterface(typeof(IRepository<>))
        .And().AreClasses()
        .Should().HaveNameEndingWith("Repository")
        .Check(Architecture);
}
```

### Handlers (CQRS/MediatR)

```csharp
[Fact]
public void Handlers_Should_Have_Handler_Suffix()
{
    Types()
        .That().ImplementInterface(typeof(IRequestHandler<,>))
        .Should().HaveNameEndingWith("Handler")
        .Check(Architecture);
}

[Fact]
public void Commands_Should_Have_Command_Suffix()
{
    Types()
        .That().ImplementInterface(typeof(IRequest<>))
        .And().ResideInNamespaceContaining("Commands")
        .Should().HaveNameEndingWith("Command")
        .Check(Architecture);
}

[Fact]
public void Queries_Should_Have_Query_Suffix()
{
    Types()
        .That().ImplementInterface(typeof(IRequest<>))
        .And().ResideInNamespaceContaining("Queries")
        .Should().HaveNameEndingWith("Query")
        .Check(Architecture);
}
```

## Domain-Driven Design Rules

### Entities

```csharp
[Fact]
public void Entities_Should_Reside_In_Domain()
{
    Types()
        .That().AreAssignableTo(typeof(Entity))
        .Should().ResideInNamespaceContaining("Domain")
        .Check(Architecture);
}

[Fact]
public void Entities_Should_Not_Have_Public_Setters()
{
    // Use custom predicate or property-level checks
    Types()
        .That().AreAssignableTo(typeof(Entity))
        .Should().BeSealed()
        .OrShould().BeAbstract()
        .Check(Architecture);
}
```

### Value Objects

```csharp
[Fact]
public void ValueObjects_Should_Be_Sealed()
{
    Types()
        .That().AreAssignableTo(typeof(ValueObject))
        .And().AreNotAbstract()
        .Should().BeSealed()
        .Check(Architecture);
}
```

### Aggregate Roots

```csharp
[Fact]
public void Only_AggregateRoots_Should_Be_Referenced_By_Repositories()
{
    Types()
        .That().HaveNameEndingWith("Repository")
        .Should().DependOnAny(
            Types().That().ImplementInterface(typeof(IAggregateRoot)))
        .Check(Architecture);
}
```

## API Design Rules

### DTOs

```csharp
[Fact]
public void DTOs_Should_Not_Reference_Domain_Entities()
{
    Types()
        .That().HaveNameEndingWith("Dto")
        .Or().HaveNameEndingWith("Request")
        .Or().HaveNameEndingWith("Response")
        .Should().NotDependOnAny(
            Types().That().ResideInNamespace("Domain.Entities"))
        .Check(Architecture);
}
```

### No Circular Dependencies

```csharp
[Fact]
public void Application_Should_Be_Free_Of_Cycles()
{
    Types()
        .That().ResideInNamespace("MyApp.Application")
        .Should().BeFreeOfCycles()
        .Check(Architecture);
}
```

## Infrastructure Rules

### Database Contexts

```csharp
[Fact]
public void DbContexts_Should_Reside_In_Infrastructure()
{
    Types()
        .That().AreAssignableTo(typeof(DbContext))
        .Should().ResideInNamespaceContaining("Infrastructure")
        .Check(Architecture);
}
```

### External Service Clients

```csharp
[Fact]
public void HttpClients_Should_Reside_In_Infrastructure()
{
    Types()
        .That().HaveNameEndingWith("Client")
        .And().DependOnAny(Types().That().HaveName("HttpClient"))
        .Should().ResideInNamespaceContaining("Infrastructure")
        .Check(Architecture);
}
```

## Dependency Injection Rules

### Interface Implementations

```csharp
[Fact]
public void Every_Service_Should_Have_Interface()
{
    Types()
        .That().HaveNameEndingWith("Service")
        .And().AreClasses()
        .And().AreNotAbstract()
        .Should().ImplementInterface(
            Types().That().AreInterfaces()
                .And().HaveNameStartingWith("I"))
        .Check(Architecture);
}
```

## Testing Rules

### Test Classes Naming

```csharp
[Fact]
public void Test_Classes_Should_Have_Tests_Suffix()
{
    Types()
        .That().ResideInAssembly(typeof(SomeTest).Assembly)
        .And().AreClasses()
        .And().HaveAnyMethodWithAttribute(typeof(FactAttribute))
        .Should().HaveNameEndingWith("Tests")
        .Check(TestArchitecture);
}
```

## Complete Test Class Example

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace MyApp.Tests.Architecture;

public class CleanArchitectureTests
{
    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Domain.DomainAssemblyMarker).Assembly,
                typeof(Application.ApplicationAssemblyMarker).Assembly,
                typeof(Infrastructure.InfrastructureAssemblyMarker).Assembly,
                typeof(Web.WebAssemblyMarker).Assembly)
            .Build();

    // Layer definitions
    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("MyApp.Domain").As("Domain Layer");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespace("MyApp.Application").As("Application Layer");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInNamespace("MyApp.Infrastructure").As("Infrastructure Layer");

    private static readonly IObjectProvider<IType> PresentationLayer =
        Types().That().ResideInNamespace("MyApp.Web").As("Presentation Layer");

    [Fact]
    public void Domain_Should_Have_No_Dependencies_On_Other_Layers()
    {
        Types()
            .That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Application_Should_Only_Depend_On_Domain()
    {
        Types()
            .That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Controllers_Should_Follow_Naming_Convention()
    {
        Types()
            .That().Are(PresentationLayer)
            .And().AreAssignableTo(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .Should().HaveNameEndingWith("Controller")
            .Check(Architecture);
    }

    [Fact]
    public void Services_Should_Be_Internal()
    {
        Types()
            .That().HaveNameEndingWith("Service")
            .And().AreClasses()
            .And().DoNotHaveNameStartingWith("I")
            .Should().BeInternal()
            .Check(Architecture);
    }
}
```
