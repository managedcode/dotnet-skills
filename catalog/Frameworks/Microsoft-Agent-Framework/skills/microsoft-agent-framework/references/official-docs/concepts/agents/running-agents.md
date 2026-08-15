---
title: Running Agents
description: Learn how to run agents with Agent Framework
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: reference
ms.author: evmattso
ms.date: 05/27/2026
ms.service: agent-framework
---

# Running Agents

The base Agent abstraction exposes various options for running the agent. Callers can choose to supply zero, one, or many input messages. Callers can also choose between streaming and non-streaming. Let's dig into the different usage scenarios.

## Streaming and non-streaming

Microsoft Agent Framework supports both streaming and non-streaming methods for running an agent.

::: zone pivot="programming-language-csharp"

For non-streaming, use the `RunAsync` method.

```csharp
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

For streaming, use the `RunStreamingAsync` method.

```csharp
await foreach (var update in agent.RunStreamingAsync("What is the weather like in Amsterdam?"))
{
    Console.Write(update);
}
```

::: zone-end
::: zone pivot="programming-language-python"

For non-streaming, use the `run` method.

```python
result = await agent.run("What is the weather like in Amsterdam?")
print(result.text)
```

For streaming, use the `run` method with `stream=True`. This returns a `ResponseStream` object that can be iterated asynchronously:

```python
async for update in agent.run("What is the weather like in Amsterdam?", stream=True):
    if update.text:
        print(update.text, end="", flush=True)
```

### ResponseStream

The `ResponseStream` object returned by `run(..., stream=True)` supports two consumption patterns:

**Pattern 1: Async iteration** — process updates as they arrive for real-time display:

```python
response_stream = agent.run("Tell me a story", stream=True)
async for update in response_stream:
    if update.text:
        print(update.text, end="", flush=True)
```

**Pattern 2: Direct finalization** — skip iteration and get the complete response:

```python
response_stream = agent.run("Tell me a story", stream=True)
final = await response_stream.get_final_response()
print(final.text)
```

**Pattern 3: Combined** — iterate for real-time display, then get the aggregated result:

```python
response_stream = agent.run("Tell me a story", stream=True)

# First, iterate to display streaming output
async for update in response_stream:
    if update.text:
        print(update.text, end="", flush=True)

# Then get the complete response (uses already-collected updates, does not re-iterate)
final = await response_stream.get_final_response()
print(f"\n\nFull response: {final.text}")
print(f"Messages: {len(final.messages)}")
```

::: zone-end

::: zone pivot="programming-language-go"

In Go, `RunText` returns a `ResponseStream` - an iterator of `(ResponseUpdate, error)` pairs.

For non-streaming, call `Collect()` on the stream to gather all updates into a single response:

```go
resp, err := a.RunText(ctx, "What is the weather like in Amsterdam?").Collect()
fmt.Println(resp, err)
```

For streaming, iterate over the stream directly using a `range` loop:

```go
for update, err := range a.RunText(ctx, "What is the weather like in Amsterdam?", agent.Stream(true)) {
    fmt.Print(update, err)
}
```

::: zone-end

## Agent run options

::: zone pivot="programming-language-csharp"

The base agent abstraction does allow passing an options object for each agent run, however the ability to customize a run at the abstraction level is quite limited.
Agents can vary significantly and therefore there aren't really common customization options.

For cases where the caller knows the type of the agent they are working with, it is possible to pass type specific options to allow customizing the run.

For example, here the agent is a `ChatClientAgent` and it is possible to pass a `ChatClientAgentRunOptions` object that inherits from `AgentRunOptions`.
This allows the caller to provide custom <xref:Microsoft.Extensions.AI.ChatOptions> that are merged with any agent level options before being passed to the `IChatClient` that
the `ChatClientAgent` is built on.

```csharp
var chatOptions = new ChatOptions() { Tools = [AIFunctionFactory.Create(GetWeather)] };
Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?", options: new ChatClientAgentRunOptions(chatOptions)));
```

::: zone-end
::: zone pivot="programming-language-python"

Python agents support customizing each run via the `options` parameter. Options are passed as a TypedDict and can be set at both construction time (via `default_options`) and per-run (via `options`). Each provider has its own TypedDict class that provides full IDE autocomplete and type checking for provider-specific settings.

Common options include:

- `max_tokens`: Maximum number of tokens to generate
- `temperature`: Controls randomness in response generation
- `model`: Override the model for this specific run
- `top_p`: Nucleus sampling parameter
- `response_format`: Specify the response format (e.g., structured outputs)

> [!NOTE]
> The `tools` and `instructions` parameters remain as direct keyword arguments and are not passed via the `options` dictionary.

```python
from agent_framework.openai import OpenAIChatClient, OpenAIChatOptions

