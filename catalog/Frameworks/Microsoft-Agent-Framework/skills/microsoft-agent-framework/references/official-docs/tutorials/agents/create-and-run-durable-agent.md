---
title: Create and run a durable agent
description: Learn how to create and run a durable AI agent with Azure Functions and the durable task extension for Microsoft Agent Framework
zone_pivot_groups: programming-languages
author: anthonychu
ms.topic: tutorial
ms.author: antchu
ms.date: 11/05/2025
ms.service: agent-framework
---

# Create and run a durable agent

This tutorial shows you how to create and run a [durable AI agent](../../user-guide/agents/agent-types/durable-agent/create-durable-agent.md) using the durable task extension for Microsoft Agent Framework. You'll build an Azure Functions app that hosts a stateful agent with built-in HTTP endpoints, and learn how to monitor it using the Durable Task Scheduler dashboard.

Durable agents provide serverless hosting with automatic state management, allowing your agents to maintain conversation history across multiple interactions without managing infrastructure.

## Prerequisites

Before you begin, ensure you have the following prerequisites:

::: zone pivot="programming-language-csharp"

- [.NET 9.0 SDK or later](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4.x](/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- [Azure Developer CLI (azd)](/azure/developer/azure-developer-cli/install-azd)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated](/cli/azure/authenticate-azure-cli)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running (for local development with Azurite and the Durable Task Scheduler emulator)
- An Azure subscription with permissions to create resources

> [!NOTE]
> Microsoft Agent Framework is supported with all actively supported versions of .NET. For the purposes of this sample, we recommend the .NET 9 SDK or a later version.

::: zone-end

::: zone pivot="programming-language-python"

- [Python 3.10 or later](https://www.python.org/downloads/)
- [Azure Functions Core Tools v4.x](/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- [Azure Developer CLI (azd)](/azure/developer/azure-developer-cli/install-azd)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated](/cli/azure/authenticate-azure-cli)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running (for local development with Azurite and the Durable Task Scheduler emulator)
- An Azure subscription with permissions to create resources

::: zone-end

## Download the quickstart project

Use Azure Developer CLI to initialize a new project from the durable agents quickstart template.

::: zone pivot="programming-language-csharp"

1. Create a new directory for your project and navigate to it:

   # [Bash](#tab/bash)

   ```bash
   mkdir MyDurableAgent
   cd MyDurableAgent
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   New-Item -ItemType Directory -Path MyDurableAgent
   Set-Location MyDurableAgent
   ```

   ---

1. Initialize the project from the template:

   ```console
   azd init --template durable-agents-quickstart-dotnet
   ```

   When prompted for an environment name, enter a name like `my-durable-agent`.

This downloads the quickstart project with all necessary files, including the Azure Functions configuration, agent code, and infrastructure as code templates.

::: zone-end

::: zone pivot="programming-language-python"

1. Create a new directory for your project and navigate to it:

   # [Bash](#tab/bash)

   ```bash
   mkdir MyDurableAgent
   cd MyDurableAgent
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   New-Item -ItemType Directory -Path MyDurableAgent
   Set-Location MyDurableAgent
   ```

   ---

1. Initialize the project from the template:

   ```console
   azd init --template durable-agents-quickstart-python
   ```

   When prompted for an environment name, enter a name like `my-durable-agent`.

1. Create and activate a virtual environment:

   # [Bash](#tab/bash)

   ```bash
   python3 -m venv .venv
   source .venv/bin/activate
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   python3 -m venv .venv
   .venv\Scripts\Activate.ps1
   ```

   ---


1. Install the required packages:

   ```console
   python -m pip install -r requirements.txt
   ```

This downloads the quickstart project with all necessary files, including the Azure Functions configuration, agent code, and infrastructure as code templates. It also prepares a virtual environment with the required dependencies.

::: zone-end

## Provision Azure resources

Use Azure Developer CLI to create the required Azure resources for your durable agent.

1. Provision the infrastructure:

   ```console
   azd provision
   ```

   This command creates:
   - An Azure OpenAI service with a gpt-4o-mini deployment
   - An Azure Functions app with Flex Consumption hosting plan
   - An Azure Storage account for the Azure Functions runtime and durable storage
   - A Durable Task Scheduler instance (Consumption plan) for managing agent state
   - Necessary networking and identity configurations

1. When prompted, select your Azure subscription and choose a location for the resources.

The provisioning process takes a few minutes. Once complete, azd stores the created resource information in your environment.

## Review the agent code

Now let's examine the code that defines your durable agent.

::: zone pivot="programming-language-csharp"

Open `Program.cs` to see the agent configuration:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using OpenAI;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o-mini";

// Create an AI agent following the standard Microsoft Agent Framework pattern
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You are a helpful assistant that can answer questions and provide information.",
        name: "MyDurableAgent");

