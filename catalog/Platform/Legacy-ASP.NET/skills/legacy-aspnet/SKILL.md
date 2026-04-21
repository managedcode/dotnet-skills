---
name: legacy-aspnet
description: "Maintain classic ASP.NET applications on .NET Framework, including Web Forms, older MVC, and legacy hosting patterns, while planning realistic modernization boundaries."
compatibility: "Requires classic ASP.NET on .NET Framework."
---

# Legacy ASP.NET

## Trigger On

- working in Web Forms, legacy MVC, or classic ASP.NET applications
- reviewing old IIS-centric configuration and lifecycle behavior
- planning migration toward ASP.NET Core without breaking core business flows

## Workflow

1. Treat classic ASP.NET as a distinct stack with different hosting, lifecycle, and configuration rules from ASP.NET Core.
2. Stabilize behavior first: routing, session, auth, server controls, configuration transforms, and deployment assumptions.
3. Plan modernization in seams: isolate domain and service logic, then move replaceable edges instead of rewriting the whole app at once.
4. Use `wcf` or `entity-framework6` when the legacy app depends on those subsystems rather than flattening them into generic web work.
5. Be careful with guidance copied from ASP.NET Core because middleware, DI, and hosting assumptions do not transfer directly.
6. Validate in an environment that resembles real IIS and configuration transforms.

## Deliver

- practical maintenance guidance for classic ASP.NET
- stabilized legacy behavior and modernization seams
- a migration path that avoids unnecessary risk

## Validate

- classic and Core guidance are not mixed
- legacy runtime assumptions are preserved deliberately
- migration steps are incremental and testable

## References

- [Migration Paths](references/migration.md): strategies for migrating from ASP.NET to ASP.NET Core, including incremental migration, strangler fig pattern, and component-specific guidance
- [Maintenance Patterns](references/patterns.md): stabilization and maintenance patterns for legacy ASP.NET code, including abstraction layers, testing seams, and deployment practices
