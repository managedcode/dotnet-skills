---
title: Adding middleware to agents
description: Legacy tutorial alias retained locally; the live Learn URL now resolves to the canonical middleware article
zone_pivot_groups: programming-languages
author: dmytrostruk
ms.topic: tutorial
ms.author: dmytrostruk
ms.date: 03/17/2026
ms.service: agent-framework
---

# Adding middleware to agents

> [!NOTE]
> The live Learn URL for this old tutorial now resolves to the same canonical middleware page as `user-guide/agents/agent-middleware.md`:
> `https://learn.microsoft.com/agent-framework/agents/middleware/`
>
> Keep this local file only as a compatibility alias for existing references inside the skill catalog.

Learn how to add middleware to your agents in a few simple steps. Middleware allows you to intercept and modify agent interactions for logging, security, and other cross-cutting concerns.

::: zone pivot="programming-language-csharp"

For the current C# walkthrough, use `references/official-docs/user-guide/agents/agent-middleware.md`.

## Current tutorial-level takeaways

1. Register agent-run middleware through `AsBuilder().Use(...)`.
2. Prefer supplying both `runFunc` and `runStreamingFunc`; use `Use(sharedFunc: ...)` only for input inspection that should not rewrite output.
3. Current run middleware examples use `AgentSession? session` instead of `AgentThread? thread`.
4. Function middleware is still the right place for approvals, allow/deny policy, and risky-tool interception.
5. Current official C# samples use `DefaultAzureCredential` and explicitly warn against carrying that default into production unchanged.

::: zone-end
::: zone pivot="programming-language-python"

The live alias now resolves to the canonical middleware article. Use the canonical live docs for current Python examples.

::: zone-end
