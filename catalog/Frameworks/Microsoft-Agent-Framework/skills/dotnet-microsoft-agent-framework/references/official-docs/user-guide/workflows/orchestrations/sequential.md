---
title: Microsoft Agent Framework Workflows Orchestrations - Sequential
description: In-depth look at Sequential Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---

# Microsoft Agent Framework Workflows Orchestrations - Sequential

In sequential orchestration, agents are organized in a pipeline. Each agent processes the task in turn, passing its output to the next agent in the sequence. This is ideal for workflows where each step builds upon the previous one, such as document review, data processing pipelines, or multi-stage reasoning.

<p align="center">
    <img src="../resources/images/orchestration-sequential.png" alt="Sequential Orchestration">
</p>

> [!IMPORTANT]
> The full conversation history from previous agents is passed to the next agent in the sequence. Each agent can see all prior messages, allowing for context-aware processing.

## What You'll Learn

- How to create a sequential pipeline of agents
- How to chain agents where each builds upon the previous output
- How to mix agents with custom executors for specialized tasks
- How to track the conversation flow through the pipeline

## Define Your Agents

::: zone pivot="programming-language-csharp"

In sequential orchestration, agents are organized in a pipeline where each agent processes the task in turn, passing output to the next agent in the sequence.

## Set Up the Azure OpenAI Client

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

// 1) Set up the Azure OpenAI client
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

Create specialized agents that will work in sequence:

```csharp
// 2) Helper method to create translation agents
static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient chatClient) =>
    new(chatClient,
        $"You are a translation assistant who only responds in {targetLanguage}. Respond to any " +
        $"input by outputting the name of the input language and then translating the input to {targetLanguage}.");

// Create translation agents for sequential processing
var translationAgents = (from lang in (string[])["French", "Spanish", "English"]
                         select GetTranslationAgent(lang, client));
```

## Set Up the Sequential Orchestration

Build the workflow using `AgentWorkflowBuilder`:

```csharp
// 3) Build sequential workflow
var workflow = AgentWorkflowBuilder.BuildSequential(translationAgents);
```

## Run the Sequential Workflow

Execute the workflow and process the events:

```csharp
// 4) Run the workflow
var messages = new List<ChatMessage> { new(ChatRole.User, "Hello, world!") };

StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

List<ChatMessage> result = new();
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
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

// Display final result
foreach (var message in result)
{
    Console.WriteLine($"{message.Role}: {message.Content}");
}
```

## Sample Output

```plaintext
French_Translation: User: Hello, world!
French_Translation: Assistant: English detected. Bonjour, le monde !
Spanish_Translation: Assistant: French detected. ¡Hola, mundo!
English_Translation: Assistant: Spanish detected. Hello, world!
```

## Key Concepts

- **Sequential Processing**: Each agent processes the output of the previous agent in order
- **AgentWorkflowBuilder.BuildSequential()**: Creates a pipeline workflow from a collection of agents
- **ChatClientAgent**: Represents an agent backed by a chat client with specific instructions
- **StreamingRun**: Provides real-time execution with event streaming capabilities
- **Event Handling**: Monitor agent progress through `AgentResponseUpdateEvent` and completion through `WorkflowOutputEvent`

::: zone-end

::: zone pivot="programming-language-python"

In sequential orchestration, each agent processes the task in turn, with output flowing from one to the next. Start by defining agents for a two-stage process:

```python
from agent_framework.azure import AzureChatClient
from azure.identity import AzureCliCredential

# 1) Create agents using AzureChatClient
chat_client = AzureChatClient(credential=AzureCliCredential())

writer = chat_client.as_agent(
    instructions=(
        "You are a concise copywriter. Provide a single, punchy marketing sentence based on the prompt."
    ),
    name="writer",
)

reviewer = chat_client.as_agent(
    instructions=(
        "You are a thoughtful reviewer. Give brief feedback on the previous assistant message."
    ),
    name="reviewer",
)
```

## Set Up the Sequential Orchestration

