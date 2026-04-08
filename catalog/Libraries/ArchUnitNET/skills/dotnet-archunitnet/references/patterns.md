# ArchUnitNET Rule Patterns

## Architecture Loading

Load the architecture once per test class to avoid repeated assembly scanning:

```csharp
private static readonly Architecture Architecture =
    new ArchLoader()
        .LoadAssemblies(typeof(SomeClassInTargetAssembly).Assembly)
        .Build();
```

For multiple assemblies:

```csharp
private static readonly Architecture Architecture =
    new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Entity).Assembly,
            typeof(Application.Service).Assembly,
            typeof(Infrastructure.Repository).Assembly)
        .Build();
```

## Rule Syntax Patterns

### Basic Pattern

```csharp
IArchRule rule = Types()
    .That()
    .Are(predicate)
    .Should()
    .Be(condition);

rule.Check(Architecture);
```

### With Test Framework Integration

Using xUnit:

```csharp
[Fact]
public void Controllers_Should_Not_Depend_On_Repositories()
{
    Types()
        .That().ResideInNamespace("Controllers")
        .Should().NotDependOnAny(Types().That().ResideInNamespace("Repositories"))
        .Check(Architecture);
}
```

Using MSTest:

```csharp
[TestMethod]
public void Controllers_Should_Not_Depend_On_Repositories()
{
    Types()
        .That().ResideInNamespace("Controllers")
        .Should().NotDependOnAny(Types().That().ResideInNamespace("Repositories"))
        .Check(Architecture);
}
```

## Selection Patterns

### By Namespace

```csharp
Types().That().ResideInNamespace("MyApp.Domain")
Types().That().ResideInNamespaceContaining("Services")
Types().That().ResideInNamespaceMatching(@"MyApp\..*\.Handlers")
```

### By Naming Convention

```csharp
Types().That().HaveNameEndingWith("Controller")
Types().That().HaveNameStartingWith("I")
Types().That().HaveNameMatching(@".*Handler$")
```

### By Type Characteristics

```csharp
Types().That().AreClasses()
Types().That().AreInterfaces()
Types().That().AreAbstract()
Types().That().AreSealed()
Types().That().ArePublic()
Types().That().ImplementInterface(typeof(IService))
```

### By Inheritance

```csharp
Types().That().AreAssignableTo(typeof(BaseClass))
Types().That().ImplementInterface(typeof(IHandler<>))
```

### By Attributes

```csharp
Types().That().HaveAnyAttributes(typeof(SerializableAttribute))
Types().That().DoNotHaveAnyAttributes(typeof(ObsoleteAttribute))
```

## Condition Patterns

### Dependency Rules

```csharp
.Should().NotDependOnAny(Types().That().ResideInNamespace("Infrastructure"))
.Should().OnlyDependOn(Types().That().ResideInNamespace("Domain"))
.Should().DependOnAny(Types().That().ResideInNamespace("Contracts"))
```

### Namespace Residence

```csharp
.Should().ResideInNamespace("MyApp.Application")
.Should().ResideInNamespaceContaining("Handlers")
```

### Naming Requirements

```csharp
.Should().HaveNameEndingWith("Service")
.Should().HaveNameStartingWith("I")
```

### Visibility Requirements

```csharp
.Should().BePublic()
.Should().BeInternal()
.Should().BeSealed()
```

### Inheritance Requirements

```csharp
.Should().ImplementInterface(typeof(IDisposable))
.Should().BeAssignableTo(typeof(BaseEntity))
```

## Layer Definition Pattern

Define layers as reusable predicates:

```csharp
private static readonly IObjectProvider<IType> DomainLayer =
    Types().That().ResideInNamespace("MyApp.Domain").As("Domain Layer");

private static readonly IObjectProvider<IType> ApplicationLayer =
    Types().That().ResideInNamespace("MyApp.Application").As("Application Layer");

private static readonly IObjectProvider<IType> InfrastructureLayer =
    Types().That().ResideInNamespace("MyApp.Infrastructure").As("Infrastructure Layer");

private static readonly IObjectProvider<IType> PresentationLayer =
    Types().That().ResideInNamespace("MyApp.Web").As("Presentation Layer");
```

Use layers in rules:

```csharp
[Fact]
public void Domain_Should_Not_Depend_On_Application()
{
    Types().That().Are(DomainLayer)
        .Should().NotDependOnAny(ApplicationLayer)
        .Check(Architecture);
}
```

## Combining Rules

Combine multiple conditions:

```csharp
Types()
    .That().ResideInNamespace("MyApp.Domain")
    .And().AreClasses()
    .And().AreNotAbstract()
    .Should().BeSealed()
    .OrShould().ImplementInterface(typeof(IEntity))
    .Check(Architecture);
```

## Custom Predicates

Create reusable custom predicates:

```csharp
public static class CustomPredicates
{
    public static IPredicate<IType> AreAggregateRoots()
    {
        return new SimplePredicate<IType>(
            type => type.ImplementsInterface(typeof(IAggregateRoot)),
            "are aggregate roots");
    }
}
```

Use in rules:

```csharp
Types().That().Are(CustomPredicates.AreAggregateRoots())
    .Should().ResideInNamespace("MyApp.Domain.Aggregates")
    .Check(Architecture);
```

## Slice Pattern for Vertical Slices

Check that vertical slices do not depend on each other:

```csharp
SliceRuleDefinition.ForFunctions()
    .Should().NotDependOnEachOther()
    .Check(Architecture);
```

## Cycle Detection Pattern

Check for circular dependencies:

```csharp
Types().That().ResideInNamespace("MyApp")
    .Should().BeFreeOfCycles()
    .Check(Architecture);
```
