---
title: Microsoft Agent Framework Quick Start
description: Quick Start guide for Agent Framework.
ms.service: agent-framework
ms.topic: tutorial
ms.date: 09/04/2025
ms.reviewer: ssalgado
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.author: taochen
---

# Microsoft Agent Framework Quick-Start Guide

This guide will help you get up and running quickly with a basic agent using Agent Framework and Azure OpenAI.

::: zone pivot="programming-language-csharp"

## Prerequisites

Before you begin, ensure you have the following:

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- [Azure OpenAI resource](/azure/ai-foundry/openai/how-to/create-resource) with a deployed model (for example, `gpt-4o-mini`)
- [Azure CLI installed](/cli/azure/install-azure-cli) and [authenticated](/cli/azure/authenticate-azure-cli) (`az login`)
- [User has the `Cognitive Services OpenAI User` or `Cognitive Services OpenAI Contributor` roles for the Azure OpenAI resource.](/azure/ai-foundry/openai/how-to/role-based-access-control)

> [!NOTE]
> Microsoft Agent Framework is supported with all actively supported versions of .NET. For the purposes of this sample, we recommend the .NET 8 SDK or a later version.

> [!NOTE]
> This demo uses Azure CLI credentials for authentication. Make sure you're logged in with `az login` and have access to the Azure OpenAI resource. For more information, see the [Azure CLI documentation](/cli/azure/authenticate-azure-cli-interactively). It is also possible to replace the `AzureCliCredential` with an `ApiKeyCredential` if you
have an api key and do not wish to use role based authentication, in which case `az login` is not required.

## Create a project

```powershell
dotnet new console -o AgentFrameworkQuickStart
cd AgentFrameworkQuickStart
```

## Install Packages

Packages will be published to [NuGet Gallery | MicrosoftAgentFramework](https://www.nuget.org/profiles/MicrosoftAgentFramework).

First, add the following Microsoft Agent Framework NuGet packages into your application, using the following commands:

```dotnetcli
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

## Running a Basic Agent Sample

This sample demonstrates how to create and use a simple AI agent with Azure OpenAI Chat Completion as the backend. It will create a basic agent using `AzureOpenAIClient` with `gpt-4o-mini` and custom instructions.

### Sample Code

Make sure to replace `https://your-resource.openai.azure.com/` with the endpoint of your Azure OpenAI resource.

```csharp
using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;

AIAgent agent = new AzureOpenAIClient(
  new Uri("https://your-resource.openai.azure.com/"),
  new AzureCliCredential())
    .GetChatClient("gpt-4o-mini")
    .AsAIAgent(instructions: "You are good at telling jokes.");

Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate."));
```

## (Optional) Install Nightly Packages

If you need to get a package containing the latest enhancements or fixes, nightly builds of Agent Framework are available at <https://github.com/orgs/microsoft/packages?repo_name=agent-framework>.

To download nightly builds, follow these steps:

1. You will need a GitHub account to complete these steps.
1. Create a GitHub Personal Access Token with the `read:packages` scope using these [instructions](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-personal-access-token-classic).
1. If your account is part of the Microsoft organization, then you must authorize the `Microsoft` organization as a single sign-on organization.
    1. Click the "Configure SSO" next to the Personal Access Token you just created and then authorize `Microsoft`.
1. Use the following command to add the Microsoft GitHub Packages source to your NuGet configuration:

    ```powershell
    dotnet nuget add source --username GITHUBUSERNAME --password GITHUBPERSONALACCESSTOKEN --store-password-in-clear-text --name GitHubMicrosoft "https://nuget.pkg.github.com/microsoft/index.json"
    ```

1. Or you can manually create a `NuGet.Config` file.

    ```xml
    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <packageSources>
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
        <add key="github" value="https://nuget.pkg.github.com/microsoft/index.json" />
      </packageSources>

      <packageSourceMapping>
        <packageSource key="nuget.org">
          <package pattern="*" />
        </packageSource>
        <packageSource key="github">
          <package pattern="*nightly"/>
          <package pattern="Microsoft.Agents.AI" />
        </packageSource>
      </packageSourceMapping>

      <packageSourceCredentials>
        <github>
            <add key="Username" value="<Your GitHub Id>" />
            <add key="ClearTextPassword" value="<Your Personal Access Token>" />
          </github>
      </packageSourceCredentials>
    </configuration>
    ```

    - If you place this file in your project folder, make sure to have Git (or whatever source control you use) ignore it.
    - For more information on where to store this file, see [nuget.config reference](/nuget/reference/nuget-config-file).

1. You can now add packages from the nightly build to your project.

   For example, use this command `dotnet add package Microsoft.Agents.AI --prerelease`

1. And the latest package release can be referenced in the project like this:

   `<PackageReference Include="Microsoft.Agents.AI" Version="*-*" />`

For more information, see <https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry>.

::: zone-end

::: zone pivot="programming-language-python"

## Prerequisites

Before you begin, ensure you have the following:

- [Python 3.10 or later](https://www.python.org/downloads/)
- An [Azure AI](/azure/ai-foundry/) project with a deployed model (for example, `gpt-4o-mini`)
- [Azure CLI](/cli/azure/install-azure-cli) installed and authenticated (`az login`)
- Install the Agent Framework Package:

```bash
pip install -U agent-framework --pre
```

> [!NOTE]
> Installing `agent-framework` will install `agent-framework-core` and all other official packages. If you want to install only the Azure AI package, you can run: `pip install agent-framework-azure-ai --pre`
> All of the official packages, including `agent-framework-azure-ai` have a dependency on `agent-framework-core`, so in most cases, you wouldn't have to specify that.
> The full list of official packages can be found in the [Agent Framework GitHub repository](https://github.com/microsoft/agent-framework/blob/main/python/pyproject.toml#L80).

> [!NOTE]
> This sample uses Azure CLI credentials for authentication. Make sure you're logged in with `az login` and have access to the Azure AI project. For more information, see the [Azure CLI documentation](/cli/azure/authenticate-azure-cli-interactively).

## Running a Basic Agent Sample

This sample demonstrates how to create and use a simple AI agent with Azure AI as the backend. It will create a basic agent using `ChatAgent` with `AzureAIAgentClient` and custom instructions.

Make sure to set the following environment variables:
- `AZURE_AI_PROJECT_ENDPOINT`: Your Azure AI project endpoint
- `AZURE_AI_MODEL_DEPLOYMENT_NAME`: The name of your model deployment

### Sample Code

```python
import asyncio
from agent_framework.azure import AzureAIClient
from azure.identity.aio import AzureCliCredential

async def main():
    async with (
        AzureCliCredential() as credential,
        AzureAIClient(async_credential=credential).as_agent(
            instructions="You are good at telling jokes."
        ) as agent,
    ):
        result = await agent.run("Tell me a joke about a pirate.")
        print(result.text)

if __name__ == "__main__":
    asyncio.run(main())
```

## More Examples

For more detailed examples and advanced scenarios, see the [Azure AI Examples](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/agents/azure_ai/README.md).


::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Create and run agents](./agents/run-agent.md)
