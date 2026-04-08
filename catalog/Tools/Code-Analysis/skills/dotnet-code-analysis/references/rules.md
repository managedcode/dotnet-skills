# SDK Analyzer Rule Categories

This reference covers the built-in .NET SDK analyzer rule categories, their prefixes, and practical guidance for each.

## Rule Prefix Summary

| Prefix | Category | Scope |
|--------|----------|-------|
| CA | Code Analysis | Design, reliability, security, performance, usage |
| IDE | IDE Analyzers | Code style, simplification, formatting |
| CS | C# Compiler Warnings | Language-level warnings promoted by analyzers |

## CA Rules: Code Analysis

CA rules are the primary SDK analyzer rules. They cover correctness, performance, and security.

### CA1xxx: Design Rules

- Ensure proper API design and naming.
- Examples: CA1000 (do not declare static members on generic types), CA1010 (collections should implement generic interface).
- High value for public APIs.

### CA2xxx: Usage and Reliability

- Detect misuse of framework types and patterns.
- Examples: CA2000 (dispose objects before losing scope), CA2007 (consider calling ConfigureAwait).
- Enable these early; they catch real bugs.

### CA3xxx: Security

- Identify security vulnerabilities.
- Examples: CA3001 (SQL injection), CA3075 (insecure DTD processing).
- Critical for any user-facing or network-connected code.

### CA5xxx: Security (Extended)

- Extended security rules for cryptography, serialization, and input handling.
- Examples: CA5350 (do not use weak cryptographic algorithms), CA5394 (do not use insecure randomness).
- Enable for production systems handling sensitive data.

### CA18xx, CA19xx: Performance

- Detect performance anti-patterns.
- Examples: CA1802 (use literals where appropriate), CA1860 (avoid Length/Count zero checks).
- High value for hot paths and resource-constrained environments.

## IDE Rules: Code Style

IDE rules enforce consistency and simplify code.

### IDE0001-IDE0099: Code Simplification

- Remove unnecessary code, simplify expressions.
- Examples: IDE0001 (simplify name), IDE0017 (use object initializers).
- Useful for codebase consistency.

### IDE0100-IDE0199: Language Feature Preferences

- Prefer modern C# features.
- Examples: IDE0160 (file-scoped namespace preference), IDE0161 (convert to block-scoped namespace).
- Align with team style guide.

### IDE0200-IDE0299: Expression Preferences

- Expression-bodied members, pattern matching, null checks.
- Examples: IDE0200 (unnecessary lambda expression), IDE0270 (use coalesce expression).
- Enforce via .editorconfig for consistency.

### IDE1xxx: Formatting

- Whitespace, indentation, newlines.
- Examples: IDE1005 (delegate invocation should be conditional).
- Typically set to suggestion or silent; enforce in CI only when the team agrees.

## Severity Levels

| Severity | Build Behavior | Use Case |
|----------|----------------|----------|
| error | Fails build | Critical correctness or security |
| warning | Logged, may fail with TreatWarningsAsErrors | Important but deferrable |
| suggestion | IDE hint only | Style preferences |
| silent | Rule runs but no diagnostic | Rule disabled for output but still analyzed |
| none | Rule disabled | Not applicable to this codebase |

## Suppression Strategies

### Inline Suppression

```csharp
#pragma warning disable CA2000
var resource = new Resource();
#pragma warning restore CA2000
```

Use sparingly. Always include a justification comment.

### GlobalSuppressions.cs

```csharp
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Ownership transferred to caller", Scope = "member",
    Target = "~M:MyClass.CreateResource")]
```

Prefer for project-wide suppression of known false positives.

### .editorconfig

```editorconfig
# Disable specific rule for all files
dotnet_diagnostic.CA1062.severity = none

# Disable for test files only
[**/Tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
```

Prefer for category-level or scope-based configuration.

## Rule Selection Guidance

1. Start with `AnalysisMode = Recommended` to get the common high-value rules.
2. Promote security rules (CA3xxx, CA5xxx) to error for production code.
3. Treat CA2xxx reliability rules as warnings initially; promote to error as the codebase matures.
4. Enforce IDE rules only when the team has agreed on style conventions.
5. Do not enable all rules at maximum severity immediately; ramp up over time.

## Discovering Available Rules

```bash
# List all analyzer diagnostics from a build
dotnet build /p:ReportAnalyzer=true /v:d | grep "CA\|IDE"

# Get rule documentation
# https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/
# https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/
```

## References

- [Code quality rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [Code style rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/)
- [Configure analyzers](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
