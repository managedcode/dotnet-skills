---
name: dotnet-code-review
version: "1.0.0"
category: "Core"
description: "Review .NET changes for bugs, regressions, architectural drift, missing tests, incorrect async or disposal behavior, and platform-specific pitfalls before you approve or merge them."
compatibility: "Works for application code, libraries, tests, tooling, and infrastructure changes."
---

# .NET Code Review

## Trigger On

- reviewing a pull request or patch in a .NET repository
- checking for behavioral regressions, API misuse, or missing tests
- auditing architectural or framework-specific correctness

## References

- [checklist.md](references/checklist.md) - comprehensive code review checklist organized by risk priority
- [patterns.md](references/patterns.md) - common patterns and anti-patterns for async, disposal, and security

## Workflow

1. Prioritize correctness, data loss, concurrency, security, lifecycle, and platform-compatibility issues before style concerns. Use the [checklist](references/checklist.md) P0-P2 categories first.
2. Check async flows, cancellation propagation, exception handling, disposal, and transient versus singleton lifetime mistakes. Refer to [patterns.md](references/patterns.md) for common pitfalls.
3. Verify tests cover the changed behavior, not only the happy path or refactored implementation details.
4. Inspect framework-specific boundaries such as EF query translation, ASP.NET middleware order, Blazor render state, or MAUI UI-thread access.
5. Call out missing observability, migration risk, or runtime configuration drift when those are part of the change.
6. Keep findings concrete, reproducible, and tied to specific files or behavior.

## Key Review Patterns

### Async Code
- Async must propagate through the entire call chain; never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in async contexts
- Always propagate `CancellationToken` parameters
- Use `ConfigureAwait(false)` in library code
- Never use `async void` except for event handlers

### Resource Disposal
- Use `using` declarations or statements for all `IDisposable` resources
- Use `await using` for `IAsyncDisposable` resources
- Use `IHttpClientFactory` instead of creating `HttpClient` directly
- Unsubscribe event handlers to prevent memory leaks
- Validate DI service lifetimes to prevent captured dependencies

### Security
- Use parameterized queries or EF to prevent SQL injection
- Validate all user input at system boundaries
- Prevent path traversal by validating resolved paths stay within allowed directories
- Never hardcode secrets; use configuration and secret management
- Enforce authorization checks before accessing protected resources

## Deliver

- ranked review findings with file references
- clear residual risks and test gaps
- brief summary of what changed only after findings

## Validate

- findings describe user-visible or maintainability-impacting risk
- assumptions are stated when repo context is incomplete
- no trivial style nit hides a more serious issue
