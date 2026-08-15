---
title: "Step 7: Host Your Agent"
description: "Deploy your agent so users and other agents can interact with it."
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: tutorial
ms.author: edvan
ms.date: 07/08/2026
ms.service: agent-framework
---

# Step 7: Host Your Agent

Once you've built your agent, you need to host it so users and other agents can interact with it.

## Hosting Options

| Option | Description | Best For |
|--------|-------------|----------|
| [A2A Protocol](../hosting/self-hosting/a2a/server.md) | Expose agents via the Agent-to-Agent protocol | Multi-agent systems |
| [OpenAI-Compatible Endpoints](../hosting/self-hosting/openai-endpoints.md) | Expose agents via Chat Completions or Responses APIs | OpenAI-compatible clients |
| [Durable Extension](../hosting/azure-functions.md) | Make C# and Python agents and workflows durable on Azure Functions or self-hosted compute | Long-running, reliable workloads |
| [AG-UI Protocol](../integrations/by-component/ui/ag-ui/index.md) | Build web-based AI agent applications | Web frontends |

:::zone pivot="programming-language-csharp"

## Hosting in ASP.NET Core

The Agent Framework provides hosting libraries that enable you to integrate AI agents into ASP.NET Core applications. These libraries simplify registering, configuring, and exposing agents through various protocols.

As described in [Agents](../concepts/agents/index.md), `AIAgent` is the fundamental agent abstraction in Agent Framework. It defines an "LLM wrapper" that processes user inputs, makes decisions, calls tools, and performs additional work to execute actions and generate responses. Exposing AI agents from your ASP.NET Core application is not trivial. The hosting libraries solve this by registering AI agents in a dependency injection container, allowing you to resolve and use them in your application services. They also enable you to manage agent dependencies, such as tools and session storage, from the same container. Agents can be hosted alongside your application infrastructure, independent of the protocols they use. Similarly, workflows can be hosted and leverage your application's common infrastructure.

### Core Hosting Library

The `Microsoft.Agents.AI.Hosting` library is the foundation for hosting AI agents in ASP.NET Core. It provides extensions for `IHostApplicationBuilder` to register and configure AI agents and workflows. In ASP.NET Core, `IHostApplicationBuilder` is the fundamental type that represents the builder for hosted applications and services, managing configuration, logging, lifetime, and more.

Before configuring agents or workflows, register an `IChatClient` in the dependency injection container. In the examples below, it is registered as a keyed singleton under the name `chat-model`:

```csharp
// endpoint is your Microsoft Foundry project endpoint
// deploymentName is 'gpt-4o-mini' for example

IChatClient chatClient = new AIProjectClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName);
builder.Services.AddSingleton(chatClient);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

#### AddAIAgent

Register an AI agent with dependency injection:

```csharp
var pirateAgent = builder.AddAIAgent(
    "pirate",
    instructions: "You are a pirate. Speak like a pirate",
    description: "An agent that speaks like a pirate.",
    chatClientServiceKey: "chat-model");
```

The `AddAIAgent()` method returns an `IHostedAgentBuilder`, which provides extension methods for configuring the agent. For example, you can add tools to the agent:

```csharp
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate")
    .WithAITool(new MyTool()); // MyTool is a custom type derived from AITool
```

You can also configure the session store (storage for conversation data):

```csharp
var pirateAgent = builder.AddAIAgent("pirate", instructions: "You are a pirate. Speak like a pirate")
    .WithInMemorySessionStore();
```

#### AddWorkflow

Register workflows that coordinate multiple agents. A workflow is essentially a "graph" where each node is an `AIAgent`, and the agents communicate with each other.

In this example, two agents work sequentially. The user input is first sent to `agent-1`, which produces a response and sends it to `agent-2`. The workflow then outputs the final response. There is also a `BuildConcurrent` method that creates a concurrent agent workflow.

```csharp
builder.AddAIAgent("agent-1", instructions: "you are agent 1!");
builder.AddAIAgent("agent-2", instructions: "you are agent 2!");

var workflow = builder.AddWorkflow("my-workflow", (sp, key) =>
{
    var agent1 = sp.GetRequiredKeyedService<AIAgent>("agent-1");
    var agent2 = sp.GetRequiredKeyedService<AIAgent>("agent-2");
    return AgentWorkflowBuilder.BuildSequential(key, [agent1, agent2]);
});
```

#### Expose Workflow as AIAgent

To use protocol integrations (such as A2A or OpenAI) with a workflow, convert it into a standalone agent. Currently, workflows do not provide similar integration capabilities on their own, so this conversion step is required:

```csharp
var workflowAsAgent = builder
    .AddWorkflow("science-workflow", (sp, key) => { ... })
    .AddAsAIAgent();  // Now the workflow can be used as an agent
```

### Implementation Details

The hosting libraries act as protocol adapters that bridge external communication protocols and the Agent Framework's internal `AIAgent` implementation. When you use a hosting integration library, the library retrieves the registered `AIAgent` from dependency injection, wraps it with protocol-specific middleware to translate incoming requests and outgoing responses, and invokes the `AIAgent` to process requests. This architecture keeps your agent implementation protocol-agnostic.

For example, using the ASP.NET Core hosting library with the A2A protocol adapter:

```csharp
// Register the agent
var pirateAgent = builder.AddAIAgent("pirate",
    instructions: "You are a pirate. Speak like a pirate",
    description: "An agent that speaks like a pirate.");

