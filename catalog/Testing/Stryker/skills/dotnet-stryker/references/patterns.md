# Mutation Testing Patterns and Thresholds

## Threshold Guidelines

### Threshold Levels

| Threshold | Purpose                                              |
|-----------|------------------------------------------------------|
| high      | Score above this is green/excellent                  |
| low       | Score below this is yellow/warning                   |
| break     | Score below this fails the build                     |

### Recommended Thresholds by Context

#### New Critical Code

```json
{
  "thresholds": {
    "high": 90,
    "low": 80,
    "break": 70
  }
}
```

#### Established Production Code

```json
{
  "thresholds": {
    "high": 80,
    "low": 60,
    "break": 50
  }
}
```

#### Legacy Code Under Improvement

```json
{
  "thresholds": {
    "high": 70,
    "low": 50,
    "break": 30
  }
}
```

#### High-Risk Domain Logic

```json
{
  "thresholds": {
    "high": 95,
    "low": 90,
    "break": 85
  }
}
```

## Mutation Score Interpretation

### What Mutation Score Means

- **100%**: All mutants killed - every code mutation was detected by tests
- **80-99%**: Strong test suite - most mutations detected
- **60-79%**: Adequate coverage - significant gaps may exist
- **40-59%**: Weak coverage - many logic errors could go undetected
- **Below 40%**: Poor coverage - tests provide minimal fault detection

### Survived Mutants

A survived mutant means:

1. A code mutation was made
2. All tests still passed
3. The test suite did not detect the change

Common causes:

- Missing assertions
- Weak assertions (checking only part of behavior)
- Untested edge cases
- Dead code
- Equivalent mutants (mutation produces same behavior)

## Scoping Patterns

### Focus on Critical Paths

Target high-value code first:

```json
{
  "mutate": [
    "**/Domain/**/*.cs",
    "**/Core/**/*.cs",
    "!**/*Dto.cs",
    "!**/*Config.cs"
  ]
}
```

### Exclude Generated and Trivial Code

```json
{
  "mutate": [
    "**/*.cs",
    "!**/Migrations/**",
    "!**/Generated/**",
    "!**/obj/**",
    "!**/*.Designer.cs",
    "!**/GlobalUsings.cs"
  ]
}
```

### Ignore Low-Value Mutations

```json
{
  "ignore-mutations": [
    "Linq",
    "StringMethod"
  ]
}
```

## Mutation Categories

### Arithmetic Mutations

Original: `a + b`
Mutated: `a - b`, `a * b`, `a / b`

### Comparison Mutations

Original: `a > b`
Mutated: `a >= b`, `a < b`, `a == b`

### Boolean Mutations

Original: `true`
Mutated: `false`

Original: `a && b`
Mutated: `a || b`

### Equality Mutations

Original: `a == b`
Mutated: `a != b`

### Negation Mutations

Original: `!condition`
Mutated: `condition`

### Block Statement Mutations

Original: `if (x) { DoSomething(); }`
Mutated: `if (x) { }`

### Return Value Mutations

Original: `return value;`
Mutated: `return default;`

## Patterns for Surviving Mutants

### Pattern: Missing Boundary Tests

Mutation that survives:

```csharp
// Original: if (x >= 10)
// Mutated:  if (x > 10)
```

Fix: Add boundary test for `x = 10`.

### Pattern: Missing Null Checks

Mutation that survives:

```csharp
// Original: return item ?? default;
// Mutated:  return item;
```

Fix: Add test for null input.

### Pattern: Unchecked Error Paths

Mutation that survives:

```csharp
// Original: throw new ArgumentException();
// Mutated:  (removed)
```

Fix: Add test that expects the exception.

### Pattern: Side Effect Not Verified

Mutation that survives:

```csharp
// Original: _logger.Log(message);
// Mutated:  (removed)
```

Fix: Verify logger was called in test.

## Incremental Mutation Testing

### Run Only on Changed Code

```bash
dotnet stryker --since --since-target main
```

### Configuration for CI

```json
{
  "stryker-config": {
    "since": {
      "enabled": true,
      "target": "main"
    }
  }
}
```

### When to Use Full Mutation Runs

- Before major releases
- After significant refactoring
- When establishing baseline scores
- On scheduled nightly or weekly builds

## Performance Optimization

### Use Coverage Analysis

```json
{
  "coverage-analysis": "perTest"
}
```

This tracks which tests cover which code, running only relevant tests per mutant.

### Limit Concurrency Based on Resources

```json
{
  "concurrency": 4
}
```

Higher values use more memory and CPU.

### Use Baseline for Large Codebases

```json
{
  "baseline": {
    "enabled": true,
    "provider": "disk",
    "path": ".stryker-baseline"
  }
}
```

This caches results and only re-runs mutations for changed code.

## Anti-Patterns

### Do Not Chase 100% Everywhere

Diminishing returns on low-value code. Focus effort on:

- Domain logic
- Business rules
- Critical calculations
- Security-sensitive code

### Do Not Ignore Survived Mutants Blindly

Each survived mutant is a potential bug that tests would miss. Investigate before ignoring.

### Do Not Run Full Mutation Testing on Every PR

Too slow for large codebases. Use incremental mode or schedule full runs separately.

### Do Not Set Break Threshold Too High Initially

Start conservative and increase as test quality improves. A failing mutation gate on every PR creates frustration without value.

## Workflow Integration

### Recommended PR Workflow

1. Run incremental mutation testing on changed files
2. Report results without failing the build
3. Track trends over time

### Recommended Release Workflow

1. Run full mutation testing before release branches
2. Use break threshold to enforce quality gate
3. Generate reports for team review

### Recommended Nightly Workflow

1. Run full mutation testing on main branch
2. Generate trend reports
3. Open issues for significant score drops
