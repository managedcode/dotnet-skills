---
title: Orchestrate durable agents
description: Learn how to orchestrate multiple durable AI agents with fan-out/fan-in patterns for concurrent processing
zone_pivot_groups: programming-languages
author: anthonychu
ms.topic: tutorial
ms.author: antchu
ms.date: 11/07/2025
ms.service: agent-framework
---

# Orchestrate durable agents

This tutorial shows you how to orchestrate multiple durable AI agents using the fan-out/fan-in patterns. You'll extend the durable agent from the [Create and run a durable agent](create-and-run-durable-agent.md) tutorial to create a multi-agent system that processes a user's question, then translates the response into multiple languages concurrently.

This orchestration pattern demonstrates how to:
- Reuse the durable agent from the first tutorial.
- Create additional durable agents for language translation.
- Fan out to multiple agents for concurrent processing.
- Fan in results and return them as structured JSON.

## Prerequisites

Before you begin, you must complete the [Create and run a durable agent](create-and-run-durable-agent.md) tutorial. This tutorial extends the project created in that tutorial by adding orchestration capabilities.

## Understanding the orchestration pattern

The orchestration you'll build follows this flow:

1. **User input** - A question or message from the user
2. **Main agent** - The `MyDurableAgent` from the first tutorial processes the question
3. **Fan-out** - The main agent's response is sent concurrently to both translation agents
4. **Translation agents** - Two specialized agents translate the response (French and Spanish)
5. **Fan-in** - Results are aggregated into a single JSON response with the original response and translations

This pattern enables concurrent processing, reducing total response time compared to sequential translation.

## Register agents at startup

To properly use agents in durable orchestrations, register them at application startup. They can be used across orchestration executions.

::: zone pivot="programming-language-csharp"

Update your `Program.cs` to register the translation agents alongside the existing `MyDurableAgent`:

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;

// Get the Azure OpenAI configuration
string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? "gpt-4o-mini";

// Create the Azure OpenAI client
AzureOpenAIClient client = new(new Uri(endpoint), new DefaultAzureCredential());
ChatClient chatClient = client.GetChatClient(deploymentName);

// Create the main agent from the first tutorial
AIAgent mainAgent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant that can answer questions and provide information.",
    name: "MyDurableAgent");

// Create translation agents
AIAgent frenchAgent = chatClient.AsAIAgent(
    instructions: "You are a translator. Translate the following text to French. Return only the translation, no explanations.",
    name: "FrenchTranslator");

AIAgent spanishAgent = chatClient.AsAIAgent(
    instructions: "You are a translator. Translate the following text to Spanish. Return only the translation, no explanations.",
    name: "SpanishTranslator");

// Build and configure the Functions host
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
    {
        // Register all agents for use in orchestrations and HTTP endpoints
        options.AddAIAgent(mainAgent);
        options.AddAIAgent(frenchAgent);
        options.AddAIAgent(spanishAgent);
    })
    .Build();

app.Run();
```

This setup:
- Keeps the original `MyDurableAgent` from the first tutorial.
- Creates two new translation agents (French and Spanish).
- Registers all three agents with the Durable Task framework using `options.AddAIAgent()`.
- Makes agents available throughout the application lifetime for individual interactions and orchestrations.

::: zone-end

::: zone pivot="programming-language-python"

Update your `function_app.py` to register the translation agents alongside the existing `MyDurableAgent`:

```python
import os
from azure.identity import DefaultAzureCredential
from agent_framework.azure import AzureOpenAIChatClient, AgentFunctionApp

# Get the Azure OpenAI configuration
endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
if not endpoint:
    raise ValueError("AZURE_OPENAI_ENDPOINT is not set.")
deployment_name = os.getenv("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")

# Create the Azure OpenAI client
chat_client = AzureOpenAIChatClient(
    endpoint=endpoint,
    deployment_name=deployment_name,
    credential=DefaultAzureCredential()
)

