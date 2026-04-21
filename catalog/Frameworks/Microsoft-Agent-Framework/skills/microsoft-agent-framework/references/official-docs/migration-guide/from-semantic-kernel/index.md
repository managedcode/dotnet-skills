---
title: Semantic Kernel to Microsoft Agent Framework Migration Guide
description: Learn how to migrate from the Semantic Kernel Agent Framework to Microsoft Agent Framework
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: reference
ms.author: westey
ms.date: 11/11/2025
ms.service: agent-framework
---

# Semantic Kernel to Agent Framework Migration Guide

## Benefits of Microsoft Agent Framework

- **Simplified API**: Reduced complexity and boilerplate code.
- **Better Performance**: Optimized object creation and memory usage.
- **Unified Interface**: Consistent patterns across different AI providers.
- **Enhanced Developer Experience**: More intuitive and discoverable APIs.

::: zone pivot="programming-language-csharp"

The following sections summarize the key differences between Semantic Kernel Agent Framework and Microsoft Agent Framework to help you migrate your code.

## 1. Namespace Updates

### Semantic Kernel

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
```

### Agent Framework

Agent Framework namespaces are under `Microsoft.Agents.AI`.
Agent Framework uses the core AI message and content types from <xref:Microsoft.Extensions.AI> for communication between components.

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
```

## 2. Agent Creation Simplification

### Semantic Kernel

Every agent in Semantic Kernel depends on a `Kernel` instance and has
an empty `Kernel` if not provided.

```csharp
 Kernel kernel = Kernel
    .AddOpenAIChatClient(modelId, apiKey)
    .Build();

 ChatCompletionAgent agent = new() { Instructions = ParrotInstructions, Kernel = kernel };
```

Azure AI Foundry requires an agent resource to be created in the cloud before creating a local agent class that uses it.

```csharp
PersistentAgentsClient azureAgentClient = AzureAIAgent.CreateAgentsClient(azureEndpoint, new AzureCliCredential());

PersistentAgent definition = await azureAgentClient.Administration.CreateAgentAsync(
    deploymentName,
    instructions: ParrotInstructions);

AzureAIAgent agent = new(definition, azureAgentClient);
 ```

### Agent Framework

Agent creation in Agent Framework is made simpler with extensions provided by all main providers.

```csharp
AIAgent openAIAgent = chatClient.AsAIAgent(instructions: ParrotInstructions);
AIAgent azureFoundryAgent = await persistentAgentsClient.CreateAIAgentAsync(instructions: ParrotInstructions);
AIAgent openAIAssistantAgent = await assistantClient.CreateAIAgentAsync(instructions: ParrotInstructions);
```

Additionally, for hosted agent providers you can also use the `GetAIAgent` method to retrieve an agent from an existing hosted agent.

```csharp
AIAgent azureFoundryAgent = await persistentAgentsClient.GetAIAgentAsync(agentId);
```

## 3. Agent Thread Creation

### Semantic Kernel

The caller has to know the thread type and create it manually.

```csharp
// Create a thread for the agent conversation.
AgentThread thread = new OpenAIAssistantAgentThread(this.AssistantClient);
AgentThread thread = new AzureAIAgentThread(this.Client);
AgentThread thread = new OpenAIResponseAgentThread(this.Client);
```

### Agent Framework

The agent is responsible for creating the thread.

```csharp
// New.
AgentThread thread = await agent.GetNewThreadAsync();
```

## 4. Hosted Agent Thread Cleanup

This case applies exclusively to a few AI providers that still provide hosted threads.

### Semantic Kernel

Threads have a `self` deletion method.

OpenAI Assistants Provider:

```csharp
await thread.DeleteAsync();
```

### Agent Framework

