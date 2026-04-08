---
title: Microsoft Agent Framework Workflows Orchestrations
description: In-depth look at Orchestrations in Microsoft Agent Framework Workflows.
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Orchestrations

Orchestrations are pre-built workflow patterns often with specially-built executors that allow developers to quickly create complex workflows by simply plugging in their own AI agents.

## Why Multi-Agent?

Traditional single-agent systems are limited in their ability to handle complex, multi-faceted tasks. By orchestrating multiple agents, each with specialized skills or roles, you can create systems that are more robust, adaptive, and capable of solving real-world problems collaboratively.

## Supported Orchestrations

| Pattern                       | Description                                                                                                                                                                                                 | Typical Use Case                                                      |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| [Concurrent](./concurrent.md) | A task is broadcast to all agents and processed concurrently.                                                                                                                                               | Parallel analysis, independent subtasks, ensemble decision making.    |
| [Sequential](./sequential.md) | Passes the result from one agent to the next in a defined order.                                                                                                                                            | Step-by-step workflows, pipelines, multi-stage processing.            |
| [Group Chat](./group-chat.md) | Assembles agents in a star topology with a manager controlling the flow of conversation.                                                                                                                    | Iterative refinement, collaborative problem-solving, content review.  |
| [Magentic](./magentic.md)     | A variant of group chat with a planner-based manager. Inspired by [MagenticOne](https://www.microsoft.com/en-us/research/articles/magentic-one-a-generalist-multi-agent-system-for-solving-complex-tasks/). | Complex, generalist multi-agent collaboration.                        |
| [Handoff](./handoff.md)       | Assembles agents in a mesh topology where agents can dynamically pass control based on context without a central manager.                                                                                   | Dynamic workflows, escalation, fallback, or expert handoff scenarios. |

## Next Steps

Explore the individual orchestration patterns to understand their unique features and how to use them effectively in your applications.
