---
title: Code Interpreter
description: Learn how to use the Code Interpreter tool with Agent Framework agents.
zone_pivot_groups: programming-languages
author: eavanvalkenburg
ms.topic: reference
ms.author: edvan
ms.date: 07/01/2026
ms.service: agent-framework
---

# Code Interpreter

Code Interpreter allows agents to write and execute code in a sandboxed environment. This is useful for data analysis, mathematical computations, file processing, and other tasks that benefit from code execution.

> [!NOTE]
> Code Interpreter availability depends on the underlying agent provider. See [Providers Overview](../../integrations/by-component/model-providers/index.md) for provider-specific support.

:::zone pivot="programming-language-csharp"

The following example shows how to create an agent with the Code Interpreter tool and read the generated output:

### Create an agent with Code Interpreter

```csharp
using System;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Requires: dotnet add package Microsoft.Agents.AI.Foundry --prerelease
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

// Create an agent with the code interpreter hosted tool
AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a helpful assistant that can write and execute Python code.",
        tools: [new CodeInterpreterToolDefinition()]);

var response = await agent.RunAsync("Calculate the factorial of 100 using code.");
Console.WriteLine(response);
```

> [!WARNING]
> `DefaultAzureCredential` is convenient for development but requires careful consideration in production. In production, consider using a specific credential (e.g., `ManagedIdentityCredential`) to avoid latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

### Read code output

```csharp
// Inspect code interpreter output from the response
foreach (var message in response.Messages)
{
    foreach (var content in message.Contents)
    {
        if (content is CodeInterpreterContent codeContent)
        {
            Console.WriteLine($"Code:\n{codeContent.Code}");
            Console.WriteLine($"Output:\n{codeContent.Output}");
        }
    }
}
```

:::zone-end

:::zone pivot="programming-language-python"

The following example shows how to create an agent with the Code Interpreter tool:

```python
# Copyright (c) Microsoft. All rights reserved.

import asyncio

from agent_framework import (
    Agent,
    Content,
)
from agent_framework.openai import OpenAIChatClient

"""
OpenAI Chat Client with Code Interpreter Example

This sample demonstrates using get_code_interpreter_tool() with OpenAI Chat Client
for Python code execution and mathematical problem solving.
"""


async def main() -> None:
    """Example showing how to use the code interpreter tool with OpenAI Chat."""
    print("=== OpenAI Chat Client Agent with Code Interpreter Example ===")

    client = OpenAIChatClient()
    agent = Agent(
        client=client,
        instructions="You are a helpful assistant that can write and execute Python code to solve problems.",
        tools=client.get_code_interpreter_tool(),
    )

    query = "Use code to get the factorial of 100?"
    print(f"User: {query}")
    result = await agent.run(query)
    print(f"Result: {result}\n")

    for message in result.messages:
        code_blocks = [c for c in message.contents if c.type == "code_interpreter_tool_call"]
        outputs = [c for c in message.contents if c.type == "code_interpreter_tool_result"]

        if code_blocks:
            code_inputs = code_blocks[0].inputs or []
            for content in code_inputs:
                if isinstance(content, Content) and content.type == "text":
                    print(f"Generated code:\n{content.text}")
                    break
        if outputs:
            print("Execution outputs:")
            for out in outputs[0].outputs or []:
                if isinstance(out, Content) and out.type == "text":
                    print(out.text)


if __name__ == "__main__":
    asyncio.run(main())
```

### Current OpenAI code interpreter sample

The current OpenAI code-interpreter sample in the code repo uses `OpenAIChatClient` and shows how to inspect generated code plus the final execution output:

:::code language="python" source="~/../agent-framework-code/python/samples/02-agents/providers/openai/client_with_code_interpreter.py" range="23-57":::

:::zone-end

:::zone pivot="programming-language-go"
## Code interpreter

The `hostedtool.CodeInterpreter` type enables server-side code execution when using a provider that supports it.

```go
import "github.com/microsoft/agent-framework-go/tool/hostedtool"

codeInterpreter := &hostedtool.CodeInterpreter{}

a := foundryprovider.NewAgent(endpoint, token, foundryprovider.ModelDeployment(model), foundryprovider.AgentConfig{
    Config: agent.Config{
        Tools: []tool.Tool{codeInterpreter},
    },
})
```

> [!NOTE]
> Code interpreter is a hosted tool — code execution happens on the AI service side, not locally.

:::zone-end
## Next steps

> [!div class="nextstepaction"]
> [File Search](./file-search.md)
