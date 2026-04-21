---
title: Testing with AG-UI Dojo
description: Learn how to test your Microsoft Agent Framework agents with AG-UI's Dojo application
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.date: 11/07/2025
ms.author: evmattso
ms.service: agent-framework
---

# Testing with AG-UI Dojo

The [AG-UI Dojo application](https://dojo.ag-ui.com/) provides an interactive environment to test and explore Microsoft Agent Framework agents that implement the AG-UI protocol. Dojo offers a visual interface to connect to your agents and interact with all 7 AG-UI features.

:::zone pivot="programming-language-python"

## Prerequisites

Before you begin, ensure you have:

- Python 3.10 or higher
- [uv](https://docs.astral.sh/uv/) for dependency management
- An OpenAI API key or Azure OpenAI endpoint
- Node.js and pnpm (for running the Dojo frontend)

## Installation

### 1. Clone the AG-UI Repository

First, clone the AG-UI repository which contains the Dojo application and Microsoft Agent Framework integration examples:

```bash
git clone https://github.com/ag-oss/ag-ui.git
cd ag-ui
```

### 2. Navigate to Examples Directory

```bash
cd integrations/microsoft-agent-framework/python/examples
```

### 3. Install Python Dependencies

Use `uv` to install the required dependencies:

```bash
uv sync
```

### 4. Configure Environment Variables

Create a `.env` file from the provided template:

```bash
cp .env.example .env
```

Edit the `.env` file and add your API credentials:

```python
# For OpenAI
OPENAI_API_KEY=your_api_key_here
OPENAI_CHAT_MODEL_ID="gpt-4.1"

# Or for Azure OpenAI
AZURE_OPENAI_ENDPOINT=your_endpoint_here
AZURE_OPENAI_API_KEY=your_api_key_here
AZURE_OPENAI_CHAT_DEPLOYMENT_NAME=your_deployment_here
```

> [!NOTE]
> If using `DefaultAzureCredential`, in place for an `api_key` for authentication, make sure you're authenticated with Azure (e.g., via `az login`). For more information, see the [Azure Identity documentation](/python/api/azure-identity/azure.identity.defaultazurecredential).

## Running the Dojo Application

### 1. Start the Backend Server

In the examples directory, start the backend server with the example agents:

```bash
cd integrations/microsoft-agent-framework/python/examples
uv run dev
```

The server will start on `http://localhost:8888` by default.

### 2. Start the Dojo Frontend

Open a new terminal window, navigate to the root of the AG-UI repository, and then to the Dojo application directory:

```bash
cd apps/dojo
pnpm install
pnpm dev
```

The Dojo frontend will be available at `http://localhost:3000`.

### 3. Connect to Your Agent

1. Open `http://localhost:3000` in your browser
2. Configure the server URL to `http://localhost:8888`

3. Select "Microsoft Agent Framework (Python)" from the dropdown
4. Start exploring the example agents

## Available Example Agents

The integration examples demonstrate all 7 AG-UI features through different agent endpoints:

| Endpoint | Feature | Description |
|----------|---------|-------------|
| `/agentic_chat` | Feature 1: Agentic Chat | Basic conversational agent with tool calling |
| `/backend_tool_rendering` | Feature 2: Backend Tool Rendering | Agent with custom tool UI rendering |
| `/human_in_the_loop` | Feature 3: Human in the Loop | Agent with approval workflows |
| `/agentic_generative_ui` | Feature 4: Agentic Generative UI | Agent that breaks down tasks into steps with streaming updates |
| `/tool_based_generative_ui` | Feature 5: Tool-based Generative UI | Agent that generates custom UI components |
| `/shared_state` | Feature 6: Shared State | Agent with bidirectional state synchronization |
| `/predictive_state_updates` | Feature 7: Predictive State Updates | Agent with predictive state updates during tool execution |

## Testing Your Own Agents

To test your own agents with Dojo:

### 1. Create Your Agent

Create a new agent following the [Getting Started](getting-started.md) guide:

```python
from agent_framework import ChatAgent
from agent_framework_azure_ai import AzureOpenAIChatClient

# Create your agent
chat_client = AzureOpenAIChatClient(
    endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),
    deployment_name=os.getenv("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
)

agent = ChatAgent(
    name="my_test_agent",
    chat_client=chat_client,
    system_message="You are a helpful assistant.",
)
```

### 2. Add the Agent to Your Server

In your FastAPI application, register the agent endpoint:

```python
from fastapi import FastAPI
from agent_framework_ag_ui import add_agent_framework_fastapi_endpoint
import uvicorn

app = FastAPI()

# Register your agent
add_agent_framework_fastapi_endpoint(
    app=app,
    path="/my_agent",
    agent=agent,
)

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8888)
```

### 3. Test in Dojo

1. Start your server
2. Open Dojo at `http://localhost:3000`
3. Set the server URL to `http://localhost:8888`
4. Your agent will appear in the endpoint dropdown as "my_agent"
5. Select it and start testing

## Project Structure

The AG-UI repository's integration examples follow this structure:

```
integrations/microsoft-agent-framework/python/examples/
├── agents/
│   ├── agentic_chat/                  # Feature 1: Basic chat agent
│   ├── backend_tool_rendering/        # Feature 2: Backend tool rendering
│   ├── human_in_the_loop/             # Feature 3: Human-in-the-loop
│   ├── agentic_generative_ui/         # Feature 4: Streaming state updates
│   ├── tool_based_generative_ui/      # Feature 5: Custom UI components
│   ├── shared_state/                  # Feature 6: Bidirectional state sync
│   ├── predictive_state_updates/      # Feature 7: Predictive state updates
│   └── dojo.py                        # FastAPI application setup
├── pyproject.toml                     # Dependencies and scripts
├── .env.example                       # Environment variable template
└── README.md                          # Integration examples documentation
```

## Troubleshooting

### Server Connection Issues

If Dojo can't connect to your server:

- Verify the server is running on the correct port (default: 8888)
- Check that the server URL in Dojo matches your server address
- Ensure no firewall is blocking the connection
- Look for CORS errors in the browser console

### Agent Not Appearing

If your agent doesn't appear in the Dojo dropdown:

- Verify the agent endpoint is registered correctly
- Check server logs for any startup errors
- Ensure the `add_agent_framework_fastapi_endpoint` call completed successfully

### Environment Variable Issues

If you see authentication errors:

- Verify your `.env` file is in the correct directory
- Check that all required environment variables are set
- Ensure API keys and endpoints are valid
- Restart the server after changing environment variables

## Next Steps

- Explore the [example agents](https://github.com/ag-ui-protocol/ag-ui/tree/main/integrations/microsoft-agent-framework/python/examples/agents) to see implementation patterns
- Learn about [Backend Tool Rendering](backend-tool-rendering.md) to customize tool UIs
<!-- - Implement [Human-in-the-Loop](human-in-the-loop.md) workflows for approval flows -->
<!-- - Add [State Management](state-management.md) for complex interactive experiences -->

## Additional Resources

- [AG-UI Documentation](https://docs.ag-ui.com/introduction)
- [AG-UI GitHub Repository](https://github.com/ag-ui-protocol/ag-ui)
- [Dojo Application](https://dojo.ag-ui.com/)

- [Microsoft Agent Framework Integration Examples](https://github.com/ag-ui-protocol/ag-ui/tree/main/integrations/microsoft-agent-framework)

:::zone-end

::: zone pivot="programming-language-csharp"

Coming soon.

::: zone-end
