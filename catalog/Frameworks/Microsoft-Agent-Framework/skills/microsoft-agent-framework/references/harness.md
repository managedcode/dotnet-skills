# Agent Framework Harness

Use this reference when a .NET task needs the packaged long-running agent runtime rather than a narrow `AIAgent` or an explicit `Workflow`.

Official source: [Agent harnesses](https://learn.microsoft.com/agent-framework/agents/harness)

The core `HarnessAgent` and `AsHarnessAgent` APIs graduated in `dotnet-1.14.0`, but the `Microsoft.Agents.AI.Harness` package remains prerelease and advanced options can remain experimental. Pin and review the package version instead of treating every Harness capability as production-stable.

## Select the Harness Deliberately

Choose `HarnessAgent` when the same agent needs several of these capabilities together:

- automatic tool invocation with a bounded iteration count
- per-service-call history persistence and crash inspection
- plan/execute modes plus a persistent todo list
- token-budget-aware context compaction
- file memory or working-directory-scoped file access
- standing tool approvals, OpenTelemetry, or hosted web search
- optional Agent Skills, shell execution, looping, or background-agent delegation

Use a normal `ChatClientAgent` when one agent with a few tools is enough. Use a typed `Workflow` when execution order, branches, checkpoints, or human requests must stay explicit and testable.

## Install and Create

```bash
dotnet add package Microsoft.Agents.AI.Harness --prerelease
```

Create the harness from any `IChatClient` and keep the session across turns:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "repository-maintainer",
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 16_384,
    DisableWebSearch = true,
});

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(
    "Inspect the repository and propose the smallest safe change.",
    session);
```

`MaxContextWindowTokens` and `MaxOutputTokens` enable the default compaction strategy. Without token limits or a custom strategy, do not assume compaction is active.

## Scope the Packaged Capabilities

| Capability | Default posture |
| --- | --- |
| Todo and plan/execute modes | Keep for genuinely multi-step work; disable for short request/response agents |
| File memory | Give it a task-owned location and lifecycle |
| File access | Opt in only by supplying a task-scoped `FileAccessStore`; the former `DisableFileAccess` option was removed |
| Web search | Disable when external retrieval is unnecessary or disallowed |
| Tool auto-approval | Keep side-effecting tools outside standing approvals |
| Agent Skills | Point `AgentSkillsSource` only at trusted skill roots |
| Shell | Supply a task-scoped `ShellExecutor`; treat deny lists and directory confinement as UX guardrails, not a security sandbox |
| Background agents | Provide a narrow allowlist and bound fan-out, cost, and completion criteria |
| Looping | Add explicit evaluators and maximum iterations |
| OpenTelemetry | Keep enabled unless the application has a deliberate alternate observability pipeline |

Approval responses are session-bound to approval requests that were actually surfaced. Reject stale or mismatched approval responses rather than treating them as a reusable authorization token.

## Validation

- prove that the task needs the packaged Harness surface instead of a normal agent
- persist and restore one `AgentSession` across turns
- force context growth far enough to exercise the configured compaction strategy
- test approval denial, approval reuse, and one side-effecting tool
- verify file access is absent until a `FileAccessStore` is supplied
- verify file and shell actions cannot escape the intended working directory at the application boundary
- bound tool-loop, outer-loop, and background-agent execution
- capture OpenTelemetry spans for model calls, tool calls, approvals, and failures
