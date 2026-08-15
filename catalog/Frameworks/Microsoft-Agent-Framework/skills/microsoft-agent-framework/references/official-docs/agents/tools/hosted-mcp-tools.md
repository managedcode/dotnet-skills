---
title: Hosted MCP Tools
description: Use hosted Model Context Protocol tools with Agent Framework agents.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: reference
ms.author: evmattso
ms.date: 07/01/2026
ms.service: agent-framework
---

# Using hosted MCP tools with agents

You can extend the capabilities of your Microsoft Foundry agent by connecting it to tools hosted on remote [Model Context Protocol (MCP)](/azure/ai-foundry/agents/how-to/tools/model-context-protocol) servers (bring your own MCP server endpoint).

## How to use the Model Context Protocol tool

This section explains how to create an agent with a hosted Model Context Protocol (MCP) server integration. The agent can utilize MCP tools that are managed and executed by the backing AI service, allowing for secure and controlled access to external resources.

### Key Features

- **Hosted MCP Server**: The MCP server is hosted and managed by Foundry, eliminating the need to manage server infrastructure
- **Persistent Agents**: Agents are created and stored server-side, allowing for stateful conversations
- **Tool Approval Workflow**: Configurable approval mechanisms for MCP tool invocations

### How It Works

::: zone pivot="programming-language-csharp"

#### 1. Environment Setup

The sample requires two environment variables:
- `AZURE_FOUNDRY_PROJECT_ENDPOINT`: Your Foundry project endpoint URL
- `AZURE_FOUNDRY_PROJECT_MODEL_ID`: The model deployment name (defaults to "gpt-4.1-mini")

```csharp
var endpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_FOUNDRY_PROJECT_ENDPOINT is not set.");
var model = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_MODEL_ID") ?? "gpt-4.1-mini";
```

#### 2. Agent Configuration

The agent is configured with specific instructions and metadata:

```csharp
const string AgentName = "MicrosoftLearnAgent";
const string AgentInstructions = "You answer questions by searching the Microsoft Learn content only.";
```

This creates an agent specialized for answering questions using Microsoft Learn documentation.

#### 3. MCP Tool Definition

The sample creates an MCP tool definition that points to a hosted MCP server:

```csharp
var mcpTool = new MCPToolDefinition(
    serverLabel: "microsoft_learn",
    serverUrl: "https://learn.microsoft.com/api/mcp");
mcpTool.AllowedTools.Add("microsoft_docs_search");
```

**Key Components:**
- **serverLabel**: A unique identifier for the MCP server instance
- **serverUrl**: The URL of the hosted MCP server
- **AllowedTools**: Specifies which tools from the MCP server the agent can use

#### 4. Agent Creation

The agent is created server-side using the Azure AI Projects SDK:

```csharp
var aiProjectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

var agentVersion = await aiProjectClient.AgentAdministrationClient.CreateAgentVersionAsync(
    AgentName,
    new ProjectsAgentVersionCreationOptions(
        new DeclarativeAgentDefinition(model)
        {
            Instructions = AgentInstructions,
            Tools = { mcpTool }
        }));
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

This creates a versioned agent that:
- Lives on the Foundry service
- Has access to the specified MCP tools
- Can maintain conversation state across multiple interactions

#### 5. Agent Retrieval and Execution

The created agent is retrieved as an `AIAgent` instance:

```csharp
AIAgent agent = aiProjectClient.AsAIAgent(agentVersion);
```

#### 6. Tool Resource Configuration

The sample configures tool resources with approval settings:

```csharp
var runOptions = new ChatClientAgentRunOptions()
{
    ChatOptions = new()
    {
        RawRepresentationFactory = (_) => new ThreadAndRunOptions()
        {
            ToolResources = new MCPToolResource(serverLabel: "microsoft_learn")
            {
                RequireApproval = new MCPApproval("never"),
            }.ToToolResources()
        }
    }
};
```

**Key Configuration:**
- **MCPToolResource**: Links the MCP server instance to the agent execution
- **RequireApproval**: Controls when user approval is needed for tool invocations
  - `"never"`: Tools execute automatically without approval
  - `"always"`: All tool invocations require user approval
  - Custom approval rules can also be configured

#### 7. Agent Execution

The agent is invoked with a question and executes using the configured MCP tools:

```csharp
AgentSession session = await agent.CreateSessionAsync();
var response = await agent.RunAsync(
    "Please summarize the Azure AI Agent documentation related to MCP Tool calling?",
    session,
    runOptions);