# Create the main agent from the first tutorial
main_agent = chat_client.as_agent(
    instructions="You are a helpful assistant that can answer questions and provide information.",
    name="MyDurableAgent"
)

# Create translation agents
french_agent = chat_client.as_agent(
    instructions="You are a translator. Translate the following text to French. Return only the translation, no explanations.",
    name="FrenchTranslator"
)

spanish_agent = chat_client.as_agent(
    instructions="You are a translator. Translate the following text to Spanish. Return only the translation, no explanations.",
    name="SpanishTranslator"
)

# Create the function app and register all agents
app = AgentFunctionApp(agents=[main_agent, french_agent, spanish_agent])
```

This setup:
- Keeps the original `MyDurableAgent` from the first tutorial.
- Creates two new translation agents (French and Spanish).
- Registers all three agents with the Durable Task framework using `AgentFunctionApp(agents=[...])`.
- Makes agents available throughout the application lifetime for individual interactions and orchestrations.

::: zone-end

## Create an orchestration function

An orchestration function coordinates the workflow across multiple agents. It retrieves registered agents from the durable context and orchestrates their execution, first calling the main agent, then fanning out to translation agents concurrently.

::: zone pivot="programming-language-csharp"

Create a new file named `AgentOrchestration.cs` in your project directory:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace MyDurableAgent;

public static class AgentOrchestration
{
    // Define a strongly-typed response structure for agent outputs
    public sealed record TextResponse(string Text);

    [Function("agent_orchestration_workflow")]
    public static async Task<Dictionary<string, string>> AgentOrchestrationWorkflow(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<string>() ?? throw new ArgumentNullException(nameof(context), "Input cannot be null");
        
        // Step 1: Get the main agent's response
        DurableAIAgent mainAgent = context.GetAgent("MyDurableAgent");
        AgentResponse<TextResponse> mainResponse = await mainAgent.RunAsync<TextResponse>(input);
        string agentResponse = mainResponse.Result.Text;

        // Step 2: Fan out - get the translation agents and run them concurrently
        DurableAIAgent frenchAgent = context.GetAgent("FrenchTranslator");
        DurableAIAgent spanishAgent = context.GetAgent("SpanishTranslator");

        Task<AgentResponse<TextResponse>> frenchTask = frenchAgent.RunAsync<TextResponse>(agentResponse);
        Task<AgentResponse<TextResponse>> spanishTask = spanishAgent.RunAsync<TextResponse>(agentResponse);

        // Step 3: Wait for both translation tasks to complete (fan-in)
        await Task.WhenAll(frenchTask, spanishTask);

        // Get the translation results
        TextResponse frenchResponse = (await frenchTask).Result;
        TextResponse spanishResponse = (await spanishTask).Result;

        // Step 4: Combine results into a dictionary
        var result = new Dictionary<string, string>
        {
            ["original"] = agentResponse,
            ["french"] = frenchResponse.Text,
            ["spanish"] = spanishResponse.Text
        };

        return result;
    }
}
```

This orchestration demonstrates the proper durable task pattern:
- **Main agent execution**: First calls `MyDurableAgent` to process the user's input.
- **Agent retrieval**: Uses `context.GetAgent()` to get registered agents by name (agents were registered at startup).
- **Sequential then concurrent**: Main agent runs first, then translation agents run concurrently using `Task.WhenAll`.

::: zone-end

::: zone pivot="programming-language-python"

Add the orchestration function to your `function_app.py` file:

