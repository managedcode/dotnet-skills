# .NET Code Review Checklist

Use this checklist to systematically evaluate .NET code changes. Items are ordered by risk severity.

---

## 1. Correctness and Data Integrity

- [ ] **Logic correctness** - Does the code produce the intended result for all inputs?
- [ ] **Edge cases** - Are null, empty, boundary, and exceptional inputs handled?
- [ ] **Data mutations** - Are collections modified safely during iteration?
- [ ] **Race conditions** - Can concurrent access corrupt shared state?
- [ ] **Transactions** - Are multi-step data changes wrapped in appropriate transactions?
- [ ] **Idempotency** - Are retryable operations safe to execute multiple times?

## 2. Concurrency and Async

- [ ] **Async all the way** - No `Task.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in async contexts?
- [ ] **ConfigureAwait** - Is `ConfigureAwait(false)` used appropriately in library code?
- [ ] **Cancellation** - Are `CancellationToken` parameters passed through the call chain?
- [ ] **Deadlocks** - Could synchronous waits on UI or ASP.NET synchronization contexts deadlock?
- [ ] **Thread safety** - Are shared resources protected with locks, `Interlocked`, or concurrent collections?
- [ ] **Parallel pitfalls** - Do `Parallel.ForEach` or `Task.WhenAll` calls handle exceptions correctly?

## 3. Resource Lifecycle and Disposal

- [ ] **IDisposable** - Are disposable objects disposed via `using` or explicit disposal?
- [ ] **IAsyncDisposable** - Are async-disposable resources handled with `await using`?
- [ ] **Scope lifetime** - Do DI-registered services have appropriate lifetimes (transient, scoped, singleton)?
- [ ] **HttpClient reuse** - Is `HttpClient` reused via `IHttpClientFactory` to avoid socket exhaustion?
- [ ] **Event handlers** - Are event subscriptions unsubscribed to prevent memory leaks?
- [ ] **Finalizers** - If implementing a finalizer, is the dispose pattern followed correctly?

## 4. Security

- [ ] **Input validation** - Are user inputs validated and sanitized?
- [ ] **SQL injection** - Are parameterized queries or EF used instead of string concatenation?
- [ ] **XSS** - Are outputs HTML-encoded in web contexts?
- [ ] **Secrets** - Are credentials, keys, and tokens stored securely (not in code or config)?
- [ ] **Authorization** - Are authorization checks performed before sensitive operations?
- [ ] **CSRF** - Are anti-forgery tokens used for state-changing operations?
- [ ] **Path traversal** - Are file paths validated to prevent directory traversal attacks?

## 5. Exception Handling

- [ ] **Specific exceptions** - Are specific exception types caught instead of bare `catch (Exception)`?
- [ ] **Exception swallowing** - Are exceptions logged or rethrown, not silently swallowed?
- [ ] **Exception wrapping** - Is the original exception preserved via inner exception when rethrowing?
- [ ] **Validation vs exception** - Are expected failures handled via validation, not exceptions?
- [ ] **Finally blocks** - Do critical cleanup operations use `finally` or `using`?

## 6. API Design and Breaking Changes

- [ ] **Binary compatibility** - Do changes preserve binary compatibility for library consumers?
- [ ] **Behavioral compatibility** - Do changes maintain expected behavior for existing callers?
- [ ] **Nullability annotations** - Are nullability annotations accurate and consistent?
- [ ] **Default parameters** - Do new default parameter values make sense for existing callers?
- [ ] **Obsolete members** - Are deprecated members marked with `[Obsolete]` and guidance provided?

## 7. Performance

- [ ] **Allocations** - Are unnecessary allocations avoided in hot paths?
- [ ] **String operations** - Is `StringBuilder` used for multiple concatenations?
- [ ] **LINQ in loops** - Are LINQ queries evaluated once, not per iteration?
- [ ] **Lazy evaluation** - Is deferred execution understood and intentional?
- [ ] **Boxing** - Is boxing avoided for value types in performance-critical code?
- [ ] **Span/Memory** - Are `Span<T>` or `Memory<T>` used for buffer operations?

## 8. Entity Framework and Data Access

- [ ] **N+1 queries** - Are related entities eagerly loaded when needed?
- [ ] **Tracking** - Is `AsNoTracking()` used for read-only queries?
- [ ] **Client evaluation** - Are LINQ expressions translatable to SQL?
- [ ] **Migrations** - Do migrations handle existing data correctly?
- [ ] **Connection management** - Are database connections scoped appropriately?
- [ ] **Bulk operations** - Are bulk inserts/updates used for large data sets?

## 9. ASP.NET Core Specifics

- [ ] **Middleware order** - Is middleware registered in the correct order?
- [ ] **Model binding** - Are model binding errors handled appropriately?
- [ ] **Action filters** - Are cross-cutting concerns handled via filters, not repeated code?
- [ ] **Response caching** - Are cache headers set correctly for cacheable responses?
- [ ] **Request size limits** - Are large request body limits configured appropriately?

## 10. Testing

- [ ] **Test coverage** - Are new code paths covered by tests?
- [ ] **Edge case tests** - Do tests cover boundary conditions and error cases?
- [ ] **Test isolation** - Are tests independent and not reliant on execution order?
- [ ] **Mock boundaries** - Are external dependencies mocked at appropriate boundaries?
- [ ] **Assertion clarity** - Do test assertions clearly indicate what is being verified?

## 11. Observability

- [ ] **Logging** - Are significant operations and errors logged appropriately?
- [ ] **Log levels** - Are log levels (Debug, Info, Warning, Error) used correctly?
- [ ] **Structured logging** - Are log messages structured with named parameters?
- [ ] **Metrics** - Are key business and performance metrics captured?
- [ ] **Tracing** - Is distributed tracing context propagated?

## 12. Documentation and Maintainability

- [ ] **XML documentation** - Are public APIs documented with XML comments?
- [ ] **Code comments** - Do comments explain "why", not "what"?
- [ ] **Naming clarity** - Are names descriptive and consistent with conventions?
- [ ] **Complexity** - Is cyclomatic complexity reasonable?
- [ ] **Dead code** - Is unused code removed rather than commented out?

---

## Quick Reference: Review Priority

| Priority | Category | Risk Level |
|----------|----------|------------|
| P0 | Data loss, security vulnerabilities, crashes | Critical |
| P1 | Concurrency bugs, resource leaks, broken functionality | High |
| P2 | Performance regressions, missing error handling | Medium |
| P3 | API design issues, missing tests | Medium |
| P4 | Code style, documentation gaps | Low |

Focus review effort on P0-P2 issues before addressing P3-P4 concerns.
