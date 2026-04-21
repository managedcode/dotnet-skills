---
title: Microsoft Agent Framework Workflows Orchestrations - Concurrent
description: In-depth look at Concurrent Orchestrations in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/12/2025
ms.service: agent-framework
---


# Microsoft Agent Framework Workflows Orchestrations - Concurrent

Concurrent orchestration enables multiple agents to work on the same task in parallel. Each agent processes the input independently, and their results are collected and aggregated. This approach is well-suited for scenarios where diverse perspectives or solutions are valuable, such as brainstorming, ensemble reasoning, or voting systems.

<p align="center">
    <img src="../resources/images/orchestration-concurrent.png" alt="Concurrent Orchestration"/>
</p>

## What You'll Learn

- How to define multiple agents with different expertise
- How to orchestrate these agents to work concurrently on a single task
- How to collect and process the results

::: zone pivot="programming-language-csharp"

In concurrent orchestration, multiple agents work on the same task simultaneously and independently, providing diverse perspectives on the same input.

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

## Define Your Agents

Create multiple specialized agents that will work on the same task concurrently:

```csharp
// 2) Helper method to create translation agents
static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient chatClient) =>
    new(chatClient,
        $"You are a translation assistant who only responds in {targetLanguage}. Respond to any " +
        $"input by outputting the name of the input language and then translating the input to {targetLanguage}.");

// Create translation agents for concurrent processing
var translationAgents = (from lang in (string[])["French", "Spanish", "English"]
                         select GetTranslationAgent(lang, client));
```

## Set Up the Concurrent Orchestration

Build the workflow using `AgentWorkflowBuilder` to run agents in parallel:

```csharp
// 3) Build concurrent workflow
var workflow = AgentWorkflowBuilder.BuildConcurrent(translationAgents);
```

## Run the Concurrent Workflow and Collect Results

Execute the workflow and process events from all agents running simultaneously:

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

// Display aggregated results from all agents
Console.WriteLine("===== Final Aggregated Results =====");
foreach (var message in result)
{
    Console.WriteLine($"{message.Role}: {message.Content}");
}
```

## Sample Output

```plaintext
French_Agent: English detected. Bonjour, le monde !
Spanish_Agent: English detected. ¡Hola, mundo!
English_Agent: English detected. Hello, world!

===== Final Aggregated Results =====
User: Hello, world!
Assistant: English detected. Bonjour, le monde !
Assistant: English detected. ¡Hola, mundo!
Assistant: English detected. Hello, world!
```

## Key Concepts

- **Parallel Execution**: All agents process the input simultaneously and independently
- **AgentWorkflowBuilder.BuildConcurrent()**: Creates a concurrent workflow from a collection of agents
- **Automatic Aggregation**: Results from all agents are automatically collected into the final result
- **Event Streaming**: Real-time monitoring of agent progress through `AgentResponseUpdateEvent`
- **Diverse Perspectives**: Each agent brings its unique expertise to the same problem

::: zone-end

::: zone pivot="programming-language-python"

Agents are specialized entities that can process tasks. The following code defines three agents: a research expert, a marketing expert, and a legal expert.

```python
from agent_framework.azure import AzureChatClient

# 1) Create three domain agents using AzureChatClient
chat_client = AzureChatClient(credential=AzureCliCredential())

researcher = chat_client.as_agent(
    instructions=(
        "You're an expert market and product researcher. Given a prompt, provide concise, factual insights,"
        " opportunities, and risks."
    ),
    name="researcher",
)

marketer = chat_client.as_agent(
    instructions=(
        "You're a creative marketing strategist. Craft compelling value propositions and target messaging"
        " aligned to the prompt."
    ),
    name="marketer",
)

legal = chat_client.as_agent(
    instructions=(
        "You're a cautious legal/compliance reviewer. Highlight constraints, disclaimers, and policy concerns"
        " based on the prompt."
    ),
    name="legal",
)
```

## Set Up the Concurrent Orchestration

The `ConcurrentBuilder` class allows you to construct a workflow to run multiple agents in parallel. You pass the list of agents as participants.

```python
from agent_framework import ConcurrentBuilder

# 2) Build a concurrent workflow
# Participants are either Agents (type of AgentProtocol) or Executors
workflow = ConcurrentBuilder().participants([researcher, marketer, legal]).build()
```

## Run the Concurrent Workflow and Collect the Results

```python
from agent_framework import ChatMessage, WorkflowOutputEvent

