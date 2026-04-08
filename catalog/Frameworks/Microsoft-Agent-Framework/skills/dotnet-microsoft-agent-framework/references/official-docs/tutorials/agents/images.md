---
title: Using images with an agent
description: Learn how to use images with an agent
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/24/2025
ms.service: agent-framework
---

# Using images with an agent

This tutorial shows you how to use images with an agent, allowing the agent to analyze and respond to image content.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

::: zone pivot="programming-language-csharp"

## Passing images to the agent

You can send images to an agent by creating a `ChatMessage` that includes both text and image content. The agent can then analyze the image and respond accordingly.

First, create an `AIAgent` that is able to analyze images.

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
    .GetChatClient("gpt-4o")
    .AsAIAgent(
        name: "VisionAgent",
        instructions: "You are a helpful agent that can analyze images");
```

Next, create a `ChatMessage` that contains both a text prompt and an image URL. Use `TextContent` for the text and `UriContent` for the image.

```csharp
ChatMessage message = new(ChatRole.User, [
    new TextContent("What do you see in this image?"),
    new UriContent("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", "image/jpeg")
]);
```

Run the agent with the message. You can use streaming to receive the response as it is generated.

```csharp
Console.WriteLine(await agent.RunAsync(message));
```

This will print the agent's analysis of the image to the console.

::: zone-end
::: zone pivot="programming-language-python"

## Passing images to the agent

You can send images to an agent by creating a `ChatMessage` that includes both text and image content. The agent can then analyze the image and respond accordingly.

First, create an agent that is able to analyze images.

```python
import asyncio
from agent_framework.azure import AzureOpenAIChatClient
from azure.identity import AzureCliCredential

agent = AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
    name="VisionAgent",
    instructions="You are a helpful agent that can analyze images"
)
```

Next, create a `ChatMessage` that contains both a text prompt and an image URL. Use `TextContent` for the text and `UriContent` for the image.

```python
from agent_framework import ChatMessage, TextContent, UriContent, Role

message = ChatMessage(
    role=Role.USER,
    contents=[
        TextContent(text="What do you see in this image?"),
        UriContent(
            uri="https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg",
            media_type="image/jpeg"
        )
    ]
)
```

You can also load an image from your local file system using `DataContent`:

```python
from agent_framework import ChatMessage, TextContent, DataContent, Role

# Load image from local file
with open("path/to/your/image.jpg", "rb") as f:
    image_bytes = f.read()

message = ChatMessage(
    role=Role.USER,
    contents=[
        TextContent(text="What do you see in this image?"),
        DataContent(
            data=image_bytes,
            media_type="image/jpeg"
        )
    ]
)
```

Run the agent with the message. You can use streaming to receive the response as it is generated.

```python
async def main():
    result = await agent.run(message)
    print(result.text)

asyncio.run(main())
```

This will print the agent's analysis of the image to the console.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Having a multi-turn conversation with an agent](./multi-turn-conversation.md)
