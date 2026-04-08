---
title: Create and run an agent with Agent Framework
description: Learn how to create and run an AI agent using Agent Framework
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/15/2025
ms.service: agent-framework
---

# Create and run an agent with Agent Framework

::: zone pivot="programming-language-csharp"

This tutorial shows you how to create and run an agent with Agent Framework, based on the Azure OpenAI Chat Completion service.

> [!IMPORTANT]
> Agent Framework supports many different types of agents. This tutorial uses an agent based on a Chat Completion service, but all other agent types are run in the same way. For more information on other agent types and how to construct them, see the [Agent Framework user guide](../../user-guide/overview.md).

## Prerequisites

Before you begin, ensure you have the following prerequisites:

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- [Azure OpenAI service endpoint and deployment configured](/azure/ai-foundry/openai/how-to/create-resource)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated (for Azure credential authentication)](/cli/azure/authenticate-azure-cli)
- [User has the `Cognitive Services OpenAI User` or `Cognitive Services OpenAI Contributor` roles for the Azure OpenAI resource.](/azure/ai-foundry/openai/how-to/role-based-access-control)

> [!NOTE]
> Microsoft Agent Framework is supported with all actively supported versions of .NET. For the purposes of this sample, we recommend the .NET 8 SDK or a later version.

> [!IMPORTANT]
> This tutorial uses Azure OpenAI for the Chat Completion service, but you can use any inference service that provides a <xref:Microsoft.Extensions.AI.IChatClient> implementation.

## Install NuGet packages

To use Microsoft Agent Framework with Azure OpenAI, you need to install the following NuGet packages:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Create the agent

- First, create a client for Azure OpenAI by providing the Azure OpenAI endpoint and using the same login as you used when authenticating with the Azure CLI in the [Prerequisites](#prerequisites) step.
- Then, get a chat client for communicating with the chat completion service, where you also specify the specific model deployment to use. Use one of the deployments that you created in the [Prerequisites](#prerequisites) step.
- Finally, create the agent, providing instructions and a name for the agent.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
        .GetChatClient("gpt-4o-mini")
        .AsAIAgent(instructions: "You are good at telling jokes.", name: "Joker");
```

## Running the agent

To run the agent, call the `RunAsync` method on the agent instance, providing the user input.
The agent will return an `AgentResponse` object, and calling `.ToString()` or `.Text` on this response object, provides the text result from the agent.

```csharp
Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

Sample output:

```text
Why did the pirate go to school?

Because he wanted to improve his "arrr-ticulation"! 🏴‍☠️
```

## Running the agent with streaming

To run the agent with streaming, call the `RunStreamingAsync` method on the agent instance, providing the user input.
The agent will return a stream `AgentResponseUpdate` objects, and calling `.ToString()` or `.Text` on each update object provides the part of the text result contained in that update.

```csharp
await foreach (var update in agent.RunStreamingAsync("Tell me a joke about a pirate."))
{
    Console.WriteLine(update);
}
```

Sample output:

```text
Why
 did
 the
 pirate
 go
 to
 school
?


To
 improve
 his
 "
ar
rrrr
rr
tic
ulation
!"
```

## Running the agent with ChatMessages

Instead of a simple string, you can also provide one or more `ChatMessage` objects to the `RunAsync` and `RunStreamingAsync` methods.

Here is an example with a single user message:

```csharp
ChatMessage message = new(ChatRole.User, [
    new TextContent("Tell me a joke about this image?"),
    new UriContent("https://upload.wikimedia.org/wikipedia/commons/1/11/Joseph_Grimaldi.jpg", "image/jpeg")
]);

Console.WriteLine(await agent.RunAsync(message));
```

Sample output:

```text
Why did the clown bring a bottle of sparkling water to the show?

Because he wanted to make a splash!
```

Here is an example with a system and user message:

```csharp
ChatMessage systemMessage = new(
    ChatRole.System,
    """
    If the user asks you to tell a joke, refuse to do so, explaining that you are not a clown.
    Offer the user an interesting fact instead.
    """);
ChatMessage userMessage = new(ChatRole.User, "Tell me a joke about a pirate.");

Console.WriteLine(await agent.RunAsync([systemMessage, userMessage]));
```

Sample output:

```text
I'm not a clown, but I can share an interesting fact! Did you know that pirates often revised the Jolly Roger flag? Depending on the pirate captain, it could feature different symbols like skulls, bones, or hourglasses, each representing their unique approach to piracy.
```

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows you how to create and run an agent with Agent Framework, based on the Azure OpenAI Chat Completion service.

> [!IMPORTANT]
> Agent Framework supports many different types of agents. This tutorial uses an agent based on a Chat Completion service, but all other agent types are run in the same way. For more information on other agent types and how to construct them, see the [Agent Framework user guide](../../user-guide/overview.md).

## Prerequisites

Before you begin, ensure you have the following prerequisites:

- [Python 3.10 or later](https://www.python.org/downloads/)
- [Azure OpenAI service endpoint and deployment configured](/azure/ai-foundry/openai/how-to/create-resource)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated (for Azure credential authentication)](/cli/azure/authenticate-azure-cli)
- [User has the `Cognitive Services OpenAI User` or `Cognitive Services OpenAI Contributor` roles for the Azure OpenAI resource.](/azure/ai-foundry/openai/how-to/role-based-access-control)

> [!IMPORTANT]
> This tutorial uses Azure OpenAI for the Chat Completion service, but you can use any inference service that is compatible with Agent Framework's chat client protocol.

## Install Python packages

To use Microsoft Agent Framework with Azure OpenAI, you need to install the following Python packages:

```bash
pip install agent-framework --pre
```

## Create the agent

- First, create a chat client for communicating with Azure OpenAI and use the same login as you used when authenticating with the Azure CLI in the [Prerequisites](#prerequisites) step.
- Then, create the agent, providing instructions and a name for the agent.

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
    instructions="You are good at telling jokes.",
    name="Joker"
)
```

## Running the agent

To run the agent, call the `run` method on the agent instance, providing the user input.
The agent will return a response object, and accessing the `.text` property provides the text result from the agent.

```python
async def main():
    result = await agent.run("Tell me a joke about a pirate.")
    print(result.text)

asyncio.run(main())
```

## Running the agent with streaming

To run the agent with streaming, call the `run_stream` method on the agent instance, providing the user input.
The agent will stream a list of update objects, and accessing the `.text` property on each update object provides the part of the text result contained in that update.

```python
async def main():
    async for update in agent.run_stream("Tell me a joke about a pirate."):
        if update.text:
            print(update.text, end="", flush=True)
    print()  # New line after streaming is complete

asyncio.run(main())
```

## Running the agent with a ChatMessage

Instead of a simple string, you can also provide one or more `ChatMessage` objects to the `run` and `run_stream` methods.

```python
from agent_framework import ChatMessage, TextContent, UriContent, Role

message = ChatMessage(
    role=Role.USER,
    contents=[
        TextContent(text="Tell me a joke about this image?"),
        UriContent(uri="https://samplesite.org/clown.jpg", media_type="image/jpeg")
    ]
)

async def main():
    result = await agent.run(message)
    print(result.text)

asyncio.run(main())
```

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Using images with an agent](./images.md)
