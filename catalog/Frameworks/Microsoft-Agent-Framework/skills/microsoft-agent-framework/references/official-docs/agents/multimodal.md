---
title: Using images with an agent
description: Learn how to use images with an agent
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 07/01/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

  | Section                    | C# | Python | Go | Notes |
  |----------------------------|:--:|:------:|:--:|-------|
  | Create agent               | ✅ |   ✅   | ✅ |       |
  | Message with image URL     | ✅ |   ✅   | ✅ |       |
  | Load image from file       | ❌ |   ✅   | ✅ |       |
  | Run agent                  | ✅ |   ✅   | ✅ |       |
-->

# Using images with an agent

This tutorial shows you how to use images with an agent, allowing the agent to analyze and respond to image content.

::: zone pivot="programming-language-csharp"

## Passing images to the agent

You can send images to an agent by creating a `ChatMessage` that includes both text and image content. The agent can then analyze the image and respond accordingly.

First, create an `AIAgent` that is able to analyze images.

```csharp
AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
    .AsAIAgent(
        model: "gpt-4o",
        name: "VisionAgent",
        instructions: "You are a helpful agent that can analyze images");
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

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

You can send images to an agent by creating a `Message` that includes both text and image content. The agent can then analyze the image and respond accordingly.

First, create an agent that is able to analyze images.

```python
import asyncio
import os
from agent_framework.openai import OpenAIChatCompletionClient
from azure.identity import AzureCliCredential

agent = OpenAIChatCompletionClient(
    model=os.environ["AZURE_OPENAI_CHAT_COMPLETION_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
).as_agent(
    name="VisionAgent",
    instructions="You are a helpful agent that can analyze images"
)
```

Next, create a `Message` that contains both a text prompt and an image URL. Use `Content.from_text()` for the text and `Content.from_uri()` for the image.

```python
from agent_framework import Message, Content

message = Message(
    role="user",
    contents=[
        Content.from_text(text="What do you see in this image?"),
        Content.from_uri(
            uri="https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg",
            media_type="image/jpeg"
        )
    ]
)
```

You can also load an image from your local file system using `Content.from_data()`:

```python
from agent_framework import Message, Content

# Load image from local file
with open("path/to/your/image.jpg", "rb") as f:
    image_bytes = f.read()

message = Message(
    role="user",
    contents=[
        Content.from_text(text="What do you see in this image?"),
        Content.from_data(
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

::: zone pivot="programming-language-go"

## Passing images to the agent

You can send images to an agent by creating a `message` that includes both text and image content. The agent can then analyze the image and respond accordingly.

First, create an agent that is able to analyze images.

```go
import (
    "context"
    "fmt"
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"
    "github.com/microsoft/agent-framework-go/message"

    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
)

token, err := azidentity.NewDefaultAzureCredential(nil)
if err != nil {
    panic(err)
}

a := foundryprovider.NewAgent(
    os.Getenv("FOUNDRY_PROJECT_ENDPOINT"),
    token,
    foundryprovider.ModelDeployment(os.Getenv("FOUNDRY_MODEL")),
    foundryprovider.AgentConfig{
        Instructions: "You are a helpful agent that can analyze images",
        Config: agent.Config{
            Name: "VisionAgent",
        },
    },
)
```

> [!WARNING]
> `azidentity.NewDefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential, such as `azidentity.NewManagedIdentityCredential`, to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

Next, create a message that contains both a text prompt and an image URL. Use `message.TextContent` for the text and `message.URIContent` for the image.

```go
msg := message.New(
    &message.TextContent{Text: "What do you see in this image?"},
    &message.URIContent{
        URI:       "https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg",
        MediaType: "image/jpeg",
    },
)
```

You can also load an image from your local file system using `message.DataContent`:

```go
import "encoding/base64"

imageBytes, err := os.ReadFile("path/to/your/image.jpg")
if err != nil {
    panic(err)
}

msg := message.New(
    &message.TextContent{Text: "What do you see in this image?"},
    &message.DataContent{
        Data:      base64.StdEncoding.EncodeToString(imageBytes),
        MediaType: "image/jpeg",
    },
)
```

Run the agent with the message. You can use streaming to receive the response as it is generated.

```go
ctx := context.Background()
resp, err := a.RunMessage(ctx, msg).Collect()
fmt.Println(resp.Text(), err)
```

This will print the agent's analysis of the image to the console.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Structured Outputs](structured-outputs.md)