> [!NOTE]
> OpenAI Responses introduced a new conversation model that simplifies how conversations are handled. This change simplifies hosted thread management compared to the now deprecated OpenAI Assistants model. For more information, see the [OpenAI Assistants migration guide](https://platform.openai.com/docs/assistants/migration).

Agent Framework doesn't have a thread deletion API in the `AgentThread` type as not all providers support hosted threads or thread deletion. This design will become more common as more providers shift to responses-based architectures.

If you require thread deletion and the provider allows it, the caller **should** keep track of the created threads and delete them later when necessary via the provider's SDK.

OpenAI Assistants Provider:

```csharp
await assistantClient.DeleteThreadAsync(thread.ConversationId);
```

## 5. Tool Registration

### Semantic Kernel

To expose a function as a tool, you must:

1. Decorate the function with a `[KernelFunction]` attribute.
1. Have a `Plugin` class or use the `KernelPluginFactory` to wrap the function.
1. Have a `Kernel` to add your plugin to.
1. Pass the `Kernel` to the agent.

```csharp
KernelFunction function = KernelFunctionFactory.CreateFromMethod(GetWeather);
KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions("KernelPluginName", [function]);
Kernel kernel = ... // Create kernel
kernel.Plugins.Add(plugin);

ChatCompletionAgent agent = new() { Kernel = kernel, ... };
```

### Agent Framework

In Agent Framework, in a single call you can register tools directly in the agent creation process.

```csharp
AIAgent agent = chatClient.AsAIAgent(tools: [AIFunctionFactory.Create(GetWeather)]);
```

## 6. Agent Non-Streaming Invocation

Key differences can be seen in the method names from `Invoke` to `Run`, return types, and parameters `AgentRunOptions`.

### Semantic Kernel

The Non-Streaming uses a streaming pattern `IAsyncEnumerable<AgentResponseItem<ChatMessageContent>>` for returning multiple agent messages.

```csharp
await foreach (AgentResponseItem<ChatMessageContent> result in agent.InvokeAsync(userInput, thread, agentOptions))
{
    Console.WriteLine(result.Message);
}
```

### Agent Framework

The Non-Streaming returns a single `AgentResponse` with the agent response that can contain multiple messages.
The text result of the run is available in `AgentResponse.Text` or `AgentResponse.ToString()`.
All messages created as part of the response are returned in the `AgentResponse.Messages` list.
This might include tool call messages, function results, reasoning updates, and final results.

```csharp
AgentResponse agentResponse = await agent.RunAsync(userInput, thread);
```

## 7. Agent Streaming Invocation

The key differences are in the method names from `Invoke` to `Run`, return types, and parameters `AgentRunOptions`.

### Semantic Kernel

```csharp
await foreach (StreamingChatMessageContent update in agent.InvokeStreamingAsync(userInput, thread))
{
    Console.Write(update);
}
```

### Agent Framework

Agent Framework has a similar streaming API pattern, with the key difference being that it returns `AgentResponseUpdate` objects that include more agent-related information per update.

All updates produced by any service underlying the AIAgent are returned. The textual result of the agent is available by concatenating the `AgentResponse.Text` values.

```csharp
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, thread))
{
    Console.Write(update); // Update is ToString() friendly
}
```

## 8. Tool Function Signatures

**Problem**: Semantic Kernel plugin methods need `[KernelFunction]` attributes.

```csharp
public class MenuPlugin
{
    [KernelFunction] // Required.
    public static MenuItem[] GetMenu() => ...;
}
```

**Solution**: Agent Framework can use methods directly without attributes.

```csharp
public class MenuTools
{
    [Description("Get menu items")] // Optional description.
    public static MenuItem[] GetMenu() => ...;
}
```

## 9. Options Configuration

**Problem**: Complex options setup in Semantic Kernel.

```csharp
OpenAIPromptExecutionSettings settings = new() { MaxTokens = 1000 };
AgentInvokeOptions options = new() { KernelArguments = new(settings) };
```

**Solution**: Simplified options in Agent Framework.

```csharp
ChatClientAgentRunOptions options = new(new() { MaxOutputTokens = 1000 });
```

> [!IMPORTANT]
> This example shows passing implementation-specific options to a `ChatClientAgent`. Not all `AIAgents` support `ChatClientAgentRunOptions`. `ChatClientAgent` is provided to build agents based on underlying inference services, and therefore supports inference options like `MaxOutputTokens`.

## 10. Dependency Injection

### Semantic Kernel

A `Kernel` registration is required in the service container to be able to create an agent,
as every agent abstraction needs to be initialized with a `Kernel` property.

Semantic Kernel uses the `Agent` type as the base abstraction class for agents.

```csharp
services.AddKernel().AddProvider(...);
serviceContainer.AddKeyedSingleton<SemanticKernel.Agents.Agent>(
    TutorName,
    (sp, key) =>
        new ChatCompletionAgent()
        {
            // Passing the kernel is required.
            Kernel = sp.GetRequiredService<Kernel>(),
        });
```

### Agent Framework

Agent Framework provides the `AIAgent` type as the base abstraction class.

```csharp
services.AddKeyedSingleton<AIAgent>(() => client.AsAIAgent(...));
```

## 11. Agent Type Consolidation

### Semantic Kernel

Semantic Kernel provides specific agent classes for various services, for example:

- `ChatCompletionAgent` for use with chat-completion-based inference services.
- `OpenAIAssistantAgent` for use with the OpenAI Assistants service.
- `AzureAIAgent` for use with the Azure AI Foundry Agents service.

### Agent Framework

Agent Framework supports all the mentioned services via a single agent type, `ChatClientAgent`.

`ChatClientAgent` can be used to build agents using any underlying service that provides an SDK that implements the `IChatClient` interface.

::: zone-end
::: zone pivot="programming-language-python"

## Key differences

Here is a summary of the key differences between the Semantic Kernel Agent Framework and Microsoft Agent Framework to help you migrate your code.

## 1. Package and import updates

### Semantic Kernel

Semantic Kernel packages are installed as `semantic-kernel` and imported as `semantic_kernel`. The package also has a number of `extras` that you can install to install the different dependencies for different AI providers and other features.

```python
from semantic_kernel import Kernel
from semantic_kernel.agents import ChatCompletionAgent
```

### Agent Framework

Agent Framework package is installed as `agent-framework` and imported as `agent_framework`.
Agent Framework is built up differently, it has a core package `agent-framework-core` that contains the core functionality, and then there are multiple packages that rely on that core package, such as `agent-framework-azure-ai`, `agent-framework-mem0`, `agent-framework-copilotstudio`, etc. When you run `pip install agent-framework --pre` it will install the core package and *all* packages, so that you can get started with all the features quickly. When you are ready to reduce the number of packages because you know what you need, you can install only the packages you need, so for instance if you only plan to use Azure AI Foundry and Mem0 you can install only those two packages: `pip install agent-framework-azure-ai agent-framework-mem0 --pre`, `agent-framework-core` is a dependency to those two, so will automatically be installed.

Even though the packages are split up, the imports are all from `agent_framework`, or it's modules. So for instance to import the client for Azure AI Foundry you would do:

```python
from agent_framework.azure import AzureAIAgentClient
```

Many of the most commonly used types are imported directly from `agent_framework`:

```python
from agent_framework import ChatMessage, ChatAgent
```

## 2. Agent Type Consolidation

### Semantic Kernel

Semantic Kernel provides specific agent classes for various services, for example, ChatCompletionAgent, AzureAIAgent, OpenAIAssistantAgent, etc. See [Agent types in Semantic Kernel](/semantic-kernel/Frameworks/agent/agent-types/azure-ai-agent).

### Agent Framework

In Agent Framework, the majority of agents are built using the `ChatAgent` which can be used with all the `ChatClient` based services, such as Azure AI Foundry, OpenAI ChatCompletion, and OpenAI Responses. There are two additional agents: `CopilotStudioAgent` for use with Copilot Studio and `A2AAgent` for use with A2A.

All the built-in agents are based on the BaseAgent (`from agent_framework import BaseAgent`). And all agents are consistent with the `AgentProtocol` (`from agent_framework import AgentProtocol`) interface.

## 3. Agent Creation Simplification

### Semantic Kernel

Every agent in Semantic Kernel depends on a `Kernel` instance and will have
an empty `Kernel` if not provided.

```python
from semantic_kernel.agents import ChatCompletionAgent
from semantic_kernel.connectors.ai.open_ai import OpenAIChatCompletion

agent = ChatCompletionAgent(
    service=OpenAIChatCompletion(),
    name="Support",
    instructions="Answer in one sentence.",
)
```

### Agent Framework

Agent creation in Agent Framework can be done in two ways, directly:

```python
from agent_framework.azure import AzureAIAgentClient
from agent_framework import ChatMessage, ChatAgent

agent = ChatAgent(chat_client=AzureAIAgentClient(credential=AzureCliCredential()), instructions="You are a helpful assistant")
```

Or, with the convenience methods provided by chat clients:

```python
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential
agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(instructions="You are a helpful assistant")
```

The direct method exposes all possible parameters you can set for your agent. While the convenience method has a subset, you can still pass in the same set of parameters, because it calls the direct method internally.

## 4. Agent Thread Creation

### Semantic Kernel

The caller has to know the thread type and create it manually.

```python
from semantic_kernel.agents import ChatHistoryAgentThread

thread = ChatHistoryAgentThread()
```

### Agent Framework

The agent can be asked to create a new thread for you.

```python
agent = ...
thread = agent.get_new_thread()
```

A thread is then created in one of three ways:

1. If the agent has a `thread_id` (or `conversation_id` or something similar) set, it will create a thread in the underlying service with that ID. Once a thread has a `service_thread_id`, you can no longer use it to store messages in memory. This only applies to agents that have a service-side thread concept. such as Azure AI Foundry Agents and OpenAI Assistants.
2. If the agent has a `chat_message_store_factory` set, it will use that factory to create a message store and use that to create an in-memory thread. It can then no longer be used with a agent with the `store` parameter set to `True`.
3. If neither of the previous settings is set, it's considered `uninitialized` and depending on how it is used, it will either become a in-memory thread or a service thread.

### Agent Framework

> [!NOTE]
> OpenAI Responses introduced a new conversation model that simplifies how conversations are handled. This simplifies hosted thread management compared to the now deprecated OpenAI Assistants model. For more information see the [OpenAI Assistants migration guide](https://platform.openai.com/docs/assistants/migration).

Agent Framework doesn't have a thread deletion API in the `AgentThread` type as not all providers support hosted threads or thread deletion and this will become more common as more providers shift to responses based architectures.

If you require thread deletion and the provider allows this, the caller **should** keep track of the created threads and delete them later when necessary via the provider's sdk.

OpenAI Assistants Provider:

```python
# OpenAI Assistants threads have self-deletion method in Semantic Kernel
await thread.delete_async()
```

## 5. Tool Registration

### Semantic Kernel

To expose a function as a tool, you must:

1. Decorate the function with a `@kernel_function` decorator.
1. Have a `Plugin` class or use the kernel plugin factory to wrap the function.
1. Have a `Kernel` to add your plugin to.
1. Pass the `Kernel` to the agent.

```python
from semantic_kernel.functions import kernel_function

class SpecialsPlugin:
    @kernel_function(name="specials", description="List daily specials")
    def specials(self) -> str:
        return "Clam chowder, Cobb salad, Chai tea"

agent = ChatCompletionAgent(
    service=OpenAIChatCompletion(),
    name="Host",
    instructions="Answer menu questions accurately.",
    plugins=[SpecialsPlugin()],
)
```

### Agent Framework

In a single call, you can register tools directly in the agent creation process. Agent Framework doesn't have the concept of a plugin to wrap multiple functions, but you can still do that if desired.

The simplest way to create a tool is just to create a Python function:

```python
def get_weather(location: str) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny."

agent = chat_client.as_agent(tools=get_weather)
```

> [!NOTE]
> The `tools` parameter is present on both the agent creation, the `run` and `run_stream` methods, as well as the `get_response` and `get_streaming_response` methods, it allows you to supply tools both as a list or a single function.

The name of the function will then become the name of the tool, and the docstring will become the description of the tool, you can also add a description to the parameters:

```python
from typing import Annotated

def get_weather(location: Annotated[str, "The location to get the weather for."]) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny."
```

Finally, you can use the decorator to further customize the name and description of the tool:

```python
from typing import Annotated
from agent_framework import ai_function

@ai_function(name="weather_tool", description="Retrieves weather information for any location")
def get_weather(location: Annotated[str, "The location to get the weather for."])
    """Get the weather for a given location."""
    return f"The weather in {location} is sunny."
```

This also works when you create a class with multiple tools as methods.

When creating the agent, you can now provide the function tool to the agent by passing it to the `tools` parameter.

```python
class Plugin:

    def __init__(self, initial_state: str):
        self.state: list[str] = [initial_state]

    def get_weather(self, location: Annotated[str, "The location to get the weather for."]) -> str:
        """Get the weather for a given location."""
        self.state.append(f"Requested weather for {location}. ")
        return f"The weather in {location} is sunny."

    def get_weather_details(self, location: Annotated[str, "The location to get the weather details for."]) -> str:
        """Get detailed weather for a given location."""
        self.state.append(f"Requested detailed weather for {location}. ")
        return f"The weather in {location} is sunny with a high of 25°C and a low of 15°C."

plugin = Plugin("Initial state")
agent = chat_client.as_agent(tools=[plugin.get_weather, plugin.get_weather_details])

... # use the agent

print("Plugin state:", plugin.state)
```

> [!NOTE]
> The functions within the class can also be decorated with `@ai_function` to customize the name and description of the tools.

This mechanism is also useful for tools that need additional input that cannot be supplied by the LLM, such as connections, secrets, etc.

### Compatibility: Using KernelFunction as Agent Framework tools

If you have existing Semantic Kernel code with `KernelFunction` instances (either from prompts or from methods), you can convert them to Agent Framework tools using the `.as_agent_framework_tool` method.

> [!IMPORTANT]
> This feature requires `semantic-kernel` version 1.38 or higher.

#### Using KernelFunction from a prompt template

```python
from semantic_kernel import Kernel
from semantic_kernel.functions import KernelFunctionFromPrompt
from semantic_kernel.connectors.ai.open_ai import OpenAIChatCompletion, OpenAIChatPromptExecutionSettings
from semantic_kernel.prompt_template import KernelPromptTemplate, PromptTemplateConfig
from agent_framework.openai import OpenAIResponsesClient

# Create a kernel with services and plugins
kernel = Kernel()
# will get the api_key and model_id from the environment
kernel.add_service(OpenAIChatCompletion(service_id="default"))

# Create a function from a prompt template that uses plugin functions
function_definition = """
Today is: {{time.date}}
Current time is: {{time.time}}

Answer to the following questions using JSON syntax, including the data used.
Is it morning, afternoon, evening, or night (morning/afternoon/evening/night)?
Is it weekend time (weekend/not weekend)?
"""

prompt_template_config = PromptTemplateConfig(template=function_definition)
prompt_template = KernelPromptTemplate(prompt_template_config=prompt_template_config)

# Create a KernelFunction from the prompt
kernel_function = KernelFunctionFromPrompt(
    description="Determine the kind of day based on the current time and date.",
    plugin_name="TimePlugin",
    prompt_execution_settings=OpenAIChatPromptExecutionSettings(service_id="default", max_tokens=100),
    function_name="kind_of_day",
    prompt_template=prompt_template,
)

# Convert the KernelFunction to an Agent Framework tool
agent_tool = kernel_function.as_agent_framework_tool(kernel=kernel)

# Use the tool with an Agent Framework agent
agent = OpenAIResponsesClient(model_id="gpt-4o").as_agent(tools=agent_tool)
response = await agent.run("What kind of day is it?")
print(response.text)
```

#### Using KernelFunction from a method

```python
from semantic_kernel.functions import kernel_function
from agent_framework.openai import OpenAIResponsesClient

# Create a plugin class with kernel functions
@kernel_function(name="get_weather", description="Get the weather for a location")
def get_weather(self, location: str) -> str:
    return f"The weather in {location} is sunny."

# Get the KernelFunction and convert it to an Agent Framework tool
agent_tool = get_weather.as_agent_framework_tool()

# Use the tool with an Agent Framework agent
agent = OpenAIResponsesClient(model_id="gpt-4o").as_agent(tools=agent_tool)
response = await agent.run("What's the weather in Seattle?")
print(response.text)
```

#### Using VectorStore with create_search_function

You can also use Semantic Kernel's VectorStore integrations with Agent Framework. The `create_search_function` method from a vector store collection returns a `KernelFunction` that can be converted to an Agent Framework tool.

```python
from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import OpenAITextEmbedding
from semantic_kernel.connectors.azure_ai_search import AzureAISearchCollection
from semantic_kernel.functions import KernelParameterMetadata
from agent_framework.openai import OpenAIResponsesClient

# Define your data model
class HotelSampleClass:
    HotelId: str
    HotelName: str
    Description: str
    # ... other fields

# Create an Azure AI Search collection
collection = AzureAISearchCollection[str, HotelSampleClass](
    record_type=HotelSampleClass,
    embedding_generator=OpenAITextEmbedding()
)

async with collection:
    await collection.ensure_collection_exists()
    # Load your records into the collection
    # await collection.upsert(records)

    # Create a search function from the collection
    search_function = collection.create_search_function(
        description="A hotel search engine, allows searching for hotels in specific cities.",
        search_type="keyword_hybrid",
        filter=lambda x: x.Address.Country == "USA",
        parameters=[
            KernelParameterMetadata(
                name="query",
                description="What to search for.",
                type="str",
                is_required=True,
                type_object=str,
            ),
            KernelParameterMetadata(
                name="city",
                description="The city that you want to search for a hotel in.",
                type="str",
                type_object=str,
            ),
            KernelParameterMetadata(
                name="top",
                description="Number of results to return.",
                type="int",
                default_value=5,
                type_object=int,
            ),
        ],
        string_mapper=lambda x: f"(hotel_id: {x.record.HotelId}) {x.record.HotelName} - {x.record.Description}",
    )

    # Convert the search function to an Agent Framework tool
    search_tool = search_function.as_agent_framework_tool()

    # Use the tool with an Agent Framework agent
    agent = OpenAIResponsesClient(model_id="gpt-4o").as_agent(
        instructions="You are a travel agent that helps people find hotels.",
        tools=search_tool
    )
    response = await agent.run("Find me a hotel in Seattle")
    print(response.text)
```

This pattern works with any Semantic Kernel VectorStore connector (Azure AI Search, Qdrant, Pinecone, etc.), allowing you to leverage your existing vector search infrastructure with Agent Framework agents.

This compatibility layer allows you to gradually migrate your code from Semantic Kernel to Agent Framework, reusing your existing `KernelFunction` implementations while taking advantage of Agent Framework's simplified agent creation and execution patterns.

## 6. Agent Non-Streaming Invocation

Key differences can be seen in the method names from `invoke` to `run`, return types (for example, `AgentResponse`) and parameters.

### Semantic Kernel

The Non-Streaming invoke uses an async iterator pattern for returning multiple agent messages.

```python
async for response in agent.invoke(
    messages=user_input,
    thread=thread,
):
    print(f"# {response.role}: {response}")
    thread = response.thread
```

And there was a convenience method to get the final response:

```python
response = await agent.get_response(messages="How do I reset my bike tire?", thread=thread)
print(f"# {response.role}: {response}")
```

### Agent Framework

The Non-Streaming run returns a single `AgentResponse` with the agent response that can contain multiple messages.
The text result of the run is available in `response.text` or `str(response)`.
All messages created as part of the response are returned in the `response.messages` list.
This might include tool call messages, function results, reasoning updates and final results.

```python
agent = ...

response = await agent.run(user_input, thread)
print("Agent response:", response.text)

```

## 7. Agent Streaming Invocation

Key differences in the method names from `invoke` to `run_stream`, return types (`AgentResponseUpdate`) and parameters.

### Semantic Kernel

```python
async for update in agent.invoke_stream(
    messages="Draft a 2 sentence blurb.",
    thread=thread,
):
    if update.message:
        print(update.message.content, end="", flush=True)
```

### Agent Framework

Similar streaming API pattern with the key difference being that it returns `AgentResponseUpdate` objects including more agent related information per update.

All contents produced by any service underlying the Agent are returned. The final result of the agent is available by combining the `update` values into a single response.

```python
from agent_framework import AgentResponse
agent = ...
updates = []
async for update in agent.run_stream(user_input, thread):
    updates.append(update)
    print(update.text)

full_response = AgentResponse.from_agent_response_updates(updates)
print("Full agent response:", full_response.text)
```

You can even do that directly:

```python
from agent_framework import AgentResponse
agent = ...
full_response = AgentResponse.from_agent_response_generator(agent.run_stream(user_input, thread))
print("Full agent response:", full_response.text)
```

## 8. Options Configuration

**Problem**: Complex options setup in Semantic Kernel

```python
from semantic_kernel.connectors.ai.open_ai import OpenAIPromptExecutionSettings

settings = OpenAIPromptExecutionSettings(max_tokens=1000)
arguments = KernelArguments(settings)

response = await agent.get_response(user_input, thread=thread, arguments=arguments)
```

**Solution**: Simplified TypedDict-based options in Agent Framework

Agent Framework uses a TypedDict-based options system for `ChatClients` and `ChatAgents`. Options are passed via a single `options` parameter as a typed dictionary, with provider-specific TypedDict classes (like `OpenAIChatOptions`) for full IDE autocomplete and type checking.

```python
from agent_framework.openai import OpenAIChatClient

client = OpenAIChatClient()

# Set default options at agent creation
agent = client.as_agent(
    instructions="You are a helpful assistant.",
    default_options={
        "max_tokens": 1000,
        "temperature": 0.7,
    }
)

# Override options per call
response = await agent.run(
    user_input,
    thread,
    options={
        "max_tokens": 500,
        "frequency_penalty": 0.5,
    }
)
```

> [!NOTE]
> The `tools` and `instructions` parameters remain as direct keyword arguments on agent creation and `run()` methods, and are not passed via the `options` dictionary.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Quickstart Guide](../../tutorials/quick-start.md)
