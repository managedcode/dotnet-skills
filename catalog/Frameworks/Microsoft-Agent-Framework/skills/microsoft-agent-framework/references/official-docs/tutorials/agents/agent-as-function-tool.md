---
title: Using an agent as a function tool
description: Legacy tutorial alias retained locally; the live Learn URL now resolves into the broader Function Tools surface
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 03/17/2026
ms.service: agent-framework
---

# Using an agent as a function tool

> [!NOTE]
> The live Learn URL for this old tutorial now redirects to the canonical Function Tools article.
> Keep this local file only as a compatibility alias for existing references inside the skill catalog.

::: zone pivot="programming-language-csharp"

Use `AIAgent.AsAIFunction()` when one agent needs a bounded specialist capability without escalating to a full workflow.

## Current guidance

- keep the delegated behavior narrow and easy to reason about
- keep the outer agent in control of retries, fallbacks, and policy
- escalate to explicit workflows when control flow, approvals, or fan-out logic become important

```csharp
AIAgent coordinator = chatClient.AsAIAgent(
    instructions: "Delegate weather questions when needed.",
    tools: [weatherAgent.AsAIFunction()]);
```

For current runnable examples, load:

- `references/official-docs/tutorials/agents/function-tools.md`
- `references/official-docs/user-guide/agents/agent-tools.md`

::: zone-end
::: zone pivot="programming-language-python"

The live alias now resolves to the broader Function Tools article. Use the canonical live docs for current Python examples.

::: zone-end

## Next steps

- Use `references/official-docs/tutorials/agents/agent-as-mcp-tool.md` when the delegated capability should surface as an MCP tool instead of a normal function tool.
- Escalate to `references/workflows.md` when delegation becomes explicit orchestration instead of bounded tool composition.