using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options => options.AddAIAgent(agent))
    .Build();
app.Run();
```

This code:
1. Retrieves your Azure OpenAI configuration from environment variables.
1. Creates an Azure OpenAI client using Azure credentials.
1. Creates an AI agent with instructions and a name.
1. Configures the Azure Functions app to host the agent with durable thread management.

::: zone-end

::: zone pivot="programming-language-python"

Open `function_app.py` to see the agent configuration:

```python
import os
from agent_framework.azure import AzureOpenAIChatClient, AgentFunctionApp
from azure.identity import DefaultAzureCredential

endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
if not endpoint:
    raise ValueError("AZURE_OPENAI_ENDPOINT is not set.")
deployment_name = os.getenv("AZURE_OPENAI_DEPLOYMENT_NAME", "gpt-4o-mini")

# Create an AI agent following the standard Microsoft Agent Framework pattern
agent = AzureOpenAIChatClient(
    endpoint=endpoint,
    deployment_name=deployment_name,
    credential=DefaultAzureCredential()
).as_agent(
    instructions="You are a helpful assistant that can answer questions and provide information.",
    name="MyDurableAgent"
)

# Configure the function app to host the agent with durable thread management
app = AgentFunctionApp(agents=[agent])
```

This code:
+ Retrieves your Azure OpenAI configuration from environment variables.
+ Creates an Azure OpenAI client using Azure credentials.
+ Creates an AI agent with instructions and a name.
+ Configures the Azure Functions app to host the agent with durable thread management.

::: zone-end

The agent is now ready to be hosted in Azure Functions. The durable task extension automatically creates HTTP endpoints for interacting with your agent and manages conversation state across multiple requests.

## Configure local settings

Create a `local.settings.json` file for local development based on the sample file included in the project.

1. Copy the sample settings file:

   # [Bash](#tab/bash)

   ```bash
   cp local.settings.sample.json local.settings.json
   ```

   # [PowerShell](#tab/powershell)

   ```powershell
   Copy-Item local.settings.sample.json local.settings.json
   ```

   ---

1. Get your Azure OpenAI endpoint from the provisioned resources:

   ```console
   azd env get-value AZURE_OPENAI_ENDPOINT
   ```

1. Open `local.settings.json` and replace `<your-resource-name>` in the `AZURE_OPENAI_ENDPOINT` value with the endpoint from the previous command.

Your `local.settings.json` should look like this:

```json
{
  "IsEncrypted": false,
  "Values": {
    // ... other settings ...
    "AZURE_OPENAI_ENDPOINT": "https://your-openai-resource.openai.azure.com",
    "AZURE_OPENAI_DEPLOYMENT": "gpt-4o-mini",
    "TASKHUB_NAME": "default"
  }
}
```

> [!NOTE]
> The `local.settings.json` file is used for local development only and is not deployed to Azure. For production deployments, these settings are automatically configured in your Azure Functions app by the infrastructure templates.

## Start local development dependencies

To run durable agents locally, you need to start two services:
- **Azurite**: Emulates Azure Storage services (used by Azure Functions for managing triggers and internal state).
- **Durable Task Scheduler (DTS) emulator**: Manages durable state (conversation history, orchestration state) and scheduling for your agents 

### Start Azurite

Azurite emulates Azure Storage services locally. The Azure Functions uses it for managing internal state. You'll need to run this in a new terminal window and keep it running while you develop and test your durable agent.

1. Open a new terminal window and pull the Azurite Docker image:

   ```console
   docker pull mcr.microsoft.com/azure-storage/azurite
   ```

1. Start Azurite in a terminal window:

   ```console
   docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
   ```

   Azurite will start and listen on the default ports for Blob (10000), Queue (10001), and Table (10002) services.

Keep this terminal window open while you're developing and testing your durable agent.

> [!TIP]
> For more information about Azurite, including alternative installation methods, see [Use Azurite emulator for local Azure Storage development](/azure/storage/common/storage-use-azurite).

### Start the Durable Task Scheduler emulator

The DTS emulator provides the durable backend for managing agent state and orchestrations. It stores conversation history and ensures your agent's state persists across restarts. It also triggers durable orchestrations and agents. You'll need to run this in a separate new terminal window and keep it running while you develop and test your durable agent.

1. Open another new terminal window and pull the DTS emulator Docker image:

   ```console
   docker pull mcr.microsoft.com/dts/dts-emulator:latest
   ```

1. Run the DTS emulator:

   ```console
   docker run -p 8080:8080 -p 8082:8082 mcr.microsoft.com/dts/dts-emulator:latest
   ```

   This command starts the emulator and exposes:
   - Port 8080: The gRPC endpoint for the Durable Task Scheduler (used by your Functions app)
   - Port 8082: The administrative dashboard

1. The dashboard will be available at `http://localhost:8082`.

