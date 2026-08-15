---
title: Producing Structured Outputs with agents
description: Learn how to use structured outputs with an agent
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 07/01/2026
ms.service: agent-framework
---

# Producing Structured Outputs with Agents

::: zone pivot="programming-language-csharp"

This tutorial step shows you how to produce structured outputs with an agent, where the agent is built on the Azure OpenAI Chat Completion service.

> [!IMPORTANT]
> Not all agent types support structured outputs natively. The `ChatClientAgent` supports structured outputs when used with compatible chat clients.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](../concepts/agents/running-agents.md) step in this tutorial.

## Define a type for structured outputs

First, define a type that represents the structure of the output you want from the agent.

```csharp
public class PersonInfo
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Occupation { get; set; }
}
```

## Create the agent

Create a `ChatClientAgent` using the Azure AI Projects Client.

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(
            model: "gpt-4o-mini",
            name: "HelpfulAssistant",
            instructions: "You are a helpful assistant.");
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

## Structured outputs with RunAsync\<T\>

The `RunAsync<T>` method is available on the `AIAgent` base class. It accepts a generic type parameter that specifies the structured outputs type.
This approach is applicable when the structured outputs type is known at compile time and a typed result instance is needed. It supports primitives, arrays, and complex types.

```csharp
AgentResponse<PersonInfo> response = await agent.RunAsync<PersonInfo>("Please provide information about John Smith, who is a 35-year-old software engineer.");

Console.WriteLine($"Name: {response.Result.Name}, Age: {response.Result.Age}, Occupation: {response.Result.Occupation}");
```

## Structured outputs with ResponseFormat

Structured outputs can be configured by setting the `ResponseFormat` property on `AgentRunOptions` at invocation time, or at agent initialization time for agents that support it, such as `ChatClientAgent` and Foundry Agent.

This approach is applicable when:

- The structured outputs type is not known at compile time.
- The schema is represented as raw JSON.
- Structured outputs can only be configured at agent creation time.
- Only the raw JSON text is needed without deserialization.
- Inter-agent collaboration is used.

Various options for `ResponseFormat` are available:

- A built-in <xref:Microsoft.Extensions.AI.ChatResponseFormat.Text?displayProperty=nameWithType> property: The response will be plain text.
- A built-in <xref:Microsoft.Extensions.AI.ChatResponseFormat.Json?displayProperty=nameWithType> property: The response will be a JSON object without any particular schema.
- A custom <xref:Microsoft.Extensions.AI.ChatResponseFormatJson> instance: The response will be a JSON object that conforms to a specific schema.

> [!NOTE]
> Primitives and arrays are not supported by the `ResponseFormat` approach. If you need to work with primitives or arrays, use the `RunAsync<T>` approach or create a wrapper type.
>
> ```csharp
> // Instead of using List<string> directly, create a wrapper type:
> public class MovieListWrapper
> {
>     public List<string> Movies { get; set; }
> }
> ```

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;

AgentRunOptions runOptions = new()
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
};

AgentResponse response = await agent.RunAsync("Please provide information about John Smith, who is a 35-year-old software engineer.", options: runOptions);

PersonInfo personInfo = JsonSerializer.Deserialize<PersonInfo>(response.Text, JsonSerializerOptions.Web)!;

Console.WriteLine($"Name: {personInfo.Name}, Age: {personInfo.Age}, Occupation: {personInfo.Occupation}");
```

The `ResponseFormat` can also be specified using a raw JSON schema string, which is useful when there is no corresponding .NET type available, such as for declarative agents or schemas loaded from external configuration:

```csharp
string jsonSchema = """
{
    "type": "object",
    "properties": {
        "name": { "type": "string" },
        "age": { "type": "integer" },
        "occupation": { "type": "string" }
    },
    "required": ["name", "age", "occupation"]
}
""";

AgentRunOptions runOptions = new()
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonElement.Parse(jsonSchema), "PersonInfo", "Information about a person")
};

AgentResponse response = await agent.RunAsync("Please provide information about John Smith, who is a 35-year-old software engineer.", options: runOptions);

JsonElement result = JsonSerializer.Deserialize<JsonElement>(response.Text);