# 3) Run with a single prompt, stream progress, and pretty-print the final combined messages
output_evt: WorkflowOutputEvent  | None = None
async for event in workflow.run_stream("We are launching a new budget-friendly electric bike for urban commuters."):
    if isinstance(event, WorkflowOutputEvent):
        output_evt = event

if output_evt:
    print("===== Final Aggregated Conversation (messages) =====")
    messages: list[ChatMessage] | Any = output_evt.data
    for i, msg in enumerate(messages, start=1):
        name = msg.author_name if msg.author_name else "user"
        print(f"{'-' * 60}\n\n{i:02d} [{name}]:\n{msg.text}")
```

## Sample Output

```plaintext
Sample Output:

    ===== Final Aggregated Conversation (messages) =====
    ------------------------------------------------------------

    01 [user]:
    We are launching a new budget-friendly electric bike for urban commuters.
    ------------------------------------------------------------

    02 [researcher]:
    **Insights:**

    - **Target Demographic:** Urban commuters seeking affordable, eco-friendly transport;
        likely to include students, young professionals, and price-sensitive urban residents.
    - **Market Trends:** E-bike sales are growing globally, with increasing urbanization,
        higher fuel costs, and sustainability concerns driving adoption.
    - **Competitive Landscape:** Key competitors include brands like Rad Power Bikes, Aventon,
        Lectric, and domestic budget-focused manufacturers in North America, Europe, and Asia.
    - **Feature Expectations:** Customers expect reliability, ease-of-use, theft protection,
        lightweight design, sufficient battery range for daily city commutes (typically 25-40 miles),
        and low-maintenance components.

    **Opportunities:**

    - **First-time Buyers:** Capture newcomers to e-biking by emphasizing affordability, ease of
        operation, and cost savings vs. public transit/car ownership.
    ...
    ------------------------------------------------------------

    03 [marketer]:
    **Value Proposition:**
    "Empowering your city commute: Our new electric bike combines affordability, reliability, and
        sustainable design—helping you conquer urban journeys without breaking the bank."

    **Target Messaging:**

    *For Young Professionals:*
    ...
    ------------------------------------------------------------

    04 [legal]:
    **Constraints, Disclaimers, & Policy Concerns for Launching a Budget-Friendly Electric Bike for Urban Commuters:**

    **1. Regulatory Compliance**
    - Verify that the electric bike meets all applicable federal, state, and local regulations
        regarding e-bike classification, speed limits, power output, and safety features.
    - Ensure necessary certifications (for example, UL certification for batteries, CE markings if sold internationally) are obtained.

    **2. Product Safety**
    - Include consumer safety warnings regarding use, battery handling, charging protocols, and age restrictions.
```

## Advanced: Custom Agent Executors

Concurrent orchestration supports custom executors that wrap agents with additional logic. This is useful when you need more control over how agents are initialized and how they process requests:

### Define Custom Agent Executors

```python
from agent_framework import (
    AgentExecutorRequest,
    AgentExecutorResponse,
    ChatAgent,
    Executor,
    WorkflowContext,
    handler,
)

class ResearcherExec(Executor):
    agent: ChatAgent

    def __init__(self, chat_client: AzureChatClient, id: str = "researcher"):
        agent = chat_client.as_agent(
            instructions=(
                "You're an expert market and product researcher. Given a prompt, provide concise, factual insights,"
                " opportunities, and risks."
            ),
            name=id,
        )
        super().__init__(agent=agent, id=id)

    @handler
    async def run(self, request: AgentExecutorRequest, ctx: WorkflowContext[AgentExecutorResponse]) -> None:
        response = await self.agent.run(request.messages)
        full_conversation = list(request.messages) + list(response.messages)
        await ctx.send_message(AgentExecutorResponse(self.id, response, full_conversation=full_conversation))

class MarketerExec(Executor):
    agent: ChatAgent

    def __init__(self, chat_client: AzureChatClient, id: str = "marketer"):
        agent = chat_client.as_agent(
            instructions=(
                "You're a creative marketing strategist. Craft compelling value propositions and target messaging"
                " aligned to the prompt."
            ),
            name=id,
        )
        super().__init__(agent=agent, id=id)

    @handler
    async def run(self, request: AgentExecutorRequest, ctx: WorkflowContext[AgentExecutorResponse]) -> None:
        response = await self.agent.run(request.messages)
        full_conversation = list(request.messages) + list(response.messages)
        await ctx.send_message(AgentExecutorResponse(self.id, response, full_conversation=full_conversation))
