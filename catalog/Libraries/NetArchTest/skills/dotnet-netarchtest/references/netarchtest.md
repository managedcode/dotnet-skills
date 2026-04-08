# NetArchTest.Rules

## Open/Free Status

- open source
- free to use

## Install

```bash
dotnet add package NetArchTest.Rules
```

## Verify First

Before adding the package, check whether the repo already references it:

```bash
rg -n "NetArchTest\\.Rules" -g '*.csproj' .
```

## Common Usage

Use inside your test project:

```csharp
var result = Types.InAssembly(typeof(MyType).Assembly)
    .That().ResideInNamespace("MyApp.Presentation")
    .ShouldNot().HaveDependencyOn("MyApp.Data")
    .GetResult()
    .IsSuccessful;
```

## CI Fit

- runs as part of the normal test suite
- good for clean architecture and layering rules

## When Not To Use

- when the repo needs richer architecture modeling and custom assertions than NetArchTest provides comfortably

## Sources

- [NetArchTest](https://github.com/BenMorris/NetArchTest)
