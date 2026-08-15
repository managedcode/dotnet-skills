---
title: Agents in Workflows
description: Learn how to integrate agents into workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 07/01/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                        | C# | Python | Go | Notes                              |
    |--------------------------------|:--:|:------:|:--:|----------------------------------  |
    | What You'll Build              | ✅ |   ✅   | ✅ |                                    |
    | Concepts Covered               | ✅ |   ✅   | ✅ |                                    |
    | Prerequisites                  | ✅ |   ✅   | ✅ |                                    |
    | Client/Agent Setup             | ✅ |   ✅   | ✅ | Different Azure services           |
    | Create Specialized Agents      | ✅ |   ✅   | ✅ |                                    |
    | Build the Workflow             | ✅ |   ✅   | ✅ |                                    |
    | Shared Session Pattern         | ❌ |   ❌   | ❌ | Planned for future tutorial        |
    | Execute with Streaming         | ✅ |   ✅   | ✅ |                                    |
    | Resource Cleanup               | ✅ |   ❌   | ❌ | C#-specific (Azure Foundry agents) |
    | How It Works                   | ✅ |   ✅   | ✅ |                                    |
    | Key Concepts                   | ✅ |   ✅   | ✅ |                                    |
    | Complete Implementation        | ✅ |   ✅   | ✅ | Go includes complete sample        |
-->

# Agents in Workflows

This tutorial demonstrates how to integrate AI agents into workflows using Agent Framework. You'll learn to create workflows that leverage the power of specialized AI agents for content creation, review, and other collaborative tasks.

::: zone pivot="programming-language-csharp"

## What You'll Build

You'll create a workflow that:

- Uses Azure Foundry Agent Service to create intelligent agents
- Implements a French translation agent that translates input to French
- Implements a Spanish translation agent that translates French to Spanish
- Implements an English translation agent that translates Spanish back to English
- Connects agents in a sequential workflow pipeline
- Streams real-time updates as agents process requests
- Demonstrates proper resource cleanup for Azure Foundry agents

### Concepts Covered

- [Agents in Workflows](./agents-in-workflows.md)
- [Direct edges](../concepts/workflows/edges.md#direct-edges)
- [Workflow Builder](../concepts/workflows/builder-and-execution.md)

## Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- An Azure Foundry project endpoint and model configured
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated (for Azure credential authentication)](/cli/azure/authenticate-azure-cli)
- A new console application

## Step 1: Install NuGet packages

First, install the required packages for your .NET project:

```dotnetcli
dotnet add package Azure.AI.Projects --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.Foundry --prerelease
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```

## Step 2: Set Up Azure Foundry Client

Configure the Azure Foundry client with environment variables and authentication:

```csharp
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public static class Program
{
    private static async Task Main()
    {
        // Set up the Azure AI Project client
        var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
        var aiProjectClient = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());
```

## Step 3: Create Agent Factory Method

Implement a helper method to create Azure Foundry agents with specific instructions:

```csharp
    /// <summary>
    /// Creates a translation agent for the specified target language.
    /// </summary>
    /// <param name="targetLanguage">The target language for translation</param>
    /// <param name="aiProjectClient">The AIProjectClient to create the agent</param>
    /// <param name="model">The model to use for the agent</param>
    /// <returns>A ChatClientAgent configured for the specified language</returns>
    private static async Task<ChatClientAgent> GetTranslationAgentAsync(
        string targetLanguage,
        AIProjectClient aiProjectClient,
        string model)
    {
        string agentName = $"{targetLanguage} Translator";
        var version = await aiProjectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            agentName,
            new ProjectsAgentVersionCreationOptions(
                new DeclarativeAgentDefinition(model)
                {
                    Instructions = $"You are a translation assistant that translates the provided text to {targetLanguage}."
                }));

        return aiProjectClient.AsAIAgent(version);
    }
}
```

## Step 4: Create Specialized Azure Foundry Agents

Create three translation agents using the helper method:

```csharp
        // Create agents
        AIAgent frenchAgent = await GetTranslationAgentAsync("French", aiProjectClient, deploymentName);
        AIAgent spanishAgent = await GetTranslationAgentAsync("Spanish", aiProjectClient, deploymentName);
        AIAgent englishAgent = await GetTranslationAgentAsync("English", aiProjectClient, deploymentName);
```

## Step 5: Build the Workflow

