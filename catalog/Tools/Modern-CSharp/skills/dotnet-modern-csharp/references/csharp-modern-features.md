# Modern C# Features For .NET Repositories

Use this reference when modernizing C# code without breaking the repo's target framework, SDK, or language-version expectations.

## First Rule: Detect The Real Language Ceiling

- the default C# version follows the target framework in modern SDK-style projects
- `.NET 9` maps to `C# 13`
- `.NET 10` maps to `C# 14`
- newer language versions than the target framework supports are not supported
- do not set `LangVersion=latest` because it makes builds machine-dependent
- use `LangVersion=preview` only when the repo intentionally opts into preview

Useful checks:

```bash
rg -n "TargetFramework|TargetFrameworks|LangVersion" -g '*.csproj' -g 'Directory.Build.*' .
```

If the repo's current language version is unclear, the compiler can reveal it:

```csharp
#error version
```

As of March 8, 2026:

- `C# 14` is the latest stable version
- `C# 15` exists in preview, but should not be a default choice for production repo guidance

## Practical Adoption Rules

- prefer stable features that remove ceremony, improve correctness, or improve performance without obscuring intent
- do not rewrite entire codebases just to use new syntax
- modernize opportunistically when touching the code anyway
- coordinate feature adoption with `.editorconfig`, analyzers, and architecture rules

## Version Guide

### C# 8

Typical pairing: `.NET Core 3.x`

Key features:

- readonly members
- default interface members
- switch expressions
- property patterns
- tuple patterns
- positional patterns
- using declarations
- static local functions
- disposable `ref struct`
- nullable reference types
- asynchronous streams
- indices and ranges
- null-coalescing assignment
- unmanaged constructed types
- stackalloc in nested expressions
- improved interpolated verbatim strings

### C# 9

Typical pairing: `.NET 5`

Key features:

- records
- init-only setters
- top-level statements
- relational patterns
- logical patterns
- native-sized integers
- function pointers
- module initializers
- target-typed `new`
- static anonymous functions
- target-typed conditional expressions
- covariant return types
- extension `GetEnumerator` support in `foreach`
- lambda discard parameters
- attributes on local functions

### C# 10

Typical pairing: `.NET 6`

Key features:

- record structs
- struct initialization improvements
- interpolated string handlers
- `global using`
- file-scoped namespaces
- extended property patterns
- lambda natural type
- explicit lambda return types
- attributes on lambda expressions
- `const` interpolated strings
- `sealed` `ToString` in records
- more accurate definite assignment and null-state analysis
- mixed assignment and declaration in deconstruction
- `AsyncMethodBuilder` on methods
- `CallerArgumentExpression`
- new `#line` format

### C# 11

Typical pairing: `.NET 7`

Key features:

- raw string literals
- generic math support
- generic attributes
- UTF-8 string literals
- newlines inside interpolation expressions
- list patterns
- file-local types
- required members
- auto-default structs
- matching `Span<char>` against a constant string
- extended `nameof` scope
- `nint` and `nuint` aliases
- `ref` fields and `scoped ref`
- improved method-group conversion to delegate
- warning wave 7

### C# 12

Typical pairing: `.NET 8`

Key features:

- primary constructors
- collection expressions
- inline arrays
- optional parameters in lambda expressions
- `ref readonly` parameters
- alias any type
- `Experimental` attribute
- interceptors as a preview feature

### C# 13

Released November 2024. Stable on `.NET 9`.

Key features:

- `params` collections
- new `lock` type and semantics with `System.Threading.Lock`
- `\e` escape sequence
- method-group natural type improvements
- implicit indexer access in object initializers
- `ref` locals and `unsafe` contexts in iterators and async methods
- `ref struct` can implement interfaces
- `allows ref struct` anti-constraint for generics
- partial properties and partial indexers
- overload resolution priority
- `field` contextual keyword as a preview feature in C# 13-era tooling

Practical use:

- use `System.Threading.Lock` when the repo is on `.NET 9` and wants the newer synchronization model
- use `params ReadOnlySpan<T>` or related collection forms in performance-sensitive APIs
- use partial properties or indexers only where partial-type generation patterns already exist
- do not assume `field` is safe unless the repo explicitly opted into preview behavior

### C# 14

Released November 2025. Stable on `.NET 10`.

Key features:

- extension members
- null-conditional assignment
- `nameof` with unbound generic types such as `nameof(List<>)`
- more implicit conversions for `Span<T>` and `ReadOnlySpan<T>`
- modifiers on simple lambda parameters
- field-backed properties via `field`
- partial events and partial constructors
- user-defined compound assignment operators
- file-based app preprocessor directives

Practical use:

- use null-conditional assignment to remove boilerplate null guards when side effects are clear
- use field-backed properties when you need simple validation in an otherwise auto-property style
- use extension members when the repo wants richer extension APIs, not for cosmetic rewrites
- use span-related improvements in performance-sensitive code paths, not as blanket style churn

## Sources

- [Configure C# language version](https://learn.microsoft.com/dotnet/csharp/language-reference/configure-language-version)
- [The history of C#](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-version-history)
- [What's new in C# 13](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-13)
- [What's new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
