# .NET Code Complexity Metrics

## Overview

.NET provides built-in code metrics through the Roslyn analyzers. These metrics quantify maintainability, complexity, and coupling without requiring third-party tools.

## Primary Metrics

### Cyclomatic Complexity (CA1502)

Cyclomatic complexity measures the number of linearly independent paths through a method's source code.

**Calculation:**

- Start with 1
- Add 1 for each: `if`, `else if`, `case`, `while`, `for`, `foreach`, `&&`, `||`, `??`, `?.`, `catch`, conditional expression (`? :`)

**Thresholds:**

| Range   | Risk Level |
|---------|------------|
| 1-10    | Low        |
| 11-20   | Moderate   |
| 21-50   | High       |
| 51+     | Very High  |

**Recommended default:** 20 for methods, 6 for type average.

**Refactoring signals:**

- Extract method for distinct logic branches
- Replace conditional chains with polymorphism or strategy pattern
- Simplify boolean expressions

### Maintainability Index (CA1505)

The maintainability index is a composite metric ranging from 0 to 100 that indicates overall code maintainability.

**Formula:**

```
MI = MAX(0, (171 - 5.2 * ln(HV) - 0.23 * CC - 16.2 * ln(LOC)) * 100 / 171)
```

Where:
- HV = Halstead Volume (based on operators and operands)
- CC = Cyclomatic Complexity
- LOC = Lines of Code

**Thresholds:**

| Range   | Maintainability |
|---------|-----------------|
| 20-100  | High            |
| 10-19   | Moderate        |
| 0-9     | Low             |

**Recommended default:** 10 minimum.

**Refactoring signals:**

- Large methods need decomposition
- Complex expressions need simplification
- Consider extracting classes for low-scoring types

### Class Coupling (CA1506)

Class coupling counts the number of unique types that a type or method references, excluding primitives and common framework types.

**What counts as coupling:**

- Base types
- Interface implementations
- Parameter types
- Local variable types
- Method return types
- Generic type arguments
- Exception types

**What is excluded:**

- Primitive types (`int`, `string`, `bool`, etc.)
- `System.Object`
- `System.Void`

**Thresholds:**

| Scope  | Recommended Maximum |
|--------|---------------------|
| Method | 30                  |
| Type   | 80                  |

**Refactoring signals:**

- Introduce abstractions to reduce direct dependencies
- Apply dependency injection
- Split large classes by responsibility

### Inheritance Depth (CA1501)

Inheritance depth counts the number of types in the inheritance chain, starting from `System.Object`.

**Thresholds:**

| Depth | Risk Level |
|-------|------------|
| 1-4   | Low        |
| 5-6   | Moderate   |
| 7+    | High       |

**Recommended default:** 6 maximum.

**Refactoring signals:**

- Prefer composition over deep inheritance
- Flatten hierarchies where intermediate classes add little value

## Metric Interactions

These metrics often correlate:

- High cyclomatic complexity typically lowers the maintainability index
- High coupling often accompanies high complexity
- Deep inheritance can mask complexity in base classes

When multiple metrics flag the same code:

1. Address cyclomatic complexity first (most actionable)
2. Coupling improvements often follow complexity reduction
3. Maintainability index improves as other metrics improve

## Lines of Code

Lines of code (LOC) is not enforced by a dedicated analyzer rule, but it factors into the maintainability index calculation.

**Guidelines:**

- Methods: prefer under 50 logical lines
- Types: prefer under 500 logical lines
- Files: prefer under 1000 logical lines

## Sources

- [Code metrics values](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-values)
- [CA1502: Avoid excessive complexity](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1502)
- [CA1505: Avoid unmaintainable code](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1505)
- [CA1506: Avoid excessive class coupling](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1506)
- [CA1501: Avoid excessive inheritance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1501)