Keep this terminal window open while you're developing and testing your durable agent.

> [!TIP]
> To learn more about the DTS emulator, including how to configure multiple task hubs and access the dashboard, see [Develop with Durable Task Scheduler](/azure/azure-functions/durable/durable-task-scheduler/develop-with-durable-task-scheduler).

## Run the function app

Now you're ready to run your Azure Functions app with the durable agent.

1. In a new terminal window (keeping both Azurite and the DTS emulator running in separate windows), navigate to your project directory.

1. Start the Azure Functions runtime:

   ```console
   func start
   ```

1. You should see output indicating that your function app is running, including the HTTP endpoints for your agent:

   ```
   Functions:
        http-MyDurableAgent: [POST] http://localhost:7071/api/agents/MyDurableAgent/run
        dafx-MyDurableAgent: entityTrigger
   ```

These endpoints manage conversation state automatically - you don't need to create or manage thread objects yourself.

## Test the agent locally

Now you can interact with your durable agent using HTTP requests. The agent maintains conversation state across multiple requests, enabling multi-turn conversations.

### Start a new conversation

Create a new thread and send your first message:

# [Bash](#tab/bash)

```bash
curl -i -X POST http://localhost:7071/api/agents/MyDurableAgent/run \
  -H "Content-Type: text/plain" \
  -d "What are three popular programming languages?"
```

# [PowerShell](#tab/powershell)

```powershell
$response = Invoke-WebRequest -Uri "http://localhost:7071/api/agents/MyDurableAgent/run" `
  -Method POST `
  -Headers @{"Content-Type"="text/plain"} `
  -Body "What are three popular programming languages?"
$response.Headers
$response.Content
```

---

Sample response (note the `x-ms-thread-id` header contains the thread ID):

```
HTTP/1.1 200 OK
Content-Type: text/plain
x-ms-thread-id: @dafx-mydurableagent@263fa373-fa01-4705-abf2-5a114c2bb87d
Content-Length: 189