# Set default options at construction time
agent = OpenAIChatClient().as_agent(
    instructions="You are a helpful assistant",
    default_options={
        "temperature": 0.7,
        "max_tokens": 500
    }
)

# Run with custom options (overrides defaults)
# OpenAIChatOptions provides IDE autocomplete for all OpenAI-specific settings
options: OpenAIChatOptions = {
    "temperature": 0.3,
    "max_tokens": 150,
    "model": "gpt-4o",
    "presence_penalty": 0.5,
    "frequency_penalty": 0.3
}

result = await agent.run(
    "What is the weather like in Amsterdam?",
    options=options
)

# Streaming with custom options
async for update in agent.run(
    "Tell me a detailed weather forecast",
    stream=True,
    options={"temperature": 0.7, "top_p": 0.9},
    tools=[additional_weather_tool]  # tools is still a keyword argument
):
    if update.text:
        print(update.text, end="", flush=True)
```

Each provider has its own TypedDict class (e.g., `OpenAIChatOptions`, `AnthropicChatOptions`, `OllamaChatOptions`) that exposes the full set of options supported by that provider.

When both `default_options` and per-run `options` are provided, the per-run options take precedence and are merged with the defaults.

::: zone-end

::: zone pivot="programming-language-go"

Options are passed as variadic `agent.Option` arguments. Available options include:

- `agent.Stream(true)` - Enable streaming
- `agent.WithSession(session)` - Attach a session for multi-turn conversations
- `agent.WithStructuredOutput(&v)` - Request structured output into a typed value
- `agent.WithResponseFormat(format)` - Specify the response format
- `agent.WithTool(tool)` - Add a tool for this run
- `agent.AllowBackgroundResponses(true)` - Enable background responses

```go
resp, err := a.RunText(ctx, "Tell me a joke.",
    agent.Stream(true),
    agent.WithSession(session),
).Collect()
```

::: zone-end

## Response types

Both streaming and non-streaming responses from agents contain all content produced by the agent.
Content might include data that is not the result (that is, the answer to the user question) from the agent.
Examples of other data returned include function tool calls, results from function tool calls, reasoning text, status updates, and many more.

Since not all content returned is the result, it's important to look for specific content types when trying to isolate the result from the other content.

::: zone pivot="programming-language-csharp"

To extract the text result from a response, all `TextContent` items from all `ChatMessages` items need to be aggregated.
To simplify this, a `Text` property is available on all response types that aggregates all `TextContent`.

For the non-streaming case, everything is returned in one `AgentResponse` object.
`AgentResponse` allows access to the produced messages via the `Messages` property.

```csharp
var response = await agent.RunAsync("What is the weather like in Amsterdam?");
Console.WriteLine(response.Text);
Console.WriteLine(response.Messages.Count);
```

For the streaming case, `AgentResponseUpdate` objects are streamed as they are produced.
Each update might contain a part of the result from the agent, and also various other content items.
Similar to the non-streaming case, it is possible to use the `Text` property to get the portion
of the result contained in the update, and drill into the detail via the `Contents` property.

```csharp
await foreach (var update in agent.RunStreamingAsync("What is the weather like in Amsterdam?"))
{
    Console.WriteLine(update.Text);
    Console.WriteLine(update.Contents.Count);
}
```

::: zone-end
::: zone pivot="programming-language-python"

For the non-streaming case, everything is returned in one `AgentResponse` object.
`AgentResponse` allows access to the produced messages via the `messages` property.

To extract the text result from a response, all `TextContent` items from all `Message` items need to be aggregated.
To simplify this, a `Text` property is available on all response types that aggregates all `TextContent`.

```python
response = await agent.run("What is the weather like in Amsterdam?")
print(response.text)
print(len(response.messages))

