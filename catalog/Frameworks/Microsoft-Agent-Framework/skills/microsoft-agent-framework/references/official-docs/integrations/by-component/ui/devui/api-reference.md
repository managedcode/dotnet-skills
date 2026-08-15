---
title: DevUI API Reference
description: Learn about the OpenAI-compatible API endpoints provided by DevUI.
author: moonbox3
ms.topic: reference
ms.author: evmattso
ms.date: 12/10/2025
ms.service: agent-framework
zone_pivot_groups: programming-languages
---

# API Reference

DevUI provides an OpenAI-compatible Responses API, allowing you to use the OpenAI SDK or any HTTP client to interact with your agents and workflows.

::: zone pivot="programming-language-csharp"

## Coming Soon

DevUI documentation for C# is coming soon. Please check back later or refer to the Python documentation for conceptual guidance.

::: zone-end

::: zone pivot="programming-language-python"

## Base URL

```
http://localhost:8080/v1
```

The port can be configured with the `--port` CLI option.

## Authentication

By default, DevUI does not require authentication for local development. When running with `--auth`, Bearer token authentication is required.

## Using the OpenAI SDK

### Basic Request

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:8080/v1",
    api_key="not-needed"  # API key not required for local DevUI
)

response = client.responses.create(
    metadata={"entity_id": "weather_agent"},  # Your agent/workflow name
    input="What's the weather in Seattle?"
)

# Extract text from response
print(response.output[0].content[0].text)
```

### Streaming

```python
response = client.responses.create(
    metadata={"entity_id": "weather_agent"},
    input="What's the weather in Seattle?",
    stream=True
)

for event in response:
    # Process streaming events
    print(event)
```

### Multi-turn Conversations

Use the standard OpenAI `conversation` parameter for multi-turn conversations:

```python
# Create a conversation
conversation = client.conversations.create(
    metadata={"agent_id": "weather_agent"}
)

# First turn
response1 = client.responses.create(
    metadata={"entity_id": "weather_agent"},
    input="What's the weather in Seattle?",
    conversation=conversation.id
)

# Follow-up turn (continues the conversation)
response2 = client.responses.create(
    metadata={"entity_id": "weather_agent"},
    input="How about tomorrow?",
    conversation=conversation.id
)
```

DevUI automatically retrieves the conversation's message history and passes it to the agent.

## REST API Endpoints

### Responses API (OpenAI Standard)

Execute an agent or workflow:

```bash
curl -X POST http://localhost:8080/v1/responses \
  -H "Content-Type: application/json" \
  -d '{
    "metadata": {"entity_id": "weather_agent"},
    "input": "What is the weather in Seattle?"
  }'
```

### Conversations API (OpenAI Standard)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/conversations` | POST | Create a conversation |
| `/v1/conversations/{id}` | GET | Get conversation details |
| `/v1/conversations/{id}` | POST | Update conversation metadata |
| `/v1/conversations/{id}` | DELETE | Delete a conversation |
| `/v1/conversations?agent_id={id}` | GET | List conversations (DevUI extension) |
| `/v1/conversations/{id}/items` | POST | Add items to conversation |
| `/v1/conversations/{id}/items` | GET | List conversation items |
| `/v1/conversations/{id}/items/{item_id}` | GET | Get a conversation item |

### Entity Management (DevUI Extension)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/entities` | GET | List discovered agents/workflows |
| `/v1/entities/{entity_id}/info` | GET | Get detailed entity information |
| `/v1/entities/{entity_id}/reload` | POST | Hot reload entity (developer mode) |

### Health Check

```bash
curl http://localhost:8080/health
```

### Server Metadata

Get server configuration and capabilities:

```bash
curl http://localhost:8080/meta
```

Returns:
- `ui_mode` - Current mode (`developer` or `user`)
- `version` - DevUI version
- `framework` - Framework name (`agent_framework`)
- `runtime` - Backend runtime (`python`)
- `capabilities` - Feature flags (tracing, OpenAI proxy, deployment)
- `auth_required` - Whether authentication is enabled

## Event Mapping

DevUI maps Agent Framework events to OpenAI Responses API events. The table below shows the mapping:

### Lifecycle Events

| OpenAI Event | Agent Framework Event |
|--------------|----------------------|
| `response.created` + `response.in_progress` | `AgentStartedEvent` |
| `response.completed` | `AgentCompletedEvent` |
| `response.failed` | `AgentFailedEvent` |
| `response.created` + `response.in_progress` | `WorkflowEvent` with `type="started"` |
| `response.completed` | `WorkflowEvent` with `type="completed"` |
| `response.failed` | `WorkflowEvent` with `type="failed"` |

### Content Types

| OpenAI Event | Agent Framework Content |
|--------------|------------------------|
| `response.content_part.added` + `response.output_text.delta` | `Content(type="text")` |
| `response.reasoning_text.delta` | `Content(type="text_reasoning")` |
| `response.output_item.added` | `Content(type="function_call")` (initial) |
| `response.function_call_arguments.delta` | `Content(type="function_call")` (args) |
| `response.function_result.complete` | `Content(type="function_result")` |
| `response.output_item.added` (image) | `Content(type="data")` (images) |
| `response.output_item.added` (file) | `Content(type="data")` (files) |
| `error` | `Content(type="error")` |

### Workflow Events

| OpenAI Event | Agent Framework Event |
|--------------|----------------------|
| `response.output_item.added` (ExecutorActionItem) | `WorkflowEvent` with `type="executor_invoked"` |
| `response.output_item.done` (ExecutorActionItem) | `WorkflowEvent` with `type="executor_completed"` |
| `response.output_item.added` (ResponseOutputMessage) | `WorkflowEvent` with `type="output"` |

### DevUI Custom Extensions

DevUI adds custom event types for Agent Framework-specific functionality:

- `response.function_approval.requested` - Function approval requests
- `response.function_approval.responded` - Function approval responses
- `response.function_result.complete` - Server-side function execution results
- `response.workflow_event.completed` - Workflow events
- `response.trace.complete` - Execution traces

These custom extensions are namespaced and can be safely ignored by standard OpenAI clients.

## OpenAI Proxy Mode

DevUI provides an **OpenAI Proxy** feature for testing OpenAI models directly through the interface without creating custom agents. Enable via Settings in the UI.

```bash
curl -X POST http://localhost:8080/v1/responses \
  -H "X-Proxy-Backend: openai" \
  -d '{"model": "gpt-4.1-mini", "input": "Hello"}'
```

> [!NOTE]
> Proxy mode requires `OPENAI_API_KEY` environment variable configured on the backend.

::: zone-end

::: zone pivot="programming-language-go"

> [!NOTE]
> Go support for this feature is coming soon. See the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go) for the latest status.

::: zone-end
## Next Steps

- [Tracing & Observability](./tracing.md) - View traces for debugging
- [Security & Deployment](./security.md) - Secure your DevUI deployment
