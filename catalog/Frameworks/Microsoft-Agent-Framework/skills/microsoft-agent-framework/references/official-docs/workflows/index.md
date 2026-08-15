---
title: Workflow capabilities
description: Browse Agent Framework capabilities for agents, human input, checkpoints, declarative workflows, observability, visualization, and orchestration.
author: eavanvalkenburg
ms.topic: overview
ms.author: edvan
ms.date: 07/29/2026
ms.service: agent-framework
---

# Workflow capabilities

Workflow capabilities add production behaviors and reusable patterns to functional or graph-based workflows. For workflow APIs, executors, edges, events, state, and the execution model, see [Workflows](../concepts/workflows/index.md).

## Composition

| Capability | Purpose |
|---|---|
| [Agents in workflows](agents-in-workflows.md) | Use agents as workflow participants and executors. |
| [Workflows as agents](as-agents.md) | Expose a workflow through the standard agent interface. |
| [Declarative workflows](declarative.md) | Define supported workflows through declarative configuration. |

## Interaction and durability

| Capability | Purpose |
|---|---|
| [Human-in-the-loop](human-in-the-loop.md) | Pause for external input and resume execution. |
| [Checkpoints and resuming](checkpoints.md) | Save and restore workflow progress. |

## Operations

| Capability | Purpose |
|---|---|
| [Observability](observability.md) | Export workflow spans, metrics, events, and delivery status. |
| [Visualization](visualization.md) | Render and export workflow topology. |

## Multi-agent orchestration

[Orchestrations](orchestrations/index.md) provide sequential, concurrent, handoff, group-chat, and Magentic patterns for coordinating agents.

## Next steps

> [!div class="nextstepaction"]
> [Use agents in workflows](agents-in-workflows.md)