Console.WriteLine(response);
```

#### 8. Cleanup

The sample demonstrates proper resource cleanup:

```csharp
await aiProjectClient.AgentAdministrationClient.DeleteAgentAsync(agent.Id);
```

> [!TIP]
> See the [.NET Foundry Agent Hosted MCP Sample](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol/FoundryAgent_Hosted_MCP) for a complete runnable example.


::: zone-end
::: zone pivot="programming-language-python"

Foundry provides seamless integration with Model Context Protocol (MCP) servers through the Python Agent Framework. The service manages the MCP server hosting and execution, eliminating infrastructure management while providing secure, controlled access to external tools.

### Environment Setup

Configure your Foundry project credentials through environment variables:

```python
import os
from azure.identity.aio import AzureCliCredential
from agent_framework.foundry import FoundryChatClient

# Required environment variables
os.environ["FOUNDRY_PROJECT_ENDPOINT"] = "https://<your-project>.services.ai.azure.com/api/projects/<project-id>"
os.environ["FOUNDRY_MODEL"] = "gpt-4o-mini"
```

### Basic MCP Integration

Create a Foundry agent with hosted MCP tools:

```python
import asyncio
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from azure.identity.aio import AzureCliCredential

async def basic_foundry_mcp_example():
    """Basic example of Foundry agent with hosted MCP tools."""
    async with AzureCliCredential() as credential:
        client = FoundryChatClient(credential=credential)
        # Create a hosted MCP tool using the client method
        learn_mcp = client.get_mcp_tool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
        )

        # Create agent with hosted MCP tool
        async with Agent(
            client=client,
            name="MicrosoftLearnAgent",
            instructions="You answer questions by searching Microsoft Learn content only.",
            tools=[learn_mcp],
        ) as agent:
            # Simple query without approval workflow
            result = await agent.run(
                "Please summarize the Azure AI Agent documentation related to MCP tool calling?"
            )
            print(result.text)

if __name__ == "__main__":
    asyncio.run(basic_foundry_mcp_example())
```

### Multi-Tool MCP Configuration

Use multiple hosted MCP tools with a single agent:

```python
async def multi_tool_mcp_example():
    """Example using multiple hosted MCP tools."""
    async with AzureCliCredential() as credential:
        client = FoundryChatClient(credential=credential)
        # Create multiple MCP tools using the client method
        learn_mcp = client.get_mcp_tool(
            name="Microsoft Learn MCP",
            url="https://learn.microsoft.com/api/mcp",
            approval_mode="never_require",  # Auto-approve documentation searches
        )
        github_mcp = client.get_mcp_tool(
            name="GitHub MCP",
            url="https://api.githubcopilot.com/mcp/",
            approval_mode="always_require",  # Require approval for GitHub operations
            headers={"Authorization": "Bearer github-token"},
        )

        # Create agent with multiple MCP tools
        async with Agent(
            client=client,
            name="MultiToolAgent",
            instructions="You can search documentation and access GitHub repositories.",
            tools=[learn_mcp, github_mcp],
        ) as agent:
            result = await agent.run(
                "Find Azure documentation and also check the latest commits in microsoft/semantic-kernel"
            )
            print(result.text)

if __name__ == "__main__":
    asyncio.run(multi_tool_mcp_example())
```

The Python Agent Framework provides seamless integration with Foundry's hosted MCP capabilities, enabling secure and scalable access to external tools while maintaining the flexibility and control needed for production applications.

> [!TIP]
> MCP tools can also be bundled into **Microsoft Foundry Toolbox** configurations — named, versioned server-side collections of hosted tools. See [Microsoft Foundry Toolbox](../../integrations/by-component/tools/foundry-toolbox.md) for managed-agent attachment and MCP consumption guidance.

### Complete example

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio
import os

from agent_framework import Agent
from agent_framework.openai import OpenAIChatClient
from dotenv import load_dotenv

"""
MCP GitHub Integration with Personal Access Token (PAT)

This example demonstrates how to connect to GitHub's remote MCP server using a Personal Access
Token (PAT) for authentication. The agent can use GitHub operations like searching repositories,
reading files, creating issues, and more depending on how you scope your token.

Prerequisites:
1. A GitHub Personal Access Token with appropriate scopes
   - Create one at: https://github.com/settings/tokens
   - For read-only operations, you can use more restrictive scopes
2. Environment variables:
   - GITHUB_PAT: Your GitHub Personal Access Token (required)
   - OPENAI_API_KEY: Your OpenAI API key (required)
   - OPENAI_MODEL: Your OpenAI model ID (required)
"""


async def github_mcp_example() -> None:
    """Example of using GitHub MCP server with PAT authentication."""
    # 1. Load environment variables from .env file if present
    load_dotenv()

    # 2. Get configuration from environment
    github_pat = os.getenv("GITHUB_PAT")
    if not github_pat:
        raise ValueError(
            "GITHUB_PAT environment variable must be set. Create a token at https://github.com/settings/tokens"
        )

    # 3. Create authentication headers with GitHub PAT
    auth_headers = {
        "Authorization": f"Bearer {github_pat}",
    }

    # 4. Create agent with the GitHub MCP tool using instance method
    # The MCP tool manages the connection to the MCP server and makes its tools available
    # Set approval_mode="never_require" to allow the MCP tool to execute without approval
    client = OpenAIChatClient()
    # This hosted MCP tool is executed remotely by OpenAI, not locally by your application.
    github_mcp_tool = client.get_mcp_tool(
        name="GitHub",
        url="https://api.githubcopilot.com/mcp/",
        headers=auth_headers,
        approval_mode="never_require",
    )

    # 5. Create agent with the GitHub MCP tool
    async with Agent(
        client=client,
        name="GitHubAgent",
        instructions=(
            "You are a helpful assistant that can help users interact with GitHub. "
            "You can search for repositories, read file contents, check issues, and more. "
            "Always be clear about what operations you're performing."
        ),
        tools=github_mcp_tool,
    ) as agent:
        # Example 1: Get authenticated user information
        query1 = "What is my GitHub username and tell me about my account?"
        print(f"\nUser: {query1}")
        result1 = await agent.run(query1)
        print(f"Agent: {result1.text}")

        # Example 2: List my repositories
        query2 = "List all the repositories I own on GitHub"
        print(f"\nUser: {query2}")
        result2 = await agent.run(query2)
        print(f"Agent: {result2.text}")


if __name__ == "__main__":
    asyncio.run(github_mcp_example())
```