Console.WriteLine($"Name: {result.GetProperty("name").GetString()}, Age: {result.GetProperty("age").GetInt32()}, Occupation: {result.GetProperty("occupation").GetString()}");
```

## Structured outputs with streaming

When streaming, the agent response is streamed as a series of updates, and you can only deserialize the response once all the updates have been received.
You must assemble all the updates into a single response before deserializing it.

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;

AIAgent agent = new AIProjectClient(
    new Uri("<your-foundry-project-endpoint>"),
    new DefaultAzureCredential())
        .AsAIAgent(new ChatClientAgentOptions()
        {
            Name = "HelpfulAssistant",
            ChatOptions = new()
            {
                ModelId = "gpt-4o-mini",
                Instructions = "You are a helpful assistant.",
                ResponseFormat = ChatResponseFormat.ForJsonSchema<PersonInfo>()
            }
        });

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

IAsyncEnumerable<AgentResponseUpdate> updates = agent.RunStreamingAsync("Please provide information about John Smith, who is a 35-year-old software engineer.");

AgentResponse response = await updates.ToAgentResponseAsync();

PersonInfo personInfo = JsonSerializer.Deserialize<PersonInfo>(response.Text)!;

Console.WriteLine($"Name: {personInfo.Name}, Age: {personInfo.Age}, Occupation: {personInfo.Occupation}");
```

## Structured outputs with agents with no structured outputs capabilities

Some agents don't natively support structured outputs, either because it's not part of the protocol or because the agents use language models without structured outputs capabilities. One possible approach is to create a custom decorator agent that wraps any `AIAgent` and uses an additional LLM call via a chat client to convert the agent's text response into structured JSON.

> [!NOTE]
> Since this approach relies on an additional LLM call to transform the response, its reliability may not be sufficient for all scenarios.

For a reference implementation of this pattern that you can adapt to your own requirements, see the [StructuredOutputAgent sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/02-agents/Agents/Agent_Step02_StructuredOutput).

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

### Streaming example

> [!TIP]
> See the [.NET samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples) for complete runnable examples.

::: zone-end
::: zone pivot="programming-language-python"

This tutorial step shows you how to produce structured outputs with an agent, where the agent is built on the Azure OpenAI Chat Completion service.

> [!IMPORTANT]
> Not all agent types support structured outputs. The `Agent` supports structured outputs when used with compatible chat clients.

## Prerequisites

For prerequisites and installing packages, see the [Create and run a simple agent](../concepts/agents/running-agents.md) step in this tutorial.

## Create the agent with structured outputs

The `Agent` is built on top of any chat client implementation that supports structured outputs.
The `Agent` uses the `response_format` key in the `options` dict to specify the desired output schema.

When running the agent, you can provide either:

- A Pydantic model that defines the structure of the expected output.
- A JSON schema mapping (`dict`) when you want parsed JSON without defining a model class.

You can pass the `options` dict at runtime via `agent.run(..., options={"response_format": ...})`, or set it at agent creation time via the `default_options` dict.

Various response formats are supported based on the underlying chat client capabilities.

The first example creates an agent that produces structured outputs in the form of a JSON object that conforms to a Pydantic model schema.

First, define a Pydantic model that represents the structure of the output you want from the agent:

```python
from pydantic import BaseModel

class PersonInfo(BaseModel):
    """Information about a person."""
    name: str | None = None
    age: int | None = None
    occupation: str | None = None
```

Now you can create an agent using the Azure OpenAI Chat Client:

```python
import os
from agent_framework.openai import OpenAIChatCompletionClient
from azure.identity import AzureCliCredential

# Create the agent using Azure OpenAI Chat Client
agent = OpenAIChatCompletionClient(
    model=os.environ["AZURE_OPENAI_CHAT_COMPLETION_MODEL"],
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
    credential=AzureCliCredential(),
).as_agent(
    name="HelpfulAssistant",
    instructions="You are a helpful assistant that extracts person information from text."
)
```

Now you can run the agent with some textual information and specify the structured outputs format using the `response_format` key in the `options` dict:

```python
response = await agent.run(
    "Please provide information about John Smith, who is a 35-year-old software engineer.",
    options={"response_format": PersonInfo},
)
```

For a Pydantic model response format, the agent response contains the structured outputs in the `value` property as a model instance:

```python
if response.value:
    person_info = response.value
    print(f"Name: {person_info.name}, Age: {person_info.age}, Occupation: {person_info.occupation}")
else:
    print("No structured data found in response")
```

### Use a JSON schema mapping

If you already have a JSON schema as a Python mapping, pass that schema directly as the `response_format` value in the `options` dict. In this mode, `response.value` contains the parsed JSON value (typically a `dict` or `list`) instead of a Pydantic model instance.

```python
person_info_schema = {
    "type": "object",
    "properties": {
        "name": {"type": "string"},
        "age": {"type": "integer"},
        "occupation": {"type": "string"},
    },
    "required": ["name", "age", "occupation"],
}

response = await agent.run(
    "Please provide information about John Smith, who is a 35-year-old software engineer.",
    options={"response_format": person_info_schema},
)

if response.value:
    person_info = response.value
    print(f"Name: {person_info['name']}, Age: {person_info['age']}, Occupation: {person_info['occupation']}")
```

