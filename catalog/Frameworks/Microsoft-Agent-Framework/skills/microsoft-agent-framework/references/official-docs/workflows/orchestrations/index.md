---
title: Workflow orchestrations in Agent Framework
description: Multi-agent orchestration patterns including sequential, concurrent, handoff, group chat, and magentic orchestrations.
author: eavanvalkenburg
ms.topic: article
ms.author: edvan
ms.date: 02/12/2026
ms.service: agent-framework
---

# Workflow orchestrations

Agent Framework provides several built-in multi-agent orchestration patterns:

| Pattern | Description |
|---------|-------------|
| [Sequential](sequential.md) | Agents execute one after another in a defined order |
| [Concurrent](concurrent.md) | Agents execute in parallel |
| [Handoff](handoff.md) | Agents transfer control to each other based on context |
| [Group Chat](group-chat.md) | Agents collaborate in a shared conversation |
| [Magentic](magentic.md) | A manager agent dynamically coordinates specialized agents |

> [!TIP]
> Orchestrations support **human-in-the-loop** interactions through tool approval and request info. Agents can use approval-required tools that pause the workflow for human review before execution. See [Human-in-the-Loop](../human-in-the-loop.md) and the [sequential orchestration HITL tutorial](sequential.md#sequential-orchestration-with-human-in-the-loop) for details.

## Next steps

> [!div class="nextstepaction"]
> [Sequential Orchestration](sequential.md)