# Access individual messages
for message in response.messages:
    print(f"Role: {message.role}, Text: {message.text}")
```

For the streaming case, `AgentResponseUpdate` objects are streamed as they are produced via the `ResponseStream` returned by `run(..., stream=True)`.
Each update might contain a part of the result from the agent, and also various other content items.
Similar to the non-streaming case, it is possible to use the `text` property to get the portion
of the result contained in the update, and drill into the detail via the `contents` property.

```python
response_stream = agent.run("What is the weather like in Amsterdam?", stream=True)
async for update in response_stream:
    print(f"Update text: {update.text}")
    print(f"Content count: {len(update.contents)}")

    # Access individual content items
    for content in update.contents:
        if hasattr(content, 'text'):
            print(f"Content: {content.text}")

# Get the aggregated final response after streaming
final = await response_stream.get_final_response()
print(f"Complete text: {final.text}")
```

::: zone-end

::: zone pivot="programming-language-go"

`ResponseStream` yields `*agent.ResponseUpdate` values. Each update contains:

- `Contents` - Slice of `message.Content` values, such as text, function calls, and usage
- `Role` - The message role, such as assistant or system
- `MessageID` / `ResponseID` - Identifiers for the message and response

To get the full text result from a non-streaming response, use `Collect()`:

```go
resp, err := a.RunText(ctx, "What is the weather like in Amsterdam?").Collect()
fmt.Println(resp, err)
```

::: zone-end

## Message types

Input and output from agents are represented as messages. Messages are subdivided into content items.

::: zone pivot="programming-language-csharp"

The Microsoft Agent Framework uses the message and content types provided by the <xref:Microsoft.Extensions.AI> abstractions.
Messages are represented by the `ChatMessage` class and all content classes inherit from the base `AIContent` class.

Various `AIContent` subclasses exist that are used to represent different types of content. Some are provided as
part of the base <xref:Microsoft.Extensions.AI> abstractions, but providers can also add their own types, where needed.

Here are some popular types from <xref:Microsoft.Extensions.AI>:

| Type                                       | Description |
|--------------------------------------------|-------------|
| <xref:Microsoft.Extensions.AI.TextContent> | Textual content that can be both input, for example, from a user or developer, and output from the agent. Typically contains the text result from an agent. |
| <xref:Microsoft.Extensions.AI.DataContent> | Binary content that can be both input and output. Can be used to pass image, audio or video data to and from the agent (where supported). |
| <xref:Microsoft.Extensions.AI.UriContent> |A URL that typically points at hosted content such as an image, audio or video. |
| <xref:Microsoft.Extensions.AI.FunctionCallContent> | A request by an inference service to invoke a function tool. |
| <xref:Microsoft.Extensions.AI.FunctionResultContent> | The result of a function tool invocation. |

::: zone-end

::: zone pivot="programming-language-python"

The Python Agent Framework uses message and content types from the `agent_framework` package.
Messages are represented by the `Message` class and all content items are represented by the `Content` class discriminated by the `type` property.

All content is represented by the unified `Content` class with factory methods for each content type. Use the `type` property to check the content type. The following content types are available:

| Content Type | Factory Method | Description |
|---|---|---|
| `"text"` | `Content.from_text()` | Textual content for input and output. Typically contains the text result from an agent. |
| `"text_reasoning"` | `Content.from_text_reasoning()` | Reasoning text from models that support chain-of-thought reasoning. May include protected data. |
| `"data"` | `Content.from_data()`, `Content.from_uri()` | Binary content encoded as a data URI. Used for images, audio, video, and documents. |
| `"uri"` | `Content.from_uri()` | A URL pointing to hosted content such as an image, audio, or video. |
| `"error"` | `Content.from_error()` | Error information when processing fails. Includes optional error code and details. |
| `"function_call"` | `Content.from_function_call()` | A request by an AI service to invoke a function tool. |
| `"function_result"` | `Content.from_function_result()` | The result of a function tool invocation. |
| `"usage"` | `Content.from_usage()` | Token usage and billing information from the AI service. |
| `"hosted_file"` | `Content.from_hosted_file()` | A reference to a file hosted by the provider (for example, uploaded to OpenAI). |
| `"hosted_vector_store"` | `Content.from_hosted_vector_store()` | A reference to a vector store hosted by the provider. |
| `"code_interpreter_tool_call"` | `Content.from_code_interpreter_tool_call()` | A request by the AI service to execute code via a code interpreter. |
| `"code_interpreter_tool_result"` | `Content.from_code_interpreter_tool_result()` | The result of a code interpreter execution. |
| `"image_generation_tool_call"` | `Content.from_image_generation_tool_call()` | A request by the AI service to generate an image. |
| `"image_generation_tool_result"` | `Content.from_image_generation_tool_result()` | The result of an image generation request. |
| `"mcp_server_tool_call"` | `Content.from_mcp_server_tool_call()` | A request to invoke a tool on an MCP server. |
| `"mcp_server_tool_result"` | `Content.from_mcp_server_tool_result()` | The result of an MCP server tool invocation. |
| `"shell_tool_call"` | `Content.from_shell_tool_call()` | A request by the AI service to execute shell commands. |
| `"shell_tool_result"` | `Content.from_shell_tool_result()` | The aggregate result of a shell tool call. |
| `"shell_command_output"` | `Content.from_shell_command_output()` | The output of a single shell command execution. |
| `"function_approval_request"` | `Content.from_function_approval_request()` | A request for user approval before executing a function call. |
| `"function_approval_response"` | `Content.from_function_approval_response()` | The user's response to a function approval request. |
| `"oauth_consent_request"` | `Content.from_oauth_consent_request()` | A request for the user to complete OAuth consent via a provided link. |

Here's how to work with different content types:

```python
from agent_framework import Message, Content

