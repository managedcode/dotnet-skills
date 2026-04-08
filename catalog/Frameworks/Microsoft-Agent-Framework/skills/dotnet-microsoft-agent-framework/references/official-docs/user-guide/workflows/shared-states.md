---
title: Microsoft Agent Framework Workflows - Shared States
description: In-depth look at Shared States in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows - Shared States

This document provides an overview of **Shared States** in the Microsoft Agent Framework Workflow system.

## Overview

Shared States allow multiple executors within a workflow to access and modify common data. This feature is essential for scenarios where different parts of the workflow need to share information where direct message passing is not feasible or efficient.

## Writing to Shared States

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;

internal sealed class FileReadExecutor() : Executor<string, string>("FileReadExecutor")
{
    /// <summary>
    /// Reads a file and stores its content in a shared state.
    /// </summary>
    /// <param name="message">The path to the embedded resource file.</param>
    /// <param name="context">The workflow context for accessing shared states.</param>
    /// <returns>The ID of the shared state where the file content is stored.</returns>
    public async ValueTask<string> HandleAsync(string message, IWorkflowContext context)
    {
        // Read file content from embedded resource
        string fileContent = File.ReadAllText(message);
        // Store file content in a shared state for access by other executors
        string fileID = Guid.NewGuid().ToString();
        await context.QueueStateUpdateAsync<string>(fileID, fileContent, scopeName: "FileContent");

        return fileID;
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import (
    Executor,
    WorkflowContext,
    handler,
)

class FileReadExecutor(Executor):

    @handler
    async def handle(self, file_path: str, ctx: WorkflowContext[str]):
        # Read file content from embedded resource
        with open(file_path, 'r') as file:
            file_content = file.read()
        # Store file content in a shared state for access by other executors
        file_id = str(uuid.uuid4())
        await ctx.set_shared_state(file_id, file_content)

        await ctx.send_message(file_id)
```

::: zone-end

## Accessing Shared States

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;

internal sealed class WordCountingExecutor() : Executor<string, int>("WordCountingExecutor")
{
    /// <summary>
    /// Counts the number of words in the file content stored in a shared state.
    /// </summary>
    /// <param name="message">The ID of the shared state containing the file content.</param>
    /// <param name="context">The workflow context for accessing shared states.</param>
    /// <returns>The number of words in the file content.</returns>
    public async ValueTask<int> HandleAsync(string message, IWorkflowContext context)
    {
        // Retrieve the file content from the shared state
        var fileContent = await context.ReadStateAsync<string>(message, scopeName: "FileContent")
            ?? throw new InvalidOperationException("File content state not found");

        return fileContent.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
from agent_framework import (
    Executor,
    WorkflowContext,
    handler,
)

class WordCountingExecutor(Executor):

    @handler
    async def handle(self, file_id: str, ctx: WorkflowContext[int]):
        # Retrieve the file content from the shared state
        file_content = await ctx.get_shared_state(file_id)
        if file_content is None:
            raise ValueError("File content state not found")

        await ctx.send_message(len(file_content.split()))
```

::: zone-end

## Next Steps

- [Learn how to create checkpoints and resume from them](./checkpoints.md).
- [Learn how to monitor workflows](./observability.md).
- [Learn about state isolation in workflows](./state-isolation.md).
- [Learn how to visualize workflows](./visualization.md).
