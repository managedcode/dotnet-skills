---
name: dotnet-project-setup
description: "Create or reorganize .NET solutions with clean project boundaries, repeatable SDK settings, and a maintainable baseline for libraries, apps, tests, CI, and local development."
compatibility: "Best for new repositories or structural refactors of existing .NET solutions."
---

# .NET Project Setup

## Trigger On

- creating a new .NET solution or restructuring an existing one
- setting up `Directory.Build.props`, shared package management, or repo-wide defaults
- defining project layout for apps, libraries, and test projects

## Workflow

1. Start from the app model and deployment target, then choose the smallest correct SDK and target framework set.
2. Use solution folders and project names that reflect bounded contexts or product areas, not temporary implementation details.
3. Centralize shared build settings, analyzer rules, nullable context, and package versions where it reduces duplication without hiding important differences.
4. Create test projects and CI hooks early so new projects do not drift into unverified templates.
5. Prefer project references and composition over circular dependencies or utility dumping grounds.
6. Document the local build, test, and run path in repo docs or `AGENTS.md` when the workflow is not obvious.

## Deliver

- a coherent solution structure
- shared build defaults that are easy to reason about
- starter quality and testing hooks for future work

## Validate

- projects have explicit responsibility boundaries
- shared MSBuild settings do not accidentally override platform-specific needs
- a new contributor can build and test the repo without guessing

## References

- [patterns.md](references/patterns.md): solution layout conventions, `Directory.Build.props`, `Directory.Build.targets`, Central Package Management, `global.json`, `nuget.config`, analyzers, multi-targeting, and source link
- [templates.md](references/templates.md): `dotnet new` templates for console apps, class libraries, ASP.NET Core APIs, worker services, Blazor, test projects, .NET Aspire, and gRPC services