# Create a text message
text_message = Message(role="user", contents=["Hello!"])

# Create a message with multiple content types
image_data = b"..."  # your image bytes
mixed_message = Message(
    role="user",
    contents=[
        Content.from_text("Analyze this image:"),
        Content.from_data(data=image_data, media_type="image/png"),
    ]
)

# Access content from responses
response = await agent.run("Describe the image")
for message in response.messages:
    for content in message.contents:
        if content.type == "text":
            print(f"Text: {content.text}")
        elif content.type == "data":
            print(f"Data URI: {content.uri}")
        elif content.type == "uri":
            print(f"External URI: {content.uri}")
```

::: zone-end

::: zone pivot="programming-language-go"

The Go Agent Framework uses message and content types from the `message` package. Response updates can contain multiple content items; inspect each item for the content type you need.

For streaming, process updates individually as they arrive:

```go
for update, err := range a.RunText(ctx, "Tell me a story.", agent.Stream(true)) {
    fmt.Print(err)
    for _, c := range update.Contents {
        if text, ok := c.(*message.TextContent); ok {
            fmt.Print(text.Text)
        }
    }
}
```

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework-go/blob/main/examples/02-agents/agents/step01_running/main.go) for a complete runnable example.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agent Pipeline](./agent-pipeline.md)
