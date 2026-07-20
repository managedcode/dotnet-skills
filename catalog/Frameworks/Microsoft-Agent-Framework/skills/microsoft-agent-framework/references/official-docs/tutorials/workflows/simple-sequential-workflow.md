---
title: Create a Simple Sequential Workflow
description: Route the retired tutorial URL to the current sequential orchestration guidance.
ms.topic: tutorial
ms.date: 07/01/2026
ms.service: agent-framework
---

# Create a Simple Sequential Workflow

The watched Learn URL for this former tutorial now resolves to the canonical Sequential Orchestration page. Do not use the older two-executor tutorial as the current API contract.

Current .NET guidance:

- build an agent pipeline with `AgentWorkflowBuilder.BuildSequential(...)`
- run it with `InProcessExecution.RunStreamingAsync(...)`
- expect each stage to receive the previous agent's full input-and-response conversation by default
- use the documented response-only context option when the next stage should consume only the prior response
- wrap side-effecting tools with `ApprovalRequiredAIFunction` and handle the resulting `RequestInfoEvent`

Load `../../user-guide/workflows/orchestrations/sequential.md` for the current local C# patterns.

Live source: https://learn.microsoft.com/agent-framework/tutorials/workflows/simple-sequential-workflow