The `SequentialBuilder` class creates a pipeline where agents process tasks in order. Each agent sees the full conversation history and adds their response:

```python
from agent_framework import SequentialBuilder

# 2) Build sequential workflow: writer -> reviewer
workflow = SequentialBuilder().participants([writer, reviewer]).build()
```

## Run the Sequential Workflow

Execute the workflow and collect the final conversation showing each agent's contribution:

```python
from agent_framework import ChatMessage, WorkflowOutputEvent

# 3) Run and print final conversation
output_evt: WorkflowOutputEvent | None = None
async for event in workflow.run_stream("Write a tagline for a budget-friendly eBike."):
    if isinstance(event, WorkflowOutputEvent):
        output_evt = event

if output_evt:
    print("===== Final Conversation =====")
    messages: list[ChatMessage] | Any = output_evt.data
    for i, msg in enumerate(messages, start=1):
        name = msg.author_name or ("assistant" if msg.role == Role.ASSISTANT else "user")
        print(f"{'-' * 60}\n{i:02d} [{name}]\n{msg.text}")
```

## Sample Output

```plaintext
===== Final Conversation =====
------------------------------------------------------------
01 [user]
Write a tagline for a budget-friendly eBike.
------------------------------------------------------------
02 [writer]
Ride farther, spend less—your affordable eBike adventure starts here.
------------------------------------------------------------
03 [reviewer]
This tagline clearly communicates affordability and the benefit of extended travel, making it
appealing to budget-conscious consumers. It has a friendly and motivating tone, though it could
be slightly shorter for more punch. Overall, a strong and effective suggestion!
```

## Advanced: Mixing Agents with Custom Executors

Sequential orchestration supports mixing agents with custom executors for specialized processing. This is useful when you need custom logic that doesn't require an LLM:

### Define a Custom Executor

```python
from agent_framework import Executor, WorkflowContext, handler
from agent_framework import ChatMessage, Role

class Summarizer(Executor):
    """Simple summarizer: consumes full conversation and appends an assistant summary."""

    @handler
    async def summarize(
        self,
        conversation: list[ChatMessage],
        ctx: WorkflowContext[list[ChatMessage]]
    ) -> None:
        users = sum(1 for m in conversation if m.role == Role.USER)
        assistants = sum(1 for m in conversation if m.role == Role.ASSISTANT)
        summary = ChatMessage(
            role=Role.ASSISTANT,
            text=f"Summary -> users:{users} assistants:{assistants}"
        )
        await ctx.send_message(list(conversation) + [summary])
```

### Build a Mixed Sequential Workflow

```python
# Create a content agent
content = chat_client.as_agent(
    instructions="Produce a concise paragraph answering the user's request.",
    name="content",
)

# Build sequential workflow: content -> summarizer
summarizer = Summarizer(id="summarizer")
workflow = SequentialBuilder().participants([content, summarizer]).build()
```

### Sample Output with Custom Executor

```plaintext
------------------------------------------------------------
01 [user]
Explain the benefits of budget eBikes for commuters.
------------------------------------------------------------
02 [content]
Budget eBikes offer commuters an affordable, eco-friendly alternative to cars and public transport.
Their electric assistance reduces physical strain and allows riders to cover longer distances quickly,
minimizing travel time and fatigue. Budget models are low-cost to maintain and operate, making them accessible
for a wider range of people. Additionally, eBikes help reduce traffic congestion and carbon emissions,
supporting greener urban environments. Overall, budget eBikes provide cost-effective, efficient, and
sustainable transportation for daily commuting needs.
------------------------------------------------------------
03 [assistant]
Summary -> users:1 assistants:1
```

## Key Concepts

- **Shared Context**: Each participant receives the full conversation history, including all previous messages
- **Order Matters**: Agents execute strictly in the order specified in the `participants()` list
- **Flexible Participants**: You can mix agents and custom executors in any order
- **Conversation Flow**: Each agent/executor appends to the conversation, building a complete dialogue

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Group Chat Orchestration](./group-chat.md)
