---
title: Create a Simple Concurrent Workflow
description: Learn how to create a simple concurrent workflow.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 03/17/2026
ms.service: agent-framework
---

# Create a Simple Concurrent Workflow

This tutorial demonstrates how to create a concurrent workflow using Agent Framework. You'll learn to implement fan-out and fan-in patterns that enable parallel processing, allowing multiple agents to work simultaneously on the same input and then aggregate their results.

::: zone pivot="programming-language-csharp"

## What You'll Build

You'll create a workflow that:

- Takes a user message as input (for example, "Hello, world!")
- Sends the same message to multiple translation agents simultaneously
- Collects and aggregates responses from all agents into a single output
- Demonstrates concurrent execution with `AgentWorkflowBuilder.BuildConcurrent`

### Concepts Covered

- [Executors](../../user-guide/workflows/core-concepts/executors.md)
- [Fan-out Edges](../../user-guide/workflows/core-concepts/edges.md#fan-out-edges)
- [Fan-in Edges](../../user-guide/workflows/core-concepts/edges.md#fan-in-edges)
- [Workflow Builder](../../user-guide/workflows/core-concepts/workflows.md)
- [Events](../../user-guide/workflows/core-concepts/events.md)

## Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- [Azure OpenAI service endpoint and deployment configured](/azure/ai-foundry/openai/how-to/create-resource)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated (for Azure credential authentication)](/cli/azure/authenticate-azure-cli)
- A new console application

## Step 1: Install NuGet packages

First, install the required packages for your .NET project:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease
```

## Step 2: Set Up Dependencies and Azure OpenAI

Start by setting up your project with the required NuGet packages and Azure OpenAI client:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public static class Program
{
    private static async Task Main()
    {
        // Set up the Azure OpenAI client
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
        var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(deploymentName).AsIChatClient();
```

## Step 3: Create Specialized AI Agents

Create multiple specialized agents that will each process the same input concurrently:

```csharp
        // Helper method to create a translation agent for a target language
        static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient client) =>
            new(client,
                $"You are a translation assistant who only responds in {targetLanguage}. " +
                $"Respond to any input by outputting the name of the input language and then " +
                $"translating the input to {targetLanguage}.");

        // Create translation agents for concurrent processing
        var translationAgents = new[] { "French", "Spanish", "English" }
            .Select(lang => GetTranslationAgent(lang, chatClient));
```

## Step 4: Build the Concurrent Workflow

Use `AgentWorkflowBuilder.BuildConcurrent` to create the concurrent workflow from the agent collection. The builder automatically handles the fan-out and fan-in logic:

```csharp
        // Build the concurrent workflow - fan-out and fan-in are handled automatically
        var workflow = AgentWorkflowBuilder.BuildConcurrent(translationAgents);
```

## Step 5: Execute the Workflow

Run the workflow, send the turn token to kick off the agents, and capture the streaming output:

```csharp
        // Execute the workflow in streaming mode
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello, world!") };
        await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);

        // Send a turn token to start agent processing
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        List<ChatMessage> result = new();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                Console.WriteLine($"{e.ExecutorId}: {e.Data}");
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                result = (List<ChatMessage>)outputEvt.Data!;
                break;
            }
        }

        Console.WriteLine("===== Final Aggregated Results =====");
        foreach (var msg in result)
        {
            Console.WriteLine($"{msg.Role}: {msg.Content}");
        }
    }
}
```

## How It Works

1. **Fan-Out**: `AgentWorkflowBuilder.BuildConcurrent` distributes the same input to all agents simultaneously.
2. **Parallel Processing**: All agents process the same message concurrently, each providing their unique perspective.
3. **Turn Token**: `TurnToken` signals agents to begin processing the queued message.
4. **Fan-In / Aggregation**: Results from all agents are automatically collected into a `List<ChatMessage>` output.

## Key Concepts

- **`AgentWorkflowBuilder.BuildConcurrent(agents)`**: High-level builder method that creates a concurrent workflow from an `IEnumerable<AIAgent>`. Handles fan-out and fan-in automatically without requiring custom executor classes.
- **Custom Aggregator**: An optional `Func<IList<List<ChatMessage>>, List<ChatMessage>>` overload lets you provide custom aggregation logic.
- **Turn Tokens**: Use `TurnToken` to signal agents to begin processing queued messages.
- **`AgentResponseUpdateEvent`**: Streaming event for real-time per-agent progress.
- **`WorkflowOutputEvent`**: Terminal event carrying the aggregated `List<ChatMessage>` from all agents.

## Advanced: Manual Fan-Out / Fan-In with Custom Executors

For scenarios requiring fine-grained control over the dispatcher or aggregation logic, you can build the concurrent graph directly with `WorkflowBuilder`:

```csharp
// Custom executor that dispatches the user message and turn token to all connected agents
internal sealed class ConcurrentStartExecutor() : Executor<string>("ConcurrentStartExecutor")
{
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, message), cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}

// Custom executor that aggregates individual ChatMessage responses from each agent
internal sealed class ConcurrentAggregationExecutor(int agentCount) :
    Executor<ChatMessage>("ConcurrentAggregationExecutor")
{
    private readonly List<ChatMessage> _messages = [];

    public override async ValueTask HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        this._messages.Add(message);
        if (this._messages.Count == agentCount)
        {
            var formatted = string.Join(Environment.NewLine,
                this._messages.Select(m => $"{m.AuthorName}: {m.Text}"));
            await context.YieldOutputAsync(formatted, cancellationToken);
        }
    }
}
```

Build the graph manually:

```csharp
var startExecutor = new ConcurrentStartExecutor();
var aggregationExecutor = new ConcurrentAggregationExecutor(agentCount: 2);

var physicistAgent = new ChatClientAgent(chatClient, name: "Physicist",
    instructions: "You are an expert in physics.");
var chemistAgent = new ChatClientAgent(chatClient, name: "Chemist",
    instructions: "You are an expert in chemistry.");

var workflow = new WorkflowBuilder(startExecutor)
    .AddFanOutEdge(startExecutor, targets: [physicistAgent, chemistAgent])
    .AddFanInEdge(aggregationExecutor, sources: [physicistAgent, chemistAgent])
    .WithOutputFrom(aggregationExecutor)
    .Build();
```

Use this approach when:
- You need a custom dispatcher that does more than broadcast a message and turn token.
- Your aggregation logic requires domain-specific processing before yielding output.
- The agent count or structure is not known at build time and cannot be expressed with `BuildConcurrent`.

## Complete Implementation

For the complete working implementation of this concurrent workflow with AI agents, see the [concurrent orchestration sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/Workflows/Concurrent) in the Agent Framework repository.

::: zone-end

::: zone pivot="programming-language-python"

In the Python implementation, you'll build a concurrent workflow that processes data through multiple parallel executors and aggregates results of different types. This example demonstrates how the framework handles mixed result types from concurrent processing.

## What You'll Build

You'll create a workflow that:

- Takes a list of numbers as input
- Distributes the list to two parallel executors (one calculating average, one calculating sum)
- Aggregates the different result types (float and int) into a final output
- Demonstrates how the framework handles different result types from concurrent executors

### Concepts Covered

- [Executors](../../user-guide/workflows/core-concepts/executors.md)
- [Fan-out Edges](../../user-guide/workflows/core-concepts/edges.md#fan-out-edges)
- [Fan-in Edges](../../user-guide/workflows/core-concepts/edges.md#fan-in-edges)
- [Workflow Builder](../../user-guide/workflows/core-concepts/workflows.md)
- [Events](../../user-guide/workflows/core-concepts/events.md)

## Prerequisites

- Python 3.10 or later
- Agent Framework Core installed: `pip install agent-framework-core --pre`

## Step 1: Import Required Dependencies

Start by importing the necessary components from Agent Framework:

```python
import asyncio
import random

from agent_framework import Executor, WorkflowBuilder, WorkflowContext, WorkflowOutputEvent, handler
from typing_extensions import Never
```

## Step 2: Create the Dispatcher Executor

The dispatcher is responsible for distributing the initial input to multiple parallel executors:

```python
class Dispatcher(Executor):
    """
    The sole purpose of this executor is to dispatch the input of the workflow to
    other executors.
    """

    @handler
    async def handle(self, numbers: list[int], ctx: WorkflowContext[list[int]]):
        if not numbers:
            raise RuntimeError("Input must be a valid list of integers.")

        await ctx.send_message(numbers)
```

## Step 3: Create Parallel Processing Executors

Create two executors that will process the data concurrently:

```python
class Average(Executor):
    """Calculate the average of a list of integers."""

    @handler
    async def handle(self, numbers: list[int], ctx: WorkflowContext[float]):
        average: float = sum(numbers) / len(numbers)
        await ctx.send_message(average)


class Sum(Executor):
    """Calculate the sum of a list of integers."""

    @handler
    async def handle(self, numbers: list[int], ctx: WorkflowContext[int]):
        total: int = sum(numbers)
        await ctx.send_message(total)
```

## Step 4: Create the Aggregator Executor

The aggregator collects results from the parallel executors and yields the final output:

```python
class Aggregator(Executor):
    """Aggregate the results from the different tasks and yield the final output."""

    @handler
    async def handle(self, results: list[int | float], ctx: WorkflowContext[Never, list[int | float]]):
        """Receive the results from the source executors.

        The framework will automatically collect messages from the source executors
        and deliver them as a list.

        Args:
            results (list[int | float]): execution results from upstream executors.
                The type annotation must be a list of union types that the upstream
                executors will produce.
            ctx (WorkflowContext[Never, list[int | float]]): A workflow context that can yield the final output.
        """
        await ctx.yield_output(results)
```

## Step 5: Build the Workflow

Connect the executors using fan-out and fan-in edge patterns:

```python
async def main() -> None:
    # 1) Create the executors
    dispatcher = Dispatcher(id="dispatcher")
    average = Average(id="average")
    summation = Sum(id="summation")
    aggregator = Aggregator(id="aggregator")

    # 2) Build a simple fan out and fan in workflow
    workflow = (
        WorkflowBuilder()
        .set_start_executor(dispatcher)
        .add_fan_out_edges(dispatcher, [average, summation])
        .add_fan_in_edges([average, summation], aggregator)
        .build()
    )
```

## Step 6: Run the Workflow

Execute the workflow with sample data and capture the output:

```python
    # 3) Run the workflow
    output: list[int | float] | None = None
    async for event in workflow.run_stream([random.randint(1, 100) for _ in range(10)]):
        if isinstance(event, WorkflowOutputEvent):
            output = event.data

    if output is not None:
        print(output)

if __name__ == "__main__":
    asyncio.run(main())
```

## How It Works

1. **Fan-Out**: The `Dispatcher` receives the input list and sends it to both the `Average` and `Sum` executors simultaneously
2. **Parallel Processing**: Both executors process the same input concurrently, producing different result types:
   - `Average` executor produces a `float` result
   - `Sum` executor produces an `int` result
3. **Fan-In**: The `Aggregator` receives results from both executors as a list containing both types
4. **Type Handling**: The framework automatically handles the different result types using union types (`int | float`)

## Key Concepts

- **Fan-Out Edges**: Use `add_fan_out_edges()` to send the same input to multiple executors
- **Fan-In Edges**: Use `add_fan_in_edges()` to collect results from multiple source executors
- **Union Types**: Handle different result types using type annotations like `list[int | float]`
- **Concurrent Execution**: Multiple executors process data simultaneously, improving performance

## Complete Implementation

For the complete working implementation of this concurrent workflow, see the [aggregate_results_of_different_types.py](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/workflows/parallelism/aggregate_results_of_different_types.py) sample in the Agent Framework repository.

::: zone-end

## Next Steps

> [!div class="nextstepaction"]
> [Learn about using agents in workflows](agents-in-workflows.md)