Three popular programming languages are Python, JavaScript, and Java. Python is known for its simplicity and readability, JavaScript powers web interactivity, and Java is widely used in enterprise applications.
```

Save the thread ID from the `x-ms-thread-id` header (e.g., `@dafx-mydurableagent@263fa373-fa01-4705-abf2-5a114c2bb87d`) for the next request.

### Continue the conversation

Send a follow-up message to the same thread by including the thread ID as a query parameter:

# [Bash](#tab/bash)

```bash
curl -X POST "http://localhost:7071/api/agents/MyDurableAgent/run?thread_id=@dafx-mydurableagent@263fa373-fa01-4705-abf2-5a114c2bb87d" \
  -H "Content-Type: text/plain" \
  -d "Which one is best for beginners?"
```

# [PowerShell](#tab/powershell)

```powershell
$threadId = "@dafx-mydurableagent@263fa373-fa01-4705-abf2-5a114c2bb87d"
Invoke-RestMethod -Uri "http://localhost:7071/api/agents/MyDurableAgent/run?thread_id=$threadId" `
  -Method POST `
  -Headers @{"Content-Type"="text/plain"} `
  -Body "Which one is best for beginners?"
```

---

Replace `@dafx-mydurableagent@263fa373-fa01-4705-abf2-5a114c2bb87d` with the actual thread ID from the previous response's `x-ms-thread-id` header.

Sample response:

```
Python is often considered the best choice for beginners among those three. Its clean syntax reads almost like English, making it easier to learn programming concepts without getting overwhelmed by complex syntax. It's also versatile and widely used in education.
```

Notice that the agent remembers the context from the previous message (the three programming languages) without you having to specify them again. Because the conversation state is stored durably by the Durable Task Scheduler, this history persists even if you restart the function app or the conversation is resumed by a different instance.

## Monitor with the Durable Task Scheduler dashboard

The Durable Task Scheduler provides a built-in dashboard for monitoring and debugging your durable agents. The dashboard offers deep visibility into agent operations, conversation history, and execution flow.

### Access the dashboard

1. Open the dashboard for your local DTS emulator at `http://localhost:8082` in your web browser.

1. Select the **default** task hub from the list to view its details.

1. Select the gear icon in the top-right corner to open the settings, and ensure that the **Enable  Agent pages** option under *Preview Features* is selected.

### Explore agent conversations

1. In the dashboard, navigate to the **Agents** tab.

1. Select your durable agent thread (e.g., `mydurableagent - 263fa373-fa01-4705-abf2-5a114c2bb87d`) from the list.

   You'll see a detailed view of the agent thread, including the complete conversation history with all messages and responses.


The dashboard provides a timeline view to help you understand the flow of the conversation. Key information include:

- Timestamps and duration for each interaction
- Prompt and response content
- Number of tokens used

> [!TIP]
> The DTS dashboard provides real-time updates, so you can watch your agent's behavior as you interact with it through the HTTP endpoints.

## Deploy to Azure

Now that you've tested your durable agent locally, deploy it to Azure.

1. Deploy the application:

   ```console
   azd deploy
   ```

   This command packages your application and deploys it to the Azure Functions app created during provisioning.

1. Wait for the deployment to complete. The output will confirm when your agent is running in Azure.

## Test the deployed agent

After deployment, test your agent running in Azure.

### Get the function key

Azure Functions requires an API key for HTTP-triggered functions in production:

# [Bash](#tab/bash)

```bash
API_KEY=`az functionapp function keys list --name $(azd env get-value AZURE_FUNCTION_NAME) --resource-group $(azd env get-value AZURE_RESOURCE_GROUP) --function-name http-MyDurableAgent --query default -o tsv`
```

# [PowerShell](#tab/powershell)

```powershell
$functionName = azd env get-value AZURE_FUNCTION_NAME
$resourceGroup = azd env get-value AZURE_RESOURCE_GROUP
$API_KEY = az functionapp function keys list --name $functionName --resource-group $resourceGroup --function-name http-MyDurableAgent --query default -o tsv
```

---

### Start a new conversation

Create a new thread and send your first message to the deployed agent:

# [Bash](#tab/bash)

```bash
curl -i -X POST "https://$(azd env get-value AZURE_FUNCTION_NAME).azurewebsites.net/api/agents/MyDurableAgent/run?code=$API_KEY" \
  -H "Content-Type: text/plain" \
  -d "What are three popular programming languages?"
```