```

### Build a Workflow with Custom Executors

```python
chat_client = AzureChatClient(credential=AzureCliCredential())

researcher = ResearcherExec(chat_client)
marketer = MarketerExec(chat_client)
legal = LegalExec(chat_client)

workflow = ConcurrentBuilder().participants([researcher, marketer, legal]).build()
```

## Advanced: Custom Aggregator

By default, concurrent orchestration aggregates all agent responses into a list of messages. You can override this behavior with a custom aggregator that processes the results in a specific way:

### Define a Custom Aggregator

```python
# Define a custom aggregator callback that uses the chat client to summarize
async def summarize_results(results: list[Any]) -> str:
    # Extract one final assistant message per agent
    expert_sections: list[str] = []
    for r in results:
        try:
            messages = getattr(r.agent_run_response, "messages", [])
            final_text = messages[-1].text if messages and hasattr(messages[-1], "text") else "(no content)"
            expert_sections.append(f"{getattr(r, 'executor_id', 'expert')}:\n{final_text}")
        except Exception as e:
            expert_sections.append(f"{getattr(r, 'executor_id', 'expert')}: (error: {type(e).__name__}: {e})")

    # Ask the model to synthesize a concise summary of the experts' outputs
    system_msg = ChatMessage(
        Role.SYSTEM,
        text=(
            "You are a helpful assistant that consolidates multiple domain expert outputs "
            "into one cohesive, concise summary with clear takeaways. Keep it under 200 words."
        ),
    )
    user_msg = ChatMessage(Role.USER, text="\n\n".join(expert_sections))

    response = await chat_client.get_response([system_msg, user_msg])
    # Return the model's final assistant text as the completion result
    return response.messages[-1].text if response.messages else ""
```

### Build a Workflow with Custom Aggregator

```python
workflow = (
    ConcurrentBuilder()
    .participants([researcher, marketer, legal])
    .with_aggregator(summarize_results)
    .build()
)

output_evt: WorkflowOutputEvent | None = None
async for event in workflow.run_stream("We are launching a new budget-friendly electric bike for urban commuters."):
    if isinstance(event, WorkflowOutputEvent):
        output_evt = event

if output_evt:
    print("===== Final Consolidated Output =====")
    print(output_evt.data)
```

### Sample Output with Custom Aggregator

```plaintext
===== Final Consolidated Output =====
Urban e-bike demand is rising rapidly due to eco-awareness, urban congestion, and high fuel costs,
with market growth projected at a ~10% CAGR through 2030. Key customer concerns are affordability,
easy maintenance, convenient charging, compact design, and theft protection. Differentiation opportunities
include integrating smart features (GPS, app connectivity), offering subscription or leasing options, and
developing portable, space-saving designs. Partnering with local governments and bike shops can boost visibility.

Risks include price wars eroding margins, regulatory hurdles, battery quality concerns, and heightened expectations
for after-sales support. Accurate, substantiated product claims and transparent marketing (with range disclaimers)
are essential. All e-bikes must comply with local and federal regulations on speed, wattage, safety certification,
and labeling. Clear warranty, safety instructions (especially regarding batteries), and inclusive, accessible
marketing are required. For connected features, data privacy policies and user consents are mandatory.

Effective messaging should target young professionals, students, eco-conscious commuters, and first-time buyers,
emphasizing affordability, convenience, and sustainability. Slogan suggestion: "Charge Ahead—City Commutes Made
Affordable." Legal review in each target market, compliance vetting, and robust customer support policies are
critical before launch.
```

## Key Concepts

- **Parallel Execution**: All agents work on the task simultaneously and independently
- **Result Aggregation**: Results are collected and can be processed by either the default or custom aggregator
- **Diverse Perspectives**: Each agent brings its unique expertise to the same problem
- **Flexible Participants**: You can use agents directly or wrap them in custom executors
- **Custom Processing**: Override the default aggregator to synthesize results in domain-specific ways

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Sequential Orchestration](./sequential.md)
