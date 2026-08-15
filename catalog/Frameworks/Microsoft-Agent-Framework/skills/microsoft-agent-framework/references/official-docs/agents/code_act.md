---
title: CodeAct
description: Learn what CodeAct is and when to use it with Agent Framework.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 05/27/2026
ms.service: agent-framework
---
<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section                    | C# | Python | Go | Notes                                  |
  |----------------------------|:--:|:------:|:--:|----------------------------------------|
  | Why CodeAct                | ✅ |   ✅   | ❌ | Shared pattern guidance                |
  | Good fit for CodeAct       | ✅ |   ✅   | ❌ | Shared decision guidance               |
  | How CodeAct fits           | ✅ |   ✅   | ❌ | Shared framework model                 |
  | Current limitations        | ✅ |   ✅   | ❌ | Shared current-state guidance          |
  | Getting started            | ✅ |   ✅   | ❌ | C#/Python document Hyperlight          |
  | Hyperlight integration     | ✅ |   ✅   | ❌ | No Go CodeAct backend documented       |
-->

# CodeAct

CodeAct lets an agent solve a task by writing code and executing it through an `execute_code` tool. Instead of asking the model to emit one tool call at a time, CodeAct gives it a sandboxed place to combine control flow, data transformation, and tool orchestration inside a single execution step.

In Agent Framework, CodeAct is exposed through backend-specific packages rather than a single built-in core type. A connector can add the `execute_code` tool, inject runtime guidance, and optionally expose provider-owned tools that are callable from inside the sandbox.

## Why CodeAct

Modern AI agents often are not bottlenecked by model quality, but by orchestration overhead. When an agent chains together many small tool calls, each step usually requires another model turn, which increases both latency and token usage.

CodeAct collapses that model -> tool -> model loop. Instead of asking the model to pick one tool at a time, Agent Framework can expose a single `execute_code` tool and let the model express the full plan as a short program. The tools stay the same, the model stays the same, and the main change is that the plan runs once inside a sandbox instead of being scattered across several tool-call turns.

For tool-heavy workloads, that can materially reduce end-to-end latency and token usage while keeping the plan compact and auditable in one code block. See the [Hyperlight CodeAct integration](../integrations/by-component/context-providers/hyperlight.md) for a side-by-side wiring comparison.

## When CodeAct is a good fit

Use CodeAct when a task benefits from:

- combining multiple tool calls with loops, branching, filtering, or aggregation
- transforming tool results before returning a final answer
- generating larger structured outputs or artifacts as part of a run
- keeping some tools available only inside a controlled execution environment
- collapsing many small, chainable lookups or lightweight computations into one execution step

Stay with direct tool calling when:

- the task only needs one or two tool calls, so there is little orchestration overhead to remove
- each call has side effects that should stay individually visible to the model and the user
- you need per-call approval prompts instead of one approval decision around the whole `execute_code` run

## How CodeAct fits in Agent Framework

A CodeAct connector typically does four things for a run:

1. Adds an `execute_code` tool to the model-facing tool surface.
2. Supplies instructions for the configured sandbox runtime.
3. Optionally exposes provider-owned tools through `call_tool(...)`.
4. Applies capability limits such as filesystem access or outbound-network allow lists.

Because the connector owns the runtime configuration, the exact setup details depend on the backend you choose.

## Current limitations

CodeAct is a strong fit for tool-heavy workflows, but there are a few current constraints to keep in mind:

- The documented Agent Framework connector today is [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md), available for both Python and .NET (in preview).
- Approvals currently apply to the `execute_code` call as a whole. If you need individual operations to be approved one by one, keep those operations as direct agent tools instead of relying on `call_tool(...)`.
- Tools reached through `call_tool(...)` still execute in the host process. Use narrow, reviewed host tools for sensitive I/O instead of broadening sandbox access unnecessarily.
- CodeAct works best when orchestration overhead dominates. For small tasks with only one or two tool calls, the added abstraction may not buy you much.
- Tool names, parameter metadata, and return shapes matter more here because the model is writing code against that contract rather than choosing from one direct tool call at a time.

::: zone pivot="programming-language-csharp"

## Get started

For .NET, the documented connector today is [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md), shipped as the `Microsoft.Agents.AI.Hyperlight` package.

The package provides:

- `HyperlightCodeActProvider` — an `AIContextProvider` that injects `execute_code` and CodeAct guidance for every run
- `HyperlightExecuteCodeFunction` — a standalone `AIFunction` for static/manual wiring when the sandbox configuration is fixed
- provider-managed tools that remain available inside the sandbox through `call_tool(...)`
- `CodeActApprovalMode` and `ApprovalRequiredAIFunction` integration for approvals
- optional filesystem (`FileMounts`, `HostInputDirectory`) and outbound-network (`AllowedDomains`) configuration for the sandbox runtime

> [!IMPORTANT]
> The .NET package is in preview and depends on the `Hyperlight.HyperlightSandbox.Api` NuGet, which is not yet published on nuget.org. See [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md) for current install caveats and platform requirements.

See [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md) for installation, examples, and runtime-specific guidance.

::: zone-end

::: zone pivot="programming-language-python"

## Get started

For Python, the documented connector today is [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md).

The Hyperlight package provides:

- `HyperlightCodeActProvider` for context-provider-based runs
- `HyperlightExecuteCodeTool` when you want to wire `execute_code` directly
- provider-managed tools that remain available inside the sandbox through `call_tool(...)`
- optional filesystem and outbound-network configuration for the sandbox runtime

See [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md) for installation, examples, runtime-specific guidance such as when to use `print(...)` and `/output/`, and the current Hyperlight-specific limitations.

::: zone-end


::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Agent Safety](../concepts/agents/safety.md)

### Related content

- [Hyperlight CodeAct](../integrations/by-component/context-providers/hyperlight.md)
- [CodeAct paper](https://arxiv.org/abs/2402.01030)
- [Code Interpreter](./tools/code-interpreter.md)
- [Tool Approval](./tools/tool-approval.md)
- [Context Providers](../concepts/agents/conversations/context-providers.md)