# [PowerShell](#tab/powershell)

```powershell
$functionName = azd env get-value AZURE_FUNCTION_NAME
$response = Invoke-WebRequest -Uri "https://$functionName.azurewebsites.net/api/agents/MyDurableAgent/run?code=$API_KEY" `
  -Method POST `
  -Headers @{"Content-Type"="text/plain"} `
  -Body "What are three popular programming languages?"
$response.Headers
$response.Content
```

---

Note the thread ID returned in the `x-ms-thread-id` response header.

### Continue the conversation

Send a follow-up message in the same thread. Replace `<thread-id>` with the thread ID from the previous response:

# [Bash](#tab/bash)

```bash
THREAD_ID="<thread-id>"
curl -X POST "https://$(azd env get-value AZURE_FUNCTION_NAME).azurewebsites.net/api/agents/MyDurableAgent/run?code=$API_KEY&thread_id=$THREAD_ID" \
  -H "Content-Type: text/plain" \
  -d "Which is easiest to learn?"
```

# [PowerShell](#tab/powershell)

```powershell
$THREAD_ID = "<thread-id>"
$functionName = azd env get-value AZURE_FUNCTION_NAME
Invoke-RestMethod -Uri "https://$functionName.azurewebsites.net/api/agents/MyDurableAgent/run?code=$API_KEY&thread_id=$THREAD_ID" `
  -Method POST `
  -Headers @{"Content-Type"="text/plain"} `
  -Body "Which is easiest to learn?"
```

---

The agent maintains conversation context in Azure just as it did locally, demonstrating the durability of the agent state.

## Monitor the deployed agent

You can monitor your deployed agent using the Durable Task Scheduler dashboard in Azure.

1. Get the name of your Durable Task Scheduler instance:

   ```console
   azd env get-value DTS_NAME
   ```

1. Open the [Azure portal](https://portal.azure.com) and search for the Durable Task Scheduler name from the previous step.

1. In the overview blade of the Durable Task Scheduler resource, select the **default** task hub from the list.

1. Select **Open Dashboard** at the top of the task hub page to open the monitoring dashboard.

1. View your agent's conversations just as you did with the local emulator.

The Azure-hosted dashboard provides the same debugging and monitoring capabilities as the local emulator, allowing you to inspect conversation history, trace tool calls, and analyze performance in your production environment.

## Understanding durable agent features

The durable agent you just created provides several important features that differentiate it from standard agents:

### Stateful conversations

The agent automatically maintains conversation state across interactions. Each thread has its own isolated conversation history, stored durably in the Durable Task Scheduler. Unlike stateless APIs where you'd need to send the full conversation history with each request, durable agents manage this for you automatically.

### Serverless hosting

Your agent runs in Azure Functions with event-driven, pay-per-invocation pricing. When deployed to Azure with the [Flex Consumption plan](/azure/azure-functions/flex-consumption-plan), your agent can scale to thousands of instances during high traffic or down to zero when not in use, ensuring you only pay for actual usage.

### Built-in HTTP endpoints

The durable task extension automatically creates HTTP endpoints for your agent, eliminating the need to write custom HTTP handlers or API code. This includes endpoints for creating threads, sending messages, and retrieving conversation history.

### Durable state management

All agent state is managed by the Durable Task Scheduler, ensuring that:
- Conversations survive process crashes and restarts.
- State is distributed across multiple instances for high availability.
- Any instance can resume an agent's execution after interruptions.
- Conversation history is maintained reliably even during scaling events.

## Next steps

Now that you have a working durable agent, you can explore more advanced features:

> [!div class="nextstepaction"]
> [Learn about durable agent features](../../user-guide/agents/agent-types/durable-agent/features.md)

Additional resources:

- [Durable Task Scheduler Overview](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler)
- [Azure Functions Flex Consumption Plan](/azure/azure-functions/flex-consumption-plan)