Connect the agents in a sequential workflow using the WorkflowBuilder:

```csharp
        // Build the workflow by adding executors and connecting them
        var workflow = new WorkflowBuilder(frenchAgent)
            .AddEdge(frenchAgent, spanishAgent)
            .AddEdge(spanishAgent, englishAgent)
            .Build();
```

## Step 6: Execute with Streaming

Run the workflow with streaming to observe real-time updates from all agents:

```csharp
        // Execute the workflow
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, "Hello World!"));

        // Must send the turn token to trigger the agents.
        // The agents are wrapped as executors. When they receive messages,
        // they will cache the messages and only start processing when they receive a TurnToken.
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent executorComplete)
            {
                Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            }
        }
```

## Step 7: Resource Cleanup

Properly clean up the Azure Foundry agents after use:

```csharp
        // Cleanup the agents created for the sample.
        await aiProjectClient.AgentAdministrationClient.DeleteAgentAsync(frenchAgent.Id);
        await aiProjectClient.AgentAdministrationClient.DeleteAgentAsync(spanishAgent.Id);
        await aiProjectClient.AgentAdministrationClient.DeleteAgentAsync(englishAgent.Id);
    }
```

## How It Works

1. **Azure Foundry Client Setup**: Uses `AIProjectClient` with Azure CLI credentials for authentication
2. **Agent Creation**: Creates versioned agents on Azure Foundry with specific instructions for translation
3. **Sequential Processing**: French agent translates input first, then Spanish agent, then English agent
4. **Turn Token Pattern**: Agents cache messages and only process when they receive a `TurnToken`
5. **Streaming Updates**: `AgentResponseUpdateEvent` provides real-time token updates as agents generate responses
6. **Resource Management**: Proper cleanup of Azure Foundry agents using the Administration API

## Key Concepts

- **Azure Foundry Agent Service**: Cloud-based AI agents with advanced reasoning capabilities
- **AIProjectClient**: Client for creating and managing agents on Azure Foundry
- **WorkflowEvent**: Output events (`type="output"`) contain agent output data (`AgentResponseUpdate` for streaming, `AgentResponse` for non-streaming)
- **TurnToken**: Signal that triggers agent processing after message caching
- **Sequential Workflow**: Agents connected in a pipeline where output flows from one to the next

## Complete Implementation

For the complete working implementation of this Azure Foundry agents workflow, see the [FoundryAgent Program.cs](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/03-workflows/Agents/FoundryAgent/Program.cs) sample in the Agent Framework repository.

::: zone-end

::: zone pivot="programming-language-python"

## What You'll Build

You'll create a workflow that:

- Uses `FoundryChatClient` to create intelligent agents
- Implements a Writer agent that creates content based on prompts
- Implements a Reviewer agent that provides feedback on the content
- Connects agents in a sequential workflow pipeline
- Streams real-time updates as agents process requests

### Concepts Covered