When streaming, `agent.run(..., stream=True)` returns a `ResponseStream`. The stream's built-in finalizer automatically handles structured outputs parsing, so you can iterate for real-time updates and then call `get_final_response()` to get the parsed result:

```python
# Stream updates in real time, then get the structured result
stream = agent.run(query, stream=True, options={"response_format": PersonInfo})
async for update in stream:
    print(update.text, end="", flush=True)

# get_final_response() returns the AgentResponse with the parsed value
final_response = await stream.get_final_response()

if final_response.value:
    person_info = final_response.value
    print(f"Name: {person_info.name}, Age: {person_info.age}, Occupation: {person_info.occupation}")
```

The same rule applies when `response_format` is a JSON schema mapping: `final_response.value` contains parsed JSON instead of a Pydantic model instance.

If you don't need to process individual streaming updates, you can skip iteration entirely — `get_final_response()` will automatically consume the stream:

```python
stream = agent.run(query, stream=True, options={"response_format": PersonInfo})
final_response = await stream.get_final_response()

if final_response.value:
    person_info = final_response.value
    print(f"Name: {person_info.name}, Age: {person_info.age}, Occupation: {person_info.occupation}")
```

### Complete example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio

from agent_framework.openai import OpenAIChatClient
from pydantic import BaseModel

"""
OpenAI Responses Client with Structured Outputs Example

This sample demonstrates using structured outputs capabilities with OpenAI Responses Client,
showing Pydantic model integration for type-safe response parsing and data extraction.
"""


class OutputStruct(BaseModel):
    """A structured outputs model for testing purposes."""

    city: str
    description: str


async def non_streaming_example() -> None:
    print("=== Non-streaming example ===")

    agent = OpenAIChatClient().as_agent(
        name="CityAgent",
        instructions="You are a helpful agent that describes cities in a structured format.",
    )

    query = "Tell me about Paris, France"
    print(f"User: {query}")

    result = await agent.run(query, options={"response_format": OutputStruct})

    if structured_data := result.value:
        print("Structured Outputs Agent:")
        print(f"City: {structured_data.city}")
        print(f"Description: {structured_data.description}")
    else:
        print(f"Failed to parse response: {result.text}")


async def streaming_example() -> None:
    print("=== Streaming example ===")

    agent = OpenAIChatClient().as_agent(
        name="CityAgent",
        instructions="You are a helpful agent that describes cities in a structured format.",
    )

    query = "Tell me about Tokyo, Japan"
    print(f"User: {query}")

    # Stream updates in real time using ResponseStream
    stream = agent.run(query, stream=True, options={"response_format": OutputStruct})
    async for update in stream:
        if update.text:
            print(update.text, end="", flush=True)
    print()

    # get_final_response() returns the AgentResponse with structured outputs parsed
    result = await stream.get_final_response()

    if structured_data := result.value:
        print("Structured Outputs (from streaming with ResponseStream):")
        print(f"City: {structured_data.city}")
        print(f"Description: {structured_data.description}")
    else:
        print(f"Failed to parse response: {result.text}")


async def main() -> None:
    print("=== OpenAI Responses Agent with Structured Outputs ===")

    await non_streaming_example()
    await streaming_example()


if __name__ == "__main__":
    asyncio.run(main())
```

::: zone-end

::: zone pivot="programming-language-go"
## Structured output

Go agents support structured output through the `agent.WithStructuredOutput` option. Define a Go struct and the framework automatically generates the JSON schema and unmarshals the response.

### Define the output type

```go
type PersonInfo struct {
    Name       string `json:"name"`
    Age        int    `json:"age"`
    Occupation string `json:"occupation"`
}
```

### Request structured output

Use a generic helper to invoke the agent and unmarshal the response:

```go
import (
    "context"
    "fmt"

    "github.com/microsoft/agent-framework-go/agent"
)

func runFor[T any](ctx context.Context, a *agent.Agent, message string, opts ...agent.Option) (T, error) {
    var v T
    opts = append(opts, agent.WithStructuredOutput(&v), agent.Stream(false))
    for _, err := range a.RunText(ctx, message, opts...) {
        if err != nil {
            return v, err
        }
    }
    return v, nil
}

person, err := runFor[PersonInfo](ctx, a,
    "Please provide information about John Smith, who is a 35-year-old software engineer.")
fmt.Println("Name:", person.Name)
fmt.Println("Age:", person.Age)
```

### Specify response format at agent level

You can also set the response format on the agent configuration so all runs produce structured output:

```go
import "github.com/microsoft/agent-framework-go/agent/format/jsonformat"

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
        RunOptions: []agent.Option{
            agent.WithResponseFormat(jsonformat.MustFor[PersonInfo]()),
        },
    },
})
```

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step05_structured_output/main.go) for a complete runnable example.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Background Responses](./background-responses.md)
