# Routing Decision Tree

This reference defines the decision tree for routing tasks from the generic `dotnet` skill to the narrowest matching specialized skill.

## Primary Classification

Start by classifying the repository or task by its **primary app model**, then refine by **cross-cutting concerns**.

```
Is the task about a specific framework or platform?
|
+-- YES --> Route to the platform skill immediately
|
+-- NO --> Continue to app model detection
```

## App Model Detection Order

Evaluate project indicators in this order and route to the first matching skill:

### 1. Web and API

| Indicator | Route To |
|-----------|----------|
| Blazor components (`.razor`, `@page`, `RenderModeInteractiveServer`) | `dotnet-blazor` |
| Minimal API patterns (`app.MapGet`, `app.MapPost`, no controllers) | `dotnet-minimal-apis` |
| MVC or Web API controllers (`[ApiController]`, `ControllerBase`) | `dotnet-web-api` |
| SignalR hubs (`Hub`, `IHubContext`, `/hubs/` routes) | `dotnet-signalr` |
| gRPC services (`.proto`, `Grpc.AspNetCore`) | `dotnet-grpc` |
| General ASP.NET Core hosting without specific pattern | `dotnet-aspnet-core` |

### 2. Cloud and Hosting

| Indicator | Route To |
|-----------|----------|
| Aspire app host or service defaults (`Aspire.Hosting`, `AddProject`) | `dotnet-aspire` |
| Azure Functions (`[Function]`, `Microsoft.Azure.Functions.Worker`) | `dotnet-azure-functions` |
| Background services (`BackgroundService`, `IHostedService`) | `dotnet-worker-services` |

### 3. Desktop and Client

| Indicator | Route To |
|-----------|----------|
| MAUI app (`Microsoft.Maui`, `.maui`) | `dotnet-maui` |
| Uno Platform (`Uno.WinUI`, cross-platform XAML) | `dotnet-uno-platform` |
| WinUI 3 (`Microsoft.WindowsAppSDK`, `WinUI`) | `dotnet-winui` |
| WPF (`UseWPF`, `PresentationFramework`) | `dotnet-wpf` |
| Windows Forms (`UseWindowsForms`, `System.Windows.Forms`) | `dotnet-winforms` |
| MVVM patterns in any client app | `dotnet-mvvm` |

### 4. Data and Distributed

| Indicator | Route To |
|-----------|----------|
| EF Core (`Microsoft.EntityFrameworkCore`, `DbContext`) | `dotnet-entity-framework-core` |
| EF6 (`EntityFramework`, `System.Data.Entity`) | `dotnet-entity-framework6` |
| Orleans grains and silos (`Orleans.Core`, `[Grain]`) | `dotnet-orleans` |

### 5. AI and Agentic

| Indicator | Route To |
|-----------|----------|
| Semantic Kernel (`Microsoft.SemanticKernel`, `Kernel.CreateBuilder`) | `dotnet-semantic-kernel` |
| Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`) | `dotnet-microsoft-extensions-ai` |
| Microsoft Agent Framework (`Microsoft.Agents`) | `dotnet-microsoft-agent-framework` |
| ML.NET (`Microsoft.ML`, `MLContext`) | `dotnet-mlnet` |
| Mixed Reality (`Microsoft.MixedReality`) | `dotnet-mixed-reality` |
| MCP servers (`ModelContextProtocol`) | `dotnet-mcp` |

### 6. Legacy

| Indicator | Route To |
|-----------|----------|
| Legacy ASP.NET (`System.Web`, `Global.asax`) | `dotnet-legacy-aspnet` |
| WCF (`System.ServiceModel`, `.svc`) | `dotnet-wcf` |
| Windows Workflow Foundation (`System.Activities`) | `dotnet-workflow-foundation` |

## Cross-Cutting Concerns

After platform routing, check if the task is primarily about a cross-cutting concern:

### Project and Architecture

| Concern | Route To |
|---------|----------|
| Project creation, solution structure, SDK selection | `dotnet-project-setup` |
| Architecture decisions, patterns, layering | `dotnet-architecture` |
| Microsoft.Extensions patterns (DI, config, logging) | `dotnet-microsoft-extensions` |

### Code Quality and Review

| Concern | Route To |
|---------|----------|
| Code review, PR feedback | `dotnet-code-review` |
| Modern C# language features | `dotnet-modern-csharp` |

### Testing

| Concern | Route To |
|---------|----------|
| TUnit test framework | `dotnet-tunit` |
| xUnit test framework | `dotnet-xunit` |
| MSTest test framework | `dotnet-mstest` |

### Formatting and Analysis

| Concern | Route To |
|---------|----------|
| `dotnet format` usage | `dotnet-format` |
| CSharpier formatting | `dotnet-csharpier` |
| Built-in code analysis, editorconfig | `dotnet-code-analysis` |
| EditorConfig and analyzer configuration | `dotnet-analyzer-config` |
| Roslyn analyzers (Roslynator) | `dotnet-roslynator` |
| StyleCop rules | `dotnet-stylecop-analyzers` |
| Meziantou analyzers | `dotnet-meziantou-analyzer` |
| ReSharper CLI tools | `dotnet-resharper-clt` |
| CodeQL security scanning | `dotnet-codeql` |

### Quality and CI

| Concern | Route To |
|---------|----------|
| CI quality gates, build pipelines | `dotnet-quality-ci` |
| Code coverage collection | `dotnet-coverlet` |
| Coverage report generation | `dotnet-reportgenerator` |
| Mutation testing | `dotnet-stryker` |
| Complexity metrics | `dotnet-complexity` |
| Lines of code counting | `dotnet-cloc` |
| Duplicate code detection | `dotnet-quickdup` |
| Performance profiling | `dotnet-profiling` |

### Architecture Enforcement

| Concern | Route To |
|---------|----------|
| NetArchTest rules | `dotnet-netarchtest` |
| ArchUnitNET rules | `dotnet-archunitnet` |

## Multi-Skill Tasks

When a task spans multiple skills:

1. **Prefer the user-visible behavior skill first** - if the task is about adding a Blazor feature with tests, start with `dotnet-blazor`
2. **Pull in quality/tooling skills second** - after the feature is implemented, route testing to `dotnet-xunit` or `dotnet-tunit`
3. **Do not combine incompatible guidance** - runner-specific commands and patterns should come from one skill at a time

## Fallback Behavior

If no narrower skill matches:

1. Stay at `dotnet` skill
2. Be explicit about missing specialization
3. Provide generic .NET guidance only when necessary
4. Suggest which skill should be created if the gap is recurring