- [Agents in Workflows](./agents-in-workflows.md)
- [Direct edges](../concepts/workflows/edges.md#direct-edges)
- [Workflow Builder](../concepts/workflows/builder-and-execution.md)

## Prerequisites

- Python 3.10 or later
- Agent Framework installed: `pip install agent-framework`
- Azure OpenAI Responses configured with proper environment variables
- Azure CLI authentication: `az login`

## Step 1: Import Required Dependencies

Start by importing the necessary components for workflows and Azure OpenAI Responses agents:

```python
import asyncio
import os

from agent_framework import AgentResponseUpdate, WorkflowBuilder
from agent_framework.foundry import FoundryChatClient
from azure.identity import AzureCliCredential
```

## Step 2: Create Azure OpenAI Responses Client

Create one shared client that you can use to construct multiple agents:

```python
async def main() -> None:
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["FOUNDRY_MODEL"],
        credential=AzureCliCredential(),
    )
```

## Step 3: Create Specialized Agents

Create two specialized agents for content creation and review:

```python
    # Create a Writer agent that generates content
    writer_agent = client.as_agent(
        name="Writer",
        instructions=(
            "You are an excellent content writer. You create new content and edit contents based on the feedback."
        ),
    )

    # Create a Reviewer agent that provides feedback
    reviewer_agent = client.as_agent(
        name="Reviewer",
        instructions=(
            "You are an excellent content reviewer. "
            "Provide actionable feedback to the writer about the provided content. "
            "Provide the feedback in the most concise manner possible."
        ),
    )
```

## Step 4: Build the Workflow

Connect the agents in a sequential workflow using the builder:

```python
        # Build the workflow with agents as executors
        workflow = WorkflowBuilder(start_executor=writer_agent).add_edge(writer_agent, reviewer_agent).build()
```

## Step 5: Execute with Streaming

Run the workflow with streaming to observe real-time updates from both agents:

```python
    last_author: str | None = None

    events = workflow.run("Create a slogan for a new electric SUV that is affordable and fun to drive.", stream=True)
    async for event in events:
        if event.type == "output" and isinstance(event.data, AgentResponseUpdate):
            update = event.data
            author = update.author_name
            if author != last_author:
                if last_author is not None:
                    print()
                print(f"{author}: {update.text}", end="", flush=True)
                last_author = author
            else:
                print(update.text, end="", flush=True)
```

## Step 6: Complete Main Function

Wrap everything in the main function with proper async execution:

```python
if __name__ == "__main__":
    asyncio.run(main())
```

## How It Works

1. **Client Setup**: Uses one `FoundryChatClient` with Azure CLI credentials for authentication.
2. **Agent Creation**: Creates Writer and Reviewer agents from the same client configuration.
3. **Sequential Processing**: Writer agent generates content first, then passes it to the Reviewer agent.
4. **Streaming Updates**: Output events (`type="output"`) with `AgentResponseUpdate` data provide real-time token updates as agents generate responses.

## Key Concepts

- **FoundryChatClient**: Shared client used to create workflow agents with consistent configuration.
- **WorkflowEvent**: Output events (`type="output"`) contain agent output data (`AgentResponseUpdate` for streaming, `AgentResponse` for non-streaming).
- **Sequential Workflow**: Agents connected in a pipeline where output flows from one to the next.

## Complete Implementation

For the complete working implementation, see [azure_ai_agents_streaming.py](https://github.com/microsoft/agent-framework/blob/main/python/samples/03-workflows/agents/azure_ai_agents_streaming.py) in the Agent Framework repository.

::: zone-end

::: zone pivot="programming-language-go"

## What You'll Build

You'll create a workflow that:

- Uses Azure OpenAI agents as workflow executors
- Implements a French translation agent
- Implements a Spanish translation agent
- Implements an English translation agent
- Connects agents in a sequential workflow pipeline
- Streams real-time updates as agents process requests

### Concepts Covered

- [Agents in Workflows](./agents-in-workflows.md)
- [Direct edges](../concepts/workflows/edges.md#direct-edges)
- [Workflow Builder](../concepts/workflows/builder-and-execution.md)

## Prerequisites

- Go 1.25 or later
- Microsoft Foundry project endpoint and model deployment configured
- Azure CLI authentication or another Azure credential source

## Step 1: Set Up Foundry Configuration

```go
endpoint := os.Getenv("FOUNDRY_PROJECT_ENDPOINT")
model := cmp.Or(os.Getenv("FOUNDRY_MODEL"), "gpt-4o-mini")

token, err := azidentity.NewDefaultAzureCredential(nil)
if err != nil {
    return err
}
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

## Step 2: Create Agent Factory Function

Create agents with specific translation instructions:

```go
newTranslationAgent := func(language string) *agent.Agent {
    return foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
        Instructions: fmt.Sprintf(
            "Translate the user's text to %s. Return only the translation.",
            language,
        ),
        Config: agent.Config{Name: language + "Agent"},
    })
}
```

## Step 3: Create Specialized Foundry Agents

```go
frenchAgent := newTranslationAgent("French")
spanishAgent := newTranslationAgent("Spanish")
englishAgent := newTranslationAgent("English")
```

## Step 4: Build the Workflow

Agents can be used as workflow executors, enabling AI-powered workflow steps.

Bind each agent as a workflow executor, then connect the executors with edges:

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/workflow/agentworkflow"
    "github.com/microsoft/agent-framework-go/message"
    "github.com/microsoft/agent-framework-go/workflow"
    "github.com/microsoft/agent-framework-go/workflow/inproc"
)

cfg := agentworkflow.Config{DisableForwardIncomingMessages: true}
french := agentworkflow.New(frenchAgent, cfg)
spanish := agentworkflow.New(spanishAgent, cfg)
english := agentworkflow.New(englishAgent, cfg)

wf, err := workflow.NewBuilder(french).
    AddEdge(french, spanish).
    AddEdge(spanish, english).
    WithOutputFrom(english).
    Build()
if err != nil {
    return err
}
```

## Step 5: Execute with Streaming

Run the workflow and enable update events with a `workflow.TurnToken`:

```go
run, err := inproc.Default.RunStreaming(ctx, wf, message.NewText("Hello World"))
if err != nil {
    return err
}
defer run.Close(ctx)

emitEvents := true
if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
    return err
}

for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }
    if out, ok := evt.(workflow.OutputEvent); ok {
        if update, ok := out.Output.(*agent.ResponseUpdate); ok {
            fmt.Printf("%s: %s\n", out.ExecutorID, update.String())
        }
    }
}
```

## How It Works

1. **Client Setup**: Uses an Azure credential with the OpenAI client.
2. **Agent Creation**: Creates specialized agents with language-specific instructions.
3. **Agent Hosting**: Uses `agentworkflow.New` to bind each agent as a workflow executor.
4. **Sequential Processing**: The French executor runs first, then Spanish, then English.
5. **Turn Token Pattern**: Hosted agents buffer messages and run when they receive a `workflow.TurnToken`.
6. **Streaming Updates**: `workflow.OutputEvent` values can contain `*agent.ResponseUpdate` outputs for real-time progress.

## Key Concepts

- **Azure OpenAI Agent**: An `agent.Agent` backed by Azure OpenAI.
- **agentworkflow.New**: Adapts an agent for use as a workflow executor.
- **workflow.TurnToken**: Signal that triggers hosted agents to process buffered messages.
- **Workflow OutputEvent**: Carries agent response updates and final workflow outputs.
- **Sequential Workflow**: Agents connected in a pipeline where output flows from one to the next.

## Complete Implementation

```go
package main

import (
    "cmp"
    "context"
    "fmt"
    "log"
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/workflow/agentworkflow"
    "github.com/microsoft/agent-framework-go/message"
    "github.com/microsoft/agent-framework-go/workflow"
    "github.com/microsoft/agent-framework-go/workflow/inproc"

    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
)

func main() {
    ctx := context.Background()
    endpoint := os.Getenv("FOUNDRY_PROJECT_ENDPOINT")
    model := cmp.Or(os.Getenv("FOUNDRY_MODEL"), "gpt-4o-mini")

    credential, err := azidentity.NewDefaultAzureCredential(nil)
    if err != nil {
        log.Fatal(err)
    }

    newTranslationAgent := func(language string) *agent.Agent {
        return foundryprovider.NewAgent(endpoint, credential, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
            Instructions: fmt.Sprintf(
                "Translate the user's text to %s. Return only the translation.",
                language,
            ),
            Config: agent.Config{Name: language + "Agent"},
        })
    }

    cfg := agentworkflow.Config{DisableForwardIncomingMessages: true}
    french := agentworkflow.New(newTranslationAgent("French"), cfg)
    spanish := agentworkflow.New(newTranslationAgent("Spanish"), cfg)
    english := agentworkflow.New(newTranslationAgent("English"), cfg)

    wf, err := workflow.NewBuilder(french).
        AddEdge(french, spanish).
        AddEdge(spanish, english).
        WithOutputFrom(english).
        Build()
    if err != nil {
        log.Fatal(err)
    }

    run, err := inproc.Default.RunStreaming(ctx, wf, message.NewText("Hello World"))
    if err != nil {
        log.Fatal(err)
    }
    defer run.Close(ctx)

    emitEvents := true
    if err := run.SendMessage(ctx, workflow.TurnToken{EmitEvents: &emitEvents}); err != nil {
        log.Fatal(err)
    }

    for evt, err := range run.WatchStream(ctx) {
        if err != nil {
            log.Fatal(err)
        }
        if out, ok := evt.(workflow.OutputEvent); ok {
            if update, ok := out.Output.(*agent.ResponseUpdate); ok {
                fmt.Printf("%s: %s\n", out.ExecutorID, update.String())
            }
        }
    }
}
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

> [!TIP]
> See the [agents in workflows sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/03-workflows/01-start-here/02_agents_in_workflows/main.go) for a complete example.

::: zone-end
## Next Steps

> [!div class="nextstepaction"]
> [Human-in-the-Loop](./human-in-the-loop.md)