```python
import azure.durable_functions as df

@app.orchestration_trigger(context_name="context")
def agent_orchestration_workflow(context: df.DurableOrchestrationContext):
    """
    Orchestration function that coordinates multiple agents.
    Returns a dictionary with the original response and translations.
    """
    input_text = context.get_input()

    # Step 1: Get the main agent's response
    main_agent = app.get_agent(context, "MyDurableAgent")
    main_response = yield main_agent.run(input_text)
    agent_response = main_response.text

    # Step 2: Fan out - get the translation agents and run them concurrently
    french_agent = app.get_agent(context, "FrenchTranslator")
    spanish_agent = app.get_agent(context, "SpanishTranslator")

    parallel_tasks = [
        french_agent.run(agent_response),
        spanish_agent.run(agent_response)
    ]

    # Step 3: Wait for both translation tasks to complete (fan-in)
    translations = yield context.task_all(parallel_tasks) # type: ignore

    # Step 4: Combine results into a dictionary
    result = {
        "original": agent_response,
        "french": translations[0].text,
        "spanish": translations[1].text
    }

    return result
```

This orchestration demonstrates the proper durable task pattern:
- **Main agent execution**: First calls `MyDurableAgent` to process the user's input.
- **Agent retrieval**: Uses `app.get_agent(context, "AgentName")` to get registered agents by name (agents were registered at startup).
- **Sequential then concurrent**: Main agent runs first, then translation agents run concurrently using `context.task_all`.

::: zone-end

## Test the orchestration

Ensure your local development dependencies from the first tutorial are still running:
- **Azurite** in one terminal window
- **Durable Task Scheduler emulator** in another terminal window

If you've stopped them, restart them now following the instructions in the [Create and run a durable agent](create-and-run-durable-agent.md#start-local-development-dependencies) tutorial.

With your local development dependencies running:

1. Start your Azure Functions app in a new terminal window:

   ```console
   func start
   ```

1. The Durable Functions extension automatically creates built-in HTTP endpoints for managing orchestrations. Start the orchestration using the built-in API:

   # [Bash](#tab/bash)

   ```bash
   curl -X POST http://localhost:7071/runtime/webhooks/durabletask/orchestrators/agent_orchestration_workflow \
     -H "Content-Type: application/json" \
     -d '"\"What are three popular programming languages?\""'
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   $body = '"What are three popular programming languages?"'
   Invoke-RestMethod -Method Post -Uri "http://localhost:7071/runtime/webhooks/durabletask/orchestrators/agent_orchestration_workflow" `
     -ContentType "application/json" `
     -Body $body
   ```

   ---

1. The response includes URLs for managing the orchestration instance:

   ```json
   {
     "id": "abc123def456",
     "statusQueryGetUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456",
     "sendEventPostUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456/raiseEvent/{eventName}",
     "terminatePostUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456/terminate",
     "purgeHistoryDeleteUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456"
   }
   ```

1. Query the orchestration status using the `statusQueryGetUri` (replace `abc123def456` with your actual instance ID):

   # [Bash](#tab/bash)

   ```bash
   curl http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   Invoke-RestMethod -Uri "http://localhost:7071/runtime/webhooks/durabletask/instances/abc123def456"
   ```

   ---

1. Initially, the orchestration will be running:

   ```json
   {
     "name": "agent_orchestration_workflow",
     "instanceId": "abc123def456",
     "runtimeStatus": "Running",
     "input": "What are three popular programming languages?",
     "createdTime": "2025-11-07T10:00:00Z",
     "lastUpdatedTime": "2025-11-07T10:00:05Z"
   }
   ```

1. Poll the status endpoint until `runtimeStatus` is `Completed`. When complete, you'll see the orchestration output with the main agent's response and its translations:

   ```json
   {
     "name": "agent_orchestration_workflow",
     "instanceId": "abc123def456",
     "runtimeStatus": "Completed",
     "output": {
       "original": "Three popular programming languages are Python, JavaScript, and Java. Python is known for its simplicity...",
       "french": "Trois langages de programmation populaires sont Python, JavaScript et Java. Python est connu pour sa simplicité...",
       "spanish": "Tres lenguajes de programación populares son Python, JavaScript y Java. Python es conocido por su simplicidad..."
     }
   }
   ```

Note that the `original` field contains the response from `MyDurableAgent`, not the original user input. This demonstrates how the orchestration flows from the main agent to the translation agents.

