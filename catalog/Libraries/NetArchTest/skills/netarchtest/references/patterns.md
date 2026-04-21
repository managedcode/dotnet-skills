# NetArchTest Rule Patterns

This reference covers the core fluent API patterns for `NetArchTest.Rules`.

## Basic Pattern Structure

All NetArchTest rules follow a three-part fluent pattern:

```csharp
var result = Types
    .InAssembly(assembly)       // 1. Select types
    .That()                     // 2. Apply predicates
    .ShouldNot().HaveDependencyOn("SomeNamespace")  // 3. Assert condition
    .GetResult();

Assert.True(result.IsSuccessful);
```

## Type Selection Patterns

### From Assembly

```csharp
// Current assembly
Types.InCurrentDomain()

// Specific assembly
Types.InAssembly(typeof(MyClass).Assembly)

// Multiple assemblies
Types.InAssemblies(assemblies)
```

### From Namespace

```csharp
Types.InAssembly(assembly)
    .That()
    .ResideInNamespace("MyApp.Domain")
```

### Namespace Matching Variants

```csharp
// Exact namespace
.ResideInNamespace("MyApp.Domain")

// Namespace prefix (includes sub-namespaces)
.ResideInNamespaceStartingWith("MyApp.Domain")

// Namespace suffix
.ResideInNamespaceEndingWith(".Handlers")

// Namespace contains
.ResideInNamespaceContaining("Services")
```

## Type Predicates

### By Naming Convention

```csharp
.HaveNameStartingWith("I")      // Interfaces
.HaveNameEndingWith("Service")  // Services
.HaveNameMatching(".*Handler$") // Regex match
```

### By Inheritance

```csharp
.Inherit(typeof(BaseClass))
.ImplementInterface(typeof(IService))
.BeAssignableTo(typeof(ICommand))
```

### By Attributes

```csharp
.HaveCustomAttribute(typeof(SerializableAttribute))
.HaveCustomAttributeOrInherit(typeof(MyAttribute))
```

### By Type Kind

```csharp
.BeClasses()
.BeInterfaces()
.BeSealed()
.BeAbstract()
.BePublic()
.BeInternal()
.BeStatic()
.BeGeneric()
```

### By Dependencies

```csharp
.HaveDependencyOn("System.Data")
.HaveDependencyOnAny("Lib1", "Lib2")
.HaveDependencyOnAll("Required1", "Required2")
.OnlyHaveDependenciesOn("Allowed1", "Allowed2")
```

## Assertion Patterns

### Positive Assertions

```csharp
.Should().BePublic()
.Should().BeSealed()
.Should().HaveDependencyOn("Required.Namespace")
.Should().ImplementInterface(typeof(IRequired))
```

### Negative Assertions

```csharp
.ShouldNot().BePublic()
.ShouldNot().HaveDependencyOn("Forbidden.Namespace")
.ShouldNot().HaveDependencyOnAny("Bad1", "Bad2")
```

## Combining Predicates

### And (implicit)

Chained predicates are ANDed:

```csharp
.That()
.ResideInNamespace("MyApp.Services")
.And()
.HaveNameEndingWith("Service")
```

### Or

```csharp
.That()
.ResideInNamespace("MyApp.Services")
.Or()
.ResideInNamespace("MyApp.Handlers")
```

## Result Handling

### Basic Result Check

```csharp
var result = Types.InAssembly(assembly)
    .That()
    .ResideInNamespace("MyApp.Domain")
    .ShouldNot().HaveDependencyOn("MyApp.Infrastructure")
    .GetResult();

Assert.True(result.IsSuccessful,
    $"Domain layer violation: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
```

### Detailed Failure Inspection

```csharp
if (!result.IsSuccessful)
{
    foreach (var failingType in result.FailingTypes)
    {
        Console.WriteLine($"Violation: {failingType.FullName}");
    }
}
```

## Custom Predicates

### Using MeetCustomRule

```csharp
.MeetCustomRule(new MyCustomRule())
```

### Implementing ICustomRule

```csharp
public class NoPublicFieldsRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
    {
        return !type.Fields.Any(f => f.IsPublic && !f.IsStatic);
    }
}
```

## Slices Pattern (Dependency Cycles)

```csharp
var slices = Types.InAssembly(assembly)
    .Slice()
    .ByNamespacePrefix("MyApp");

slices.Should().NotHaveDependenciesBetweenSlices();
```

## Excluding Types

```csharp
Types.InAssembly(assembly)
    .That()
    .ResideInNamespace("MyApp.Domain")
    .And()
    .DoNotHaveNameMatching(".*Tests$")  // Exclude test types
    .And()
    .AreNotNested()                      // Exclude nested types
```

## Assembly-Level Rules

```csharp
// Check assembly references
var assembly = typeof(MyClass).Assembly;
var references = assembly.GetReferencedAssemblies();

// Ensure no reference to forbidden assembly
Assert.DoesNotContain(references, r => r.Name == "ForbiddenAssembly");
```
