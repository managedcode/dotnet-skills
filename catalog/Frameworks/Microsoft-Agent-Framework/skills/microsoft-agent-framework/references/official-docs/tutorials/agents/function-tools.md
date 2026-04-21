---
title: Using function tools with an agent
description: Learn how to use function tools with an agent
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 03/17/2026
ms.service: agent-framework
---

# Using function tools with an agent

> [!NOTE]
> The live Learn page for this legacy tutorial path now resolves to the canonical tools article at `https://learn.microsoft.com/agent-framework/agents/tools/function-tools`.
> This local file keeps the historical path so existing references inside the skill catalog remain stable.

This tutorial step shows you how to use function tools with an agent, where the agent is built on the Azure OpenAI Chat Completion service.

::: zone pivot="programming-language-csharp"

> [!IMPORTANT]
> Not all agent types support function tools. Some might only support custom built-in tools, without allowing the caller to provide their own functions. This step uses a `ChatClientAgent`, which does support function tools.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create the agent with function tools

Function tools are just custom code that you want the agent to be able to call when needed.
You can turn any C# method into a function tool, by using the `AIFunctionFactory.Create` method to create an `AIFunction` instance from the method.

If you need to provide additional descriptions about the function or its parameters to the agent, so that it can more accurately choose between different functions, you can use the `System.ComponentModel.DescriptionAttribute` attribute on the method and its parameters.

Here is an example of a simple function tool that fakes getting the weather for a given location.
It is decorated with description attributes to provide additional descriptions about itself and its location parameter to the agent.

```csharp
using System.ComponentModel;

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";
```

When creating the agent, you can now provide the function tool to the agent, by passing a list of tools to the `AsAIAgent` method.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new DefaultAzureCredential())
     .GetChatClient("gpt-4o-mini")
     .AsAIAgent(instructions: "You are a helpful assistant", tools: [AIFunctionFactory.Create(GetWeather)]);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production.
> Prefer a specific credential such as `ManagedIdentityCredential` when the hosting environment is known.

Now you can just run the agent as normal, and the agent will be able to call the `GetWeather` function tool when needed.

```csharp
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

## Current addenda from the latest canonical page

- Use `FunctionInvocationContext` for runtime-only values that should stay out of the model-visible schema.
- Use declaration-only tools only when the implementation lives outside Agent Framework and your app will supply the result later.
- When multiple tools share service clients or mutable implementation state, group them behind bound methods on a class instead of exposing that state as model input.

::: zone-end
::: zone pivot="programming-language-python"

> [!IMPORTANT]
> Not all agent types support function tools. Some might only support custom built-in tools, without allowing the caller to provide their own functions. This step uses agents created via chat clients, which do support function tools.

## Prerequisites

For prerequisites and installing Python packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create the agent with function tools

Function tools are just custom code that you want the agent to be able to call when needed.
You can turn any Python function into a function tool by passing it to the agent's `tools` parameter when creating the agent.

If you need to provide additional descriptions about the function or its parameters to the agent, so that it can more accurately choose between different functions, you can use Python's type annotations with `Annotated` and Pydantic's `Field` to provide descriptions.

Here is an example of a simple function tool that fakes getting the weather for a given location.
It uses type annotations to provide additional descriptions about the function and its location parameter to the agent.

```python
from typing import Annotated
from pydantic import Field

def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    """Get the weather for a given location."""
    return f"The weather in {location} is cloudy with a high of 15°C."
```

You can also use the `ai_function` decorator to explicitly specify the function's name and description:

```python
from typing import Annotated
from pydantic import Field
from agent_framework import ai_function

@ai_function(name="weather_tool", description="Retrieves weather information for any location")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    return f"The weather in {location} is cloudy with a high of 15°C."
```

If you don't specify the `name` and `description` parameters in the `ai_function` decorator, the framework will automatically use the function's name and docstring as fallbacks.

When creating the agent, you can now provide the function tool to the agent, by passing it to the `tools` parameter.

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
    instructions="You are a helpful assistant",
    tools=get_weather
)
```

Now you can just run the agent as normal, and the agent will be able to call the `get_weather` function tool when needed.

```python
async def main():
    result = await agent.run("What is the weather like in Amsterdam?")
    print(result.text)

asyncio.run(main())
```

## Create a class with multiple function tools

You can also create a class that contains multiple function tools as methods.
This can be useful for organizing related functions together or when you want to pass state between them.

```python

class WeatherTools:
    def __init__(self):
        self.last_location = None

    def get_weather(
        self,
        location: Annotated[str, Field(description="The location to get the weather for.")],
    ) -> str:
        """Get the weather for a given location."""
        return f"The weather in {location} is cloudy with a high of 15°C."

    def get_weather_details(self) -> int:
        """Get the detailed weather for the last requested location."""
        if self.last_location is None:
            return "No location specified yet."
        return f"The detailed weather in {self.last_location} is cloudy with a high of 15°C, low of 7°C, and 60% humidity."

```

When creating the agent, you can now provide all the methods of the class as functions:

```python
tools = WeatherTools()
agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
    instructions="You are a helpful assistant",
    tools=[tools.get_weather, tools.get_weather_details]
)
```

You can also decorate the functions with the same `ai_function` decorator as before.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Using function tools with human in the loop approvals](./function-tools-approvals.md)
