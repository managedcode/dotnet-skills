---
title: File Search
description: Learn how to use the File Search tool with Agent Framework agents.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# File Search

File Search enables agents to search through uploaded files to find relevant information. This tool is particularly useful for building agents that can answer questions about documents, analyze file contents, and extract information.

> [!NOTE]
> File Search availability depends on the underlying agent provider. See [Providers Overview](../../integrations/by-component/model-providers/index.md) for provider-specific support.

:::zone pivot="programming-language-csharp"

The following example shows how to create an agent with the File Search tool:

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Requires: dotnet add package Microsoft.Agents.AI.Foundry --prerelease
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

// Create an agent with the file search hosted tool
// Provide vector store IDs containing your uploaded documents
AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a helpful assistant that searches through files to find information.",
        tools: [new FileSearchToolDefinition(vectorStoreIds: ["<your-vector-store-id>"])]);

Console.WriteLine(await agent.RunAsync("What does the document say about today's weather?"));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

:::zone-end

:::zone pivot="programming-language-python"

The following example shows how to create an agent with the File Search tool and sample documents:

### File Search Tool Example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio

from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient

"""
OpenAI Responses Client with File Search Example

This sample demonstrates using get_file_search_tool() with OpenAI Responses Client
for direct document-based question answering and information retrieval.
"""

# Helper functions


async def create_vector_store(client: OpenAIChatClient) -> tuple[str, str]:
    """Create a vector store with sample documents."""
    file = await client.client.files.create(
        file=("todays_weather.txt", b"The weather today is sunny with a high of 75F."), purpose="user_data"
    )
    vector_store = await client.client.vector_stores.create(
        name="knowledge_base",
        expires_after={"anchor": "last_active_at", "days": 1},
    )
    result = await client.client.vector_stores.files.create_and_poll(vector_store_id=vector_store.id, file_id=file.id)
    if result.last_error is not None:
        raise Exception(f"Vector store file processing failed with status: {result.last_error.message}")

    return file.id, vector_store.id


async def delete_vector_store(client: OpenAIChatClient, file_id: str, vector_store_id: str) -> None:
    """Delete the vector store after using it."""
    await client.client.vector_stores.delete(vector_store_id=vector_store_id)
    await client.client.files.delete(file_id=file_id)


async def main() -> None:
    client = OpenAIChatClient()

    message = "What is the weather today? Do a file search to find the answer."

    stream = False
    print(f"User: {message}")
    file_id, vector_store_id = await create_vector_store(client)

    agent = Agent(
        client=client,
        instructions="You are a helpful assistant that can search through files to find information.",
        tools=[client.get_file_search_tool(vector_store_ids=[vector_store_id])],
    )

    if stream:
        print("Assistant: ", end="")
        async for chunk in agent.run(message, stream=True):
            if chunk.text:
                print(chunk.text, end="")
        print("")
    else:
        response = await agent.run(message)
        print(f"Assistant: {response}")
    await delete_vector_store(client, file_id, vector_store_id)


if __name__ == "__main__":
    asyncio.run(main())
```

:::zone-end

:::zone pivot="programming-language-go"
## File search

The `hostedtool.FileSearch` type enables server-side file search when using a provider that supports it (such as OpenAI Responses).

```go
import "github.com/microsoft/agent-framework-go/tool/hostedtool"

fileSearch := &hostedtool.FileSearch{
    MaximumResultCount: 10,
}

a := openaiprovider.NewResponsesAgent(client, openaiprovider.AgentConfig{
    Model: deployment,
    Config: agent.Config{
        Tools: []tool.Tool{fileSearch},
    },
})
```

> [!NOTE]
> File search is a hosted tool — the search is performed by the AI service, not locally.

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [Web Search](./web-search.md)