// Expose via a protocol (e.g. A2A)
builder.Services.AddA2AServer();
var app = builder.Build();
app.MapA2AServer();
app.Run();
```

> [!TIP]
> See the [Durable Agents samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/04-hosting/DurableAgents) for Azure Functions and self-hosted examples.

:::zone-end

:::zone pivot="programming-language-python"

Azure Functions is one self-managed hosting option. For a comparison of Microsoft-managed Foundry Hosted Agents, self-hosting, and durable Azure Functions workloads, see [Hosting Agent Framework applications](../hosting/index.md).

Install the Azure Functions hosting package, Foundry client, and Azure authentication package:

```bash
pip install agent-framework-azurefunctions agent-framework-foundry azure-identity
```

Create an agent:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/azure_functions/01_single_agent/function_app.py" range="24-35" highlight="4-9":::

Register the agent with `AgentFunctionApp`:

:::code language="python" source="~/../agent-framework-code/python/samples/04-hosting/azure_functions/01_single_agent/function_app.py" range="38-39" highlight="2":::

Run locally with [Azure Functions Core Tools](/azure/azure-functions/functions-run-local):

```bash
az login
pip install -r requirements.txt
# Start Azurite and copy local.settings.json.template to local.settings.json first.
func start
```

Then invoke:

```bash
curl -X POST http://localhost:7071/api/agents/Joker/run \
  -H "Content-Type: text/plain" \
  -d "Tell me a short joke about cloud computing."
```

> [!TIP]
> See the [full sample](https://github.com/microsoft/agent-framework/blob/main/python/samples/04-hosting/azure_functions/01_single_agent/function_app.py) for the complete runnable file, and the [Azure Functions hosting samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/04-hosting/azure_functions) for more patterns.

:::zone-end

:::zone pivot="programming-language-go"

## Hosting with A2A Protocol

The Go port provides A2A hosting through `a2aprovider`, which wraps an agent in an HTTP handler compatible with the Agent-to-Agent protocol.

> [!NOTE]
> Durable Extension hosting isn't currently available for Go. For the latest Go SDK status, see the [Agent Framework Go repository](https://github.com/microsoft/agent-framework-go).

Create an agent:

```go
import (
    "github.com/microsoft/agent-framework-go/agent"
    "github.com/microsoft/agent-framework-go/provider/a2aprovider"
    "github.com/microsoft/agent-framework-go/provider/foundryprovider"

    "github.com/a2aproject/a2a-go/v2/a2a"
    "github.com/a2aproject/a2a-go/v2/a2asrv"
)

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Instructions: "You are a helpful assistant.",
    Config: agent.Config{
    },
})
```

Expose the agent via A2A:

```go
url := "http://localhost:5000"
card := &a2a.AgentCard{
    Name:               "MyAgent",
    Description:        "A helpful assistant.",
    Version:            "1.0.0",
    DefaultInputModes:  []string{"text"},
    DefaultOutputModes: []string{"text"},
    Capabilities:       a2a.AgentCapabilities{Streaming: false},
    SupportedInterfaces: []*a2a.AgentInterface{
        a2a.NewAgentInterface(url, a2a.TransportProtocolJSONRPC),
    },
}

mux := http.NewServeMux()
requestHandler := a2asrv.NewHandler(
    a2aprovider.NewExecutor(a, a2aprovider.ExecutorConfig{}),
    a2asrv.WithExtendedAgentCard(card),
)
mux.Handle("/", a2asrv.NewJSONRPCHandler(requestHandler))
mux.Handle(a2asrv.WellKnownAgentCardPath, a2asrv.NewStaticAgentCardHandler(card))

log.Println("A2A server listening on :5000")
http.ListenAndServe(":5000", mux)
```

> [!TIP]
> See the [full A2A client-server sample](https://github.com/microsoft/agent-framework-go/tree/main/examples/05-end-to-end/a2a_client_server) for a complete runnable example.

:::zone-end

## Next steps

> [!div class="nextstepaction"]
> [Agents](../concepts/agents/index.md)

**Go deeper:**

- [A2A agent service](../integrations/by-component/agent-services/a2a.md) — consume remote A2A agents
- [A2A hosting](../hosting/self-hosting/a2a/server.md) — expose Agent Framework agents through A2A
- [Durable Extension](../hosting/azure-functions.md) — durable C# and Python agent and workflow hosting
- [AG-UI Protocol](../integrations/by-component/ui/ag-ui/index.md) — web-based agent UIs
- [Hosting overview](../hosting/index.md) — choose Foundry Hosted Agents, self-hosting, or durable hosting
- [Foundry Hosted Agents docs](/azure/ai-foundry/agents/concepts/hosted-agents) — understand hosted agents in Microsoft Foundry
- [Foundry Hosted Agents sample (Python)](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/python/hosted-agents/agent-framework) — run an end-to-end Agent Framework hosted-agent sample

## See also

- [Agents](../concepts/agents/index.md)
- [Workflows](../concepts/workflows/index.md)
