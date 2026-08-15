---
title: Mem0
description: Add persistent Mem0 long-term memory to Agent Framework agents.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table - keep in sync when adding/removing sections.

  | Section              | C# | Python | Go | Notes                   |
  |----------------------|:--:|:------:|:--:|-------------------------|
  | Mem0 configuration   | ❌ |   ✅   | ❌ |                         |
  | Memory scoping       | ❌ |   ✅   | ❌ |                         |
  | Cross-session recall | ❌ |   ✅   | ❌ |                         |
  | Availability         | ✅ |   ✅   | ✅ | C# and Go are status only |
-->

# Mem0

Mem0 extracts durable memories from agent conversations and retrieves relevant memories in later runs. Use a stable user, agent, or application scope when memories should be available across sessions.

This integration uses the memory pattern: it extracts and recalls selected durable information rather than replaying the complete conversation transcript.

> [!IMPORTANT]
> Mem0 is a third-party system. Review its data handling, retention, regional boundaries, and service terms before sending application data.

:::zone pivot="programming-language-csharp"

> [!NOTE]
> Mem0 integration isn't currently available for Agent Framework .NET.

:::zone-end

:::zone pivot="programming-language-python"

## Install the package

```bash
pip install agent-framework-mem0 --pre
```

Set `MEM0_API_KEY` or pass an API key directly. Reusing the same `user_id` makes memories available across sessions.

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/context_providers/mem0/mem0_basic.py" range="31-80":::

Mem0 processes memories asynchronously. In production, use retry or service-aware consistency handling instead of relying on a fixed delay.

:::zone-end

:::zone pivot="programming-language-go"

> [!NOTE]
> Mem0 integration isn't currently available for Agent Framework Go. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Microsoft Foundry](microsoft-foundry.md#add-managed-semantic-memory)

**Go deeper:**

- [Context providers](../../../concepts/agents/conversations/context-providers.md)
- [Conversation sessions](../../../concepts/agents/conversations/session.md)
