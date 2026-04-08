---
title: Agent Tools
description: Learn how to use tools with Agent Framework
zone_pivot_groups: programming-languages
author: markwallace
ms.topic: reference
ms.author: markwallace
ms.date: 03/17/2026
ms.service: agent-framework
---

# Agent Tools

> [!NOTE]
> The live Learn tools surface now routes through `https://learn.microsoft.com/agent-framework/agents/tools/overview`.
> Current live docs also add a provider-support matrix and fold agent-as-tool composition into that broader tools overview.

Tooling support can vary considerably between different agent types. Some agents might allow developers to customize the agent at construction time by providing external function tools or by choosing to activate specific built-in tools that are supported by the agent. On the other hand, some custom agents might support no customization via providing external or activating built-in tools, if they already provide defined features that shouldn't be changed.

::: zone pivot="programming-language-csharp"

Therefore, the base abstraction does not provide any direct tooling support, however each agent can choose whether it accepts tooling customization at construction time.

## Tooling support with ChatClientAgent

The `ChatClientAgent` is an agent class that can be used to build agentic capabilities on top of any inference service. It comes with support for:

1. Using your own function tools with the agent
1. Using built-in tools that the underlying service might support.

> [!TIP]
> For more information on `ChatClientAgent` and information on supported services, see [Simple agents based on inference services](./agent-types/index.md#simple-agents-based-on-inference-services)

### Provide `AIFunction` instances during agent construction

There are various ways to construct a `ChatClientAgent`, for example, directly or via factory helper methods on various service clients, but all support passing tools.

```csharp
// Sample function tool.
[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

// When calling the ChatClientAgent constructor.
new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful assistant",
    tools: [AIFunctionFactory.Create(GetWeather)]);

// When using one of the helper factory methods.
openAIResponseClient.AsAIAgent(
    instructions: "You are a helpful assistant",
    tools: [AIFunctionFactory.Create(GetWeather)]);
```

### Provide `AIFunction` instances when running the agent

While the base `AIAgent` abstraction accepts `AgentRunOptions` on its run methods, subclasses of `AIAgent` can accept
subclasses of `AgentRunOptions`. This allows specific agent implementations to accept agent specific per-run options.

The underlying <xref:Microsoft.Extensions.AI.IChatClient> of the `ChatClientAgent` can be customized via the <xref:Microsoft.Extensions.AI.ChatOptions> class for any invocation.
The `ChatClientAgent` can accept a `ChatClientAgentRunOptions` which allows the caller to provide `ChatOptions` for the underlying
`IChatClient.GetResponse` method. Where any option clashes with options provided to the agent at construction time, the per run options
will take precedence.

Using this mechanism you can provide per-run tools.

```csharp
// Create the chat options class with the per-run tools.
var chatOptions = new ChatOptions()
{
    Tools = [AIFunctionFactory.Create(GetWeather)]
};
// Run the agent, with the per-run chat options.
await agent.RunAsync(
    "What is the weather like in Amsterdam?",
    options: new ChatClientAgentRunOptions(chatOptions));
```

> [!NOTE]
> Not all agents support tool calling, so providing tools per run requires providing an agent specific options class.

### Using built-in tools

Where the underlying service supports built-in tools, they can be provided using the same mechanisms as described above.

The IChatClient implementation for the underlying service should expose an `AITool` derived class that can be used to
configure the built-in tool.

For example, when creating an Azure AI Foundry Agent, you can provide a `CodeInterpreterToolDefinition` to enable the code interpreter
tool that is built into the Azure AI Foundry service.

```csharp
var agent = await azureAgentClient.CreateAIAgentAsync(
    deploymentName,
    instructions: "You are a helpful assistant",
    tools: [new CodeInterpreterToolDefinition()]);
```

::: zone-end
::: zone pivot="programming-language-python"

## Tooling support with ChatAgent

The `ChatAgent` is an agent class that can be used to build agentic capabilities on top of any inference service. It comes with support for:

1. Using your own function tools with the agent
2. Using built-in tools that the underlying service might support
3. Using hosted tools like web search and MCP (Model Context Protocol) servers

### Provide function tools during agent construction

There are various ways to construct a `ChatAgent`, either directly or via factory helper methods on various service clients. All approaches support passing tools at construction time.

```python
from typing import Annotated
from pydantic import Field
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIChatClient

# Sample function tool
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is cloudy with a high of 15°C."

# When creating a ChatAgent directly
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant",
    tools=[get_weather]  # Tools provided at construction
)

# When using factory helper methods
agent = OpenAIChatClient().as_agent(
    instructions="You are a helpful assistant",
    tools=[get_weather]
)
```

The agent will automatically use these tools whenever they're needed to answer user queries:

```python
result = await agent.run("What's the weather like in Amsterdam?")
print(result.text)  # The agent will call get_weather() function
```

### Provide function tools when running the agent

Python agents support providing tools on a per-run basis using the `tools` parameter in both `run()` and `run_stream()` methods. When both agent-level and run-level tools are provided, they are combined, with run-level tools taking precedence.

```python
# Agent created without tools
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant"
    # No tools defined here
)

# Provide tools for specific runs
result1 = await agent.run(
    "What's the weather in Seattle?",
    tools=[get_weather]  # Tool provided for this run only
)

# Use different tools for different runs
result2 = await agent.run(
    "What's the current time?",
    tools=[get_time]  # Different tool for this query
)

# Provide multiple tools for a single run
result3 = await agent.run(
    "What's the weather and time in Chicago?",
    tools=[get_weather, get_time]  # Multiple tools
)
```

This also works with streaming:

```python
async for update in agent.run_stream(
    "Tell me about the weather",
    tools=[get_weather]
):
    if update.text:
        print(update.text, end="", flush=True)
```

### Using built-in and hosted tools

The Python Agent Framework supports various built-in and hosted tools that extend agent capabilities:

#### Web Search Tool

```python
from agent_framework import HostedWebSearchTool

agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant with web search capabilities",
    tools=[
        HostedWebSearchTool(
            additional_properties={
                "user_location": {
                    "city": "Seattle",
                    "country": "US"
                }
            }
        )
    ]
)

result = await agent.run("What are the latest news about AI?")
```

#### MCP (Model Context Protocol) Tools

```python
from agent_framework import HostedMCPTool

agent = ChatAgent(
    chat_client=AzureAIAgentClient(async_credential=credential),
    instructions="You are a documentation assistant",
    tools=[
        HostedMCPTool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp"
        )
    ]
)

result = await agent.run("How do I create an Azure storage account?")
```

#### File Search Tool

```python
from agent_framework import HostedFileSearchTool, HostedVectorStoreContent

agent = ChatAgent(
    chat_client=AzureAIAgentClient(async_credential=credential),
    instructions="You are a document search assistant",
    tools=[
        HostedFileSearchTool(
            inputs=[
                HostedVectorStoreContent(vector_store_id="vs_123")
            ],
            max_results=10
        )
    ]
)

result = await agent.run("Find information about quarterly reports")
```

#### Code Interpreter Tool

```python
from agent_framework import HostedCodeInterpreterTool

agent = ChatAgent(
    chat_client=AzureAIAgentClient(async_credential=credential),
    instructions="You are a data analysis assistant",
    tools=[HostedCodeInterpreterTool()]
)

result = await agent.run("Analyze this dataset and create a visualization")
```

### Mixing agent-level and run-level tools

You can combine tools defined at the agent level with tools provided at runtime:

```python
# Agent with base tools
agent = ChatAgent(
    chat_client=OpenAIChatClient(),
    instructions="You are a helpful assistant",
    tools=[get_time]  # Base tool available for all runs
)

# This run has access to both get_time (agent-level) and get_weather (run-level)
result = await agent.run(
    "What's the weather and time in New York?",
    tools=[get_weather]  # Additional tool for this run
)
```

> [!NOTE]
> Tool support varies by service provider. Some services like Azure AI support hosted tools natively, while others might require different approaches. Always check your service provider's documentation for specific tool capabilities.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agent Retrieval Augmented Generation](./agent-rag.md)
