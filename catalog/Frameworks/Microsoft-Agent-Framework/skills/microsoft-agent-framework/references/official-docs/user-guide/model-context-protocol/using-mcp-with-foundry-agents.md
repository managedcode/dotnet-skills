---
title: MCP and Foundry Agents
description: Using MCP with Foundry Agents
zone_pivot_groups: programming-languages
author: markwallace
ms.topic: reference
ms.author: markwallace
ms.date: 09/24/2025
ms.service: agent-framework
---

# Using MCP tools with Foundry Agents

You can extend the capabilities of your Azure AI Foundry agent by connecting it to tools hosted on remote [Model Context Protocol (MCP)](/azure/ai-foundry/agents/how-to/tools/model-context-protocol) servers (bring your own MCP server endpoint).

## How to use the Model Context Protocol tool

This section explains how to create an AI agent using Azure Foundry (Azure AI) with a hosted Model Context Protocol (MCP) server integration. The agent can utilize MCP tools that are managed and executed by the Azure Foundry service, allowing for secure and controlled access to external resources.

### Key Features

- **Hosted MCP Server**: The MCP server is hosted and managed by Azure AI Foundry, eliminating the need to manage server infrastructure
- **Persistent Agents**: Agents are created and stored server-side, allowing for stateful conversations
- **Tool Approval Workflow**: Configurable approval mechanisms for MCP tool invocations

### How It Works

::: zone pivot="programming-language-csharp"

#### 1. Environment Setup

The sample requires two environment variables:
- `AZURE_FOUNDRY_PROJECT_ENDPOINT`: Your Azure AI Foundry project endpoint URL
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

#### 4. Persistent Agent Creation

The agent is created server-side using the Azure AI Foundry Persistent Agents SDK:

```csharp
var persistentAgentsClient = new PersistentAgentsClient(endpoint, new AzureCliCredential());

var agentMetadata = await persistentAgentsClient.Administration.CreateAgentAsync(
    model: model,
    name: AgentName,
    instructions: AgentInstructions,
    tools: [mcpTool]);
```

This creates a persistent agent that:
- Lives on the Azure AI Foundry service
- Has access to the specified MCP tools
- Can maintain conversation state across multiple interactions

#### 5. Agent Retrieval and Execution

The created agent is retrieved as an `AIAgent` instance:

```csharp
AIAgent agent = await persistentAgentsClient.GetAIAgentAsync(agentMetadata.Value.Id);
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
AgentThread thread = await agent.GetNewThreadAsync();
var response = await agent.RunAsync(
    "Please summarize the Azure AI Agent documentation related to MCP Tool calling?", 
    thread, 
    runOptions);
Console.WriteLine(response);
```

#### 8. Cleanup

The sample demonstrates proper resource cleanup:

```csharp
await persistentAgentsClient.Administration.DeleteAgentAsync(agent.Id);
```

::: zone-end
::: zone pivot="programming-language-python"

## Python Azure AI Foundry MCP Integration

Azure AI Foundry provides seamless integration with Model Context Protocol (MCP) servers through the Python Agent Framework. The service manages the MCP server hosting and execution, eliminating infrastructure management while providing secure, controlled access to external tools.

### Environment Setup

Configure your Azure AI Foundry project credentials through environment variables:

```python
import os
from azure.identity.aio import AzureCliCredential
from agent_framework.azure import AzureAIAgentClient

# Required environment variables
os.environ["AZURE_AI_PROJECT_ENDPOINT"] = "https://<your-project>.services.ai.azure.com/api/projects/<project-id>"
os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = "gpt-4o-mini"  # Optional, defaults to this
```

### Basic MCP Integration

Create an Azure AI Foundry agent with hosted MCP tools:

```python
import asyncio
from agent_framework import HostedMCPTool
from agent_framework.azure import AzureAIAgentClient
from azure.identity.aio import AzureCliCredential

async def basic_foundry_mcp_example():
    """Basic example of Azure AI Foundry agent with hosted MCP tools."""
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(async_credential=credential) as chat_client,
    ):
        # Enable Azure AI observability (optional but recommended)
        await chat_client.setup_azure_ai_observability()
        
        # Create agent with hosted MCP tool
        agent = chat_client.as_agent(
            name="MicrosoftLearnAgent", 
            instructions="You answer questions by searching Microsoft Learn content only.",
            tools=HostedMCPTool(
                name="Microsoft Learn MCP",
                url="https://learn.microsoft.com/api/mcp",
            ),
        )
        
        # Simple query without approval workflow
        result = await agent.run(
            "Please summarize the Azure AI Agent documentation related to MCP tool calling?"
        )
        print(result)

if __name__ == "__main__":
    asyncio.run(basic_foundry_mcp_example())
```

### Multi-Tool MCP Configuration

Use multiple hosted MCP tools with a single agent:

```python
async def multi_tool_mcp_example():
    """Example using multiple hosted MCP tools."""
    async with (
        AzureCliCredential() as credential,
        AzureAIAgentClient(async_credential=credential) as chat_client,
    ):
        await chat_client.setup_azure_ai_observability()
        
        # Create agent with multiple MCP tools
        agent = chat_client.as_agent(
            name="MultiToolAgent",
            instructions="You can search documentation and access GitHub repositories.",
            tools=[
                HostedMCPTool(
                    name="Microsoft Learn MCP",
                    url="https://learn.microsoft.com/api/mcp",
                    approval_mode="never_require",  # Auto-approve documentation searches
                ),
                HostedMCPTool(
                    name="GitHub MCP", 
                    url="https://api.github.com/mcp",
                    approval_mode="always_require",  # Require approval for GitHub operations
                    headers={"Authorization": "Bearer github-token"},
                ),
            ],
        )
        
        result = await agent.run(
            "Find Azure documentation and also check the latest commits in microsoft/semantic-kernel"
        )
        print(result)

if __name__ == "__main__":
    asyncio.run(multi_tool_mcp_example())
```

The Python Agent Framework provides seamless integration with Azure AI Foundry's hosted MCP capabilities, enabling secure and scalable access to external tools while maintaining the flexibility and control needed for production applications.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Using workflows as Agents](../workflows/as-agents.md)