::: zone-end

::: zone pivot="programming-language-go"
## Hosted MCP tools

The `hostedtool` package provides marker types for hosted tools. These tools are not executed locally — they inform the AI service that it's allowed to call the configured MCP server on the service side. In Go, use hosted MCP tools with the OpenAI Responses API through `openaiprovider.NewResponsesAgent`.

### Environment setup

Configure the model and MCP server endpoint through environment variables:

```go
endpoint := os.Getenv("MCP_SERVER_URL")
if endpoint == "" {
    endpoint = "https://learn.microsoft.com/api/mcp"
}

deployment := os.Getenv("OPENAI_RESPONSES_MODEL")
if deployment == "" {
    deployment = "gpt-4o-mini"
}
```

### Basic MCP integration

```go
import (
    "os"

    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/openaiprovider"
    "github.com/microsoft/agent-framework-go/tool"
    "github.com/microsoft/agent-framework-go/tool/hostedtool"
)

mcpTool := &hostedtool.MCPServer{
    ServerName:        "microsoft_learn",
    ServerDescription: "Search Microsoft Learn documentation.",
    ServerAddress:     endpoint,
    AllowedTools:      []string{"microsoft_docs_search"},
}

a := openaiprovider.NewResponsesAgent(client, openaiprovider.AgentConfig{
    Model:        deployment,
    Instructions: "You answer questions by searching Microsoft Learn content only.",
    Config: agent.Config{
        Name:  "MicrosoftLearnAgent",
        Tools: []tool.Tool{mcpTool},
    },
})

resp, err := a.RunText(ctx, "Summarize the Azure AI Agent documentation for MCP tool calling.").Collect()
```

### Authenticated MCP servers

For MCP servers that require authentication, set `Authorization` or provide headers. Load secrets from your application's secret store or environment, and avoid checking them into source control.

```go
githubMCPTool := &hostedtool.MCPServer{
    ServerName:    "github",
    ServerAddress: "https://api.githubcopilot.com/mcp/",
    Authorization: "Bearer " + os.Getenv("GITHUB_PAT"),
}
```

### Multiple MCP servers

Provide multiple hosted MCP server declarations when the model should be able to choose between different remote tool sets:

```go
tools := []tool.Tool{
    &hostedtool.MCPServer{
        ServerName:    "microsoft_learn",
        ServerAddress: "https://learn.microsoft.com/api/mcp",
        AllowedTools:  []string{"microsoft_docs_search"},
    },
    &hostedtool.MCPServer{
        ServerName:    "github",
        ServerAddress: "https://api.githubcopilot.com/mcp/",
        Authorization: "Bearer " + os.Getenv("GITHUB_PAT"),
    },
}

a := openaiprovider.NewResponsesAgent(client, openaiprovider.AgentConfig{
    Model:        deployment,
    Instructions: "You can search Microsoft documentation and GitHub repositories.",
    Config: agent.Config{
        Name:  "MultiToolAgent",
        Tools: tools,
    },
})
```

> [!NOTE]
> Hosted MCP tools require a provider that supports them, such as the OpenAI Responses API through `openaiprovider.NewResponsesAgent`.

::: zone-end
## Next steps

> [!div class="nextstepaction"]
> [Local MCP Tools](./local-mcp-tools.md)
