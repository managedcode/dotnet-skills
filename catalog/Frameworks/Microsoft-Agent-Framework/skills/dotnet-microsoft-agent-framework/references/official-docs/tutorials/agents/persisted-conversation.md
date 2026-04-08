---
title: Persisting and Resuming Agent Conversations
description: How to persist an agent thread to storage and reload it later
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/25/2025
ms.service: agent-framework
---

# Persisting and Resuming Agent Conversations

::: zone pivot="programming-language-csharp"

This tutorial shows how to persist an agent conversation (AgentThread) to storage and reload it later.

When hosting an agent in a service or even in a client application, you often want to maintain conversation state across multiple requests or sessions. By persisting the `AgentThread`, you can save the conversation context and reload it later.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Persisting and resuming the conversation

Create an agent and obtain a new thread that will hold the conversation state.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
     .GetChatClient("gpt-4o-mini")
     .AsAIAgent(instructions: "You are a helpful assistant.", name: "Assistant");

AgentThread thread = await agent.GetNewThreadAsync();
```

Run the agent, passing in the thread, so that the `AgentThread` includes this exchange.

```csharp
// Run the agent and append the exchange to the thread
Console.WriteLine(await agent.RunAsync("Tell me a short pirate joke.", thread));
```

Call the `Serialize` method on the thread to serialize it to a JsonElement.
It can then be converted to a string for storage and saved to a database, blob storage, or file.

```csharp
using System.IO;
using System.Text.Json;

// Serialize the thread state
string serializedJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();

// Example: save to a local file (replace with DB or blob storage in production)
string filePath = Path.Combine(Path.GetTempPath(), "agent_thread.json");
await File.WriteAllTextAsync(filePath, serializedJson);
```

Load the persisted JSON from storage and recreate the AgentThread instance from it.
The thread must be deserialized using an agent instance. This should be the
same agent type that was used to create the original thread.
This is because agents might have their own thread types and might construct threads with
additional functionality that is specific to that agent type.

```csharp
// Read persisted JSON
string loadedJson = await File.ReadAllTextAsync(filePath);
JsonElement reloaded = JsonSerializer.Deserialize<JsonElement>(loadedJson, JsonSerializerOptions.Web);

// Deserialize the thread into an AgentThread tied to the same agent type
AgentThread resumedThread = await agent.DeserializeThreadAsync(reloaded, JsonSerializerOptions.Web);
```

Use the resumed thread to continue the conversation.

```csharp
// Continue the conversation with resumed thread
Console.WriteLine(await agent.RunAsync("Now tell that joke in the voice of a pirate.", resumedThread));
```

::: zone-end
::: zone pivot="programming-language-python"

This tutorial shows how to persist an agent conversation (AgentThread) to storage and reload it later.

When hosting an agent in a service or even in a client application, you often want to maintain conversation state across multiple requests or sessions. By persisting the `AgentThread`, you can save the conversation context and reload it later.

## Prerequisites

For prerequisites and installing Python packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Persisting and resuming the conversation

Create an agent and obtain a new thread that will hold the conversation state.

```python
from azure.identity import AzureCliCredential
from agent_framework import ChatAgent
from agent_framework.azure import AzureOpenAIChatClient

agent = ChatAgent(
    chat_client=AzureOpenAIChatClient(
        endpoint="https://<myresource>.openai.azure.com",
        credential=AzureCliCredential(),
        ai_model_id="gpt-4o-mini"
    ),
    name="Assistant",
    instructions="You are a helpful assistant."
)

thread = agent.get_new_thread()
```

Run the agent, passing in the thread, so that the `AgentThread` includes this exchange.

```python
# Run the agent and append the exchange to the thread
response = await agent.run("Tell me a short pirate joke.", thread=thread)
print(response.text)
```

Call the `serialize` method on the thread to serialize it to a dictionary.
It can then be converted to JSON for storage and saved to a database, blob storage, or file.

```python
import json
import tempfile
import os

# Serialize the thread state
serialized_thread = await thread.serialize()
serialized_json = json.dumps(serialized_thread)

# Example: save to a local file (replace with DB or blob storage in production)
temp_dir = tempfile.gettempdir()
file_path = os.path.join(temp_dir, "agent_thread.json")
with open(file_path, "w") as f:
    f.write(serialized_json)
```

Load the persisted JSON from storage and recreate the AgentThread instance from it.
The thread must be deserialized using an agent instance. This should be the
same agent type that was used to create the original thread.
This is because agents might have their own thread types and might construct threads with
additional functionality that is specific to that agent type.

```python
# Read persisted JSON
with open(file_path, "r") as f:
    loaded_json = f.read()

reloaded_data = json.loads(loaded_json)

# Deserialize the thread into an AgentThread tied to the same agent type
resumed_thread = await agent.deserialize_thread(reloaded_data)
```

Use the resumed thread to continue the conversation.

```python
# Continue the conversation with resumed thread
response = await agent.run("Now tell that joke in the voice of a pirate.", thread=resumed_thread)
print(response.text)
```

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Third Party chat history storage](./third-party-chat-history-storage.md)
