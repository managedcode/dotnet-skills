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
| Blazor components (`.razor`, `@page`, `RenderModeInteractiveServer`) | `blazor` |
| Minimal API patterns (`app.MapGet`, `app.MapPost`, no controllers) | `minimal-apis` |
| MVC or Web API controllers (`[ApiController]`, `ControllerBase`) | `web-api` |
| SignalR hubs (`Hub`, `IHubContext`, `/hubs/` routes) | `signalr` |
| gRPC services (`.proto`, `Grpc.AspNetCore`) | `grpc` |
| General ASP.NET Core hosting without specific pattern | `aspnet-core` |

### 2. Cloud and Hosting

| Indicator | Route To |
|-----------|----------|
| Aspire app host or service defaults (`Aspire.Hosting`, `AddProject`) | `aspire` |
| Azure Functions (`[Function]`, `Microsoft.Azure.Functions.Worker`) | `azure-functions` |
| Background services (`BackgroundService`, `IHostedService`) | `worker-services` |

### 3. Desktop and Client

| Indicator | Route To |
|-----------|----------|
| MAUI app (`Microsoft.Maui`, `.maui`) | `maui` |
| Uno Platform (`Uno.WinUI`, cross-platform XAML) | `uno-platform` |
| WinUI 3 (`Microsoft.WindowsAppSDK`, `WinUI`) | `winui` |
| WPF (`UseWPF`, `PresentationFramework`) | `wpf` |
| Windows Forms (`UseWindowsForms`, `System.Windows.Forms`) | `winforms` |
| MVVM patterns in any client app | `mvvm` |

### 4. Data and Distributed

| Indicator | Route To |
|-----------|----------|
| EF Core (`Microsoft.EntityFrameworkCore`, `DbContext`) | `entity-framework-core` |
| EF6 (`EntityFramework`, `System.Data.Entity`) | `entity-framework6` |
| Orleans grains and silos (`Orleans.Core`, `[Grain]`) | `orleans` |

### 5. AI and Agentic

| Indicator | Route To |
|-----------|----------|
| Semantic Kernel (`Microsoft.SemanticKernel`, `Kernel.CreateBuilder`) | `semantic-kernel` |
| Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`) | `microsoft-extensions-ai` |
| Microsoft Agent Framework (`Microsoft.Agents`) | `microsoft-agent-framework` |
| ML.NET (`Microsoft.ML`, `MLContext`) | `mlnet` |
| Mixed Reality (`Microsoft.MixedReality`) | `mixed-reality` |
| MCP servers (`ModelContextProtocol`) | `mcp` |

### 6. Legacy

| Indicator | Route To |
|-----------|----------|
| Legacy ASP.NET (`System.Web`, `Global.asax`) | `legacy-aspnet` |
| WCF (`System.ServiceModel`, `.svc`) | `wcf` |
| Windows Workflow Foundation (`System.Activities`) | `workflow-foundation` |

## Cross-Cutting Concerns

After platform routing, check if the task is primarily about a cross-cutting concern:

### Project and Architecture

| Concern | Route To |
|---------|----------|
| Project creation, solution structure, SDK selection | `project-setup` |
| Architecture decisions, patterns, layering | `architecture` |
| Microsoft.Extensions patterns (DI, config, logging) | `microsoft-extensions` |

### Code Quality and Review

| Concern | Route To |
|---------|----------|
| Code review, PR feedback | `code-review` |
| Modern C# language features | `modern-csharp` |

### Testing

| Concern | Route To |
|---------|----------|
| TUnit test framework | `tunit` |
| xUnit test framework | `xunit` |
| MSTest test framework | `mstest` |

### Formatting and Analysis

| Concern | Route To |
|---------|----------|
| `dotnet format` usage | `format` |
| CSharpier formatting | `csharpier` |
| Built-in code analysis, editorconfig | `code-analysis` |
| EditorConfig and analyzer configuration | `analyzer-config` |
| Roslyn analyzers (Roslynator) | `roslynator` |
| StyleCop rules | `stylecop-analyzers` |
| Meziantou analyzers | `meziantou-analyzer` |
| ReSharper CLI tools | `resharper-clt` |
| CodeQL security scanning | `codeql` |

### Quality and CI

| Concern | Route To |
|---------|----------|
| CI quality gates, build pipelines | `quality-ci` |
| Code coverage collection | `coverlet` |
| Coverage report generation | `reportgenerator` |
| Mutation testing | `stryker` |
| Complexity metrics | `complexity` |
| Lines of code counting | `cloc` |
| Duplicate code detection | `quickdup` |
| Performance profiling | `profiling` |

### Architecture Enforcement

| Concern | Route To |
|---------|----------|
| NetArchTest rules | `netarchtest` |
| ArchUnitNET rules | `archunitnet` |

## Multi-Skill Tasks

When a task spans multiple skills:

1. **Prefer the user-visible behavior skill first** - if the task is about adding a Blazor feature with tests, start with `blazor`
2. **Pull in quality/tooling skills second** - after the feature is implemented, route testing to `xunit` or `tunit`
3. **Do not combine incompatible guidance** - runner-specific commands and patterns should come from one skill at a time

## Fallback Behavior

If no narrower skill matches:

1. Stay at `dotnet` skill
2. Be explicit about missing specialization
3. Provide generic .NET guidance only when necessary
4. Suggest which skill should be created if the gap is recurring