## Monitor the orchestration in the dashboard

The Durable Task Scheduler dashboard provides visibility into your orchestration:

1. Open `http://localhost:8082` in your browser.

1. Select the "default" task hub.

1. Select the "Orchestrations" tab.

1. Find your orchestration instance in the list.

1. Select the instance to see:
   - The orchestration timeline
   - Main agent execution followed by concurrent translation agents
   - Each agent execution (MyDurableAgent, then French and Spanish translators)
   - Fan-out and fan-in patterns visualized
   - Timing and duration for each step

## Understanding the benefits

This orchestration pattern provides several advantages:

### Concurrent processing

The translation agents run in parallel, significantly reducing total response time compared to sequential execution. The main agent runs first to generate a response, then both translations happen concurrently.

- **.NET**: Uses `Task.WhenAll` to await multiple agent tasks simultaneously.
- **Python**: Uses `context.task_all` to execute multiple agent runs concurrently.

### Durability and reliability

The orchestration state is persisted by the Durable Task Scheduler. If an agent execution fails or times out, the orchestration can retry that specific step without restarting the entire workflow.

### Scalability

The Azure Functions Flex Consumption plan can scale out to hundreds of instances to handle concurrent translations across many orchestration instances.

## Deploy to Azure

Now that you've tested the orchestration locally, deploy the updated application to Azure.

1. Deploy the updated application using Azure Developer CLI:

   ```console
   azd deploy
   ```

   This deploys your updated code with the new orchestration function and additional agents to the Azure Functions app created in the first tutorial.

1. Wait for the deployment to complete.

## Test the deployed orchestration

After deployment, test your orchestration running in Azure.

1. Get the system key for the durable extension:

   # [Bash](#tab/bash)

   ```bash
   SYSTEM_KEY=$(az functionapp keys list --name $(azd env get-value AZURE_FUNCTION_NAME) --resource-group $(azd env get-value AZURE_RESOURCE_GROUP) --query "systemKeys.durabletask_extension" -o tsv)
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   $functionName = azd env get-value AZURE_FUNCTION_NAME
   $resourceGroup = azd env get-value AZURE_RESOURCE_GROUP
   $SYSTEM_KEY = (az functionapp keys list --name $functionName --resource-group $resourceGroup --query "systemKeys.durabletask_extension" -o tsv)
   ```

   ---

1. Start the orchestration using the built-in API:

   # [Bash](#tab/bash)

   ```bash
   curl -X POST "https://$(azd env get-value AZURE_FUNCTION_NAME).azurewebsites.net/runtime/webhooks/durabletask/orchestrators/agent_orchestration_workflow?code=$SYSTEM_KEY" \
     -H "Content-Type: application/json" \
     -d '"\"What are three popular programming languages?\""'
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   $functionName = azd env get-value AZURE_FUNCTION_NAME
   $body = '"What are three popular programming languages?"'
   Invoke-RestMethod -Method Post -Uri "https://$functionName.azurewebsites.net/runtime/webhooks/durabletask/orchestrators/agent_orchestration_workflow?code=$SYSTEM_KEY" `
     -ContentType "application/json" `
     -Body $body
   ```

   ---

1. Use the `statusQueryGetUri` from the response to poll for completion and view the results with translations.

## Next steps

Now that you understand durable agent orchestration, you can explore more advanced patterns:

- **Sequential orchestrations** - Chain agents where each depends on the previous output.
- **Conditional branching** - Route to different agents based on content.
- **Human-in-the-loop** - Pause orchestration for human approval.
- **External events** - Trigger orchestration steps from external systems.

Additional resources:

- [Durable Task Scheduler Overview](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler)
- [Durable Functions patterns and concepts](/azure/azure-functions/durable/durable-functions-overview?tabs=in-process%2Cnodejs-v3%2Cv1-model&pivots=csharp)
