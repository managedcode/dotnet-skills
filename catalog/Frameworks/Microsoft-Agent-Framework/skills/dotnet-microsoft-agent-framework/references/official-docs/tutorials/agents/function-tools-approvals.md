---
title: Using function tools with human in the loop approvals
description: Learn how to use function tools with human in the loop approvals
zone_pivot_groups: programming-languages
author: westey-m
ms.topic: tutorial
ms.author: westey
ms.date: 09/15/2025
ms.service: agent-framework
---

# Using function tools with human in the loop approvals

::: zone pivot="programming-language-csharp"

This tutorial step shows you how to use function tools that require human approval with an agent, where the agent is built on the Azure OpenAI Chat Completion service.

When agents require any user input, for example to approve a function call, this is referred to as a human-in-the-loop pattern.
An agent run that requires user input, will complete with a response that indicates what input is required from the user, instead of completing with a final answer.
The caller of the agent is then responsible for getting the required input from the user, and passing it back to the agent as part of a new agent run.

## Prerequisites

For prerequisites and installing NuGet packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create the agent with function tools

When using functions, it's possible to indicate for each function, whether it requires human approval before being executed.
This is done by wrapping the `AIFunction` instance in an `ApprovalRequiredAIFunction` instance.

Here is an example of a simple function tool that fakes getting the weather for a given location.

```csharp
using System;
using System.ComponentModel;
using System.Linq;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";
```

To create an `AIFunction` and then wrap it in an `ApprovalRequiredAIFunction`, you can do the following:

```csharp
AIFunction weatherFunction = AIFunctionFactory.Create(GetWeather);
AIFunction approvalRequiredWeatherFunction = new ApprovalRequiredAIFunction(weatherFunction);
```

When creating the agent, you can now provide the approval requiring function tool to the agent, by passing a list of tools to the `AsAIAgent` method.

```csharp
AIAgent agent = new AzureOpenAIClient(
    new Uri("https://<myresource>.openai.azure.com"),
    new AzureCliCredential())
     .GetChatClient("gpt-4o-mini")
     .AsAIAgent(instructions: "You are a helpful assistant", tools: [approvalRequiredWeatherFunction]);
```

Since you now have a function that requires approval, the agent might respond with a request for approval, instead of executing the function directly and returning the result.
You can check the response content for any `FunctionApprovalRequestContent` instances, which indicates that the agent requires user approval for a function.

```csharp
AgentThread thread = await agent.GetNewThreadAsync();
AgentResponse response = await agent.RunAsync("What is the weather like in Amsterdam?", thread);

var functionApprovalRequests = response.Messages
    .SelectMany(x => x.Contents)
    .OfType<FunctionApprovalRequestContent>()
    .ToList();
```

If there are any function approval requests, the detail of the function call including name and arguments can be found in the `FunctionCall` property on the `FunctionApprovalRequestContent` instance.
This can be shown to the user, so that they can decide whether to approve or reject the function call.
For this example, assume there is one request.

```csharp
FunctionApprovalRequestContent requestContent = functionApprovalRequests.First();
Console.WriteLine($"We require approval to execute '{requestContent.FunctionCall.Name}'");
```

Once the user has provided their input, you can create a `FunctionApprovalResponseContent` instance using the `CreateResponse` method on the `FunctionApprovalRequestContent`.
Pass `true` to approve the function call, or `false` to reject it.

The response content can then be passed to the agent in a new `User` `ChatMessage`, along with the same thread object to get the result back from the agent.

```csharp
var approvalMessage = new ChatMessage(ChatRole.User, [requestContent.CreateResponse(true)]);
Console.WriteLine(await agent.RunAsync(approvalMessage, thread));
```

Whenever you are using function tools with human in the loop approvals, remember to check for `FunctionApprovalRequestContent` instances in the response, after each agent run, until all function calls have been approved or rejected.

::: zone-end
::: zone pivot="programming-language-python"

This tutorial step shows you how to use function tools that require human approval with an agent.

When agents require any user input, for example to approve a function call, this is referred to as a human-in-the-loop pattern.
An agent run that requires user input, will complete with a response that indicates what input is required from the user, instead of completing with a final answer.
The caller of the agent is then responsible for getting the required input from the user, and passing it back to the agent as part of a new agent run.

## Prerequisites

For prerequisites and installing Python packages, see the [Create and run a simple agent](./run-agent.md) step in this tutorial.

## Create the agent with function tools requiring approval

When using functions, it's possible to indicate for each function, whether it requires human approval before being executed.
This is done by setting the `approval_mode` parameter to `"always_require"` when using the `@ai_function` decorator.

Here is an example of a simple function tool that fakes getting the weather for a given location.

```python
from typing import Annotated
from agent_framework import ai_function

@ai_function
def get_weather(location: Annotated[str, "The city and state, e.g. San Francisco, CA"]) -> str:
    """Get the current weather for a given location."""
    return f"The weather in {location} is cloudy with a high of 15°C."
```

To create a function that requires approval, you can use the `approval_mode` parameter:

```python
@ai_function(approval_mode="always_require")
def get_weather_detail(location: Annotated[str, "The city and state, e.g. San Francisco, CA"]) -> str:
    """Get detailed weather information for a given location."""
    return f"The weather in {location} is cloudy with a high of 15°C, humidity 88%."
```

When creating the agent, you can now provide the approval requiring function tool to the agent, by passing a list of tools to the `ChatAgent` constructor.

```python
from agent_framework import ChatAgent
from agent_framework.openai import OpenAIResponsesClient

async with ChatAgent(
    chat_client=OpenAIResponsesClient(),
    name="WeatherAgent",
    instructions="You are a helpful weather assistant.",
    tools=[get_weather, get_weather_detail],
) as agent:
    # Agent is ready to use
```

Since you now have a function that requires approval, the agent might respond with a request for approval, instead of executing the function directly and returning the result.
You can check the response for any user input requests, which indicates that the agent requires user approval for a function.

```python
result = await agent.run("What is the detailed weather like in Amsterdam?")

if result.user_input_requests:
    for user_input_needed in result.user_input_requests:
        print(f"Function: {user_input_needed.function_call.name}")
        print(f"Arguments: {user_input_needed.function_call.arguments}")
```

If there are any function approval requests, the detail of the function call including name and arguments can be found in the `function_call` property on the user input request.
This can be shown to the user, so that they can decide whether to approve or reject the function call.

Once the user has provided their input, you can create a response using the `create_response` method on the user input request.
Pass `True` to approve the function call, or `False` to reject it.

The response can then be passed to the agent in a new `ChatMessage`, to get the result back from the agent.

```python
from agent_framework import ChatMessage, Role

# Get user approval (in a real application, this would be interactive)
user_approval = True  # or False to reject

# Create the approval response
approval_message = ChatMessage(
    role=Role.USER, 
    contents=[user_input_needed.create_response(user_approval)]
)

# Continue the conversation with the approval
final_result = await agent.run([
    "What is the detailed weather like in Amsterdam?",
    ChatMessage(role=Role.ASSISTANT, contents=[user_input_needed]),
    approval_message
])
print(final_result.text)
```

## Handling approvals in a loop

When working with multiple function calls that require approval, you may need to handle approvals in a loop until all functions are approved or rejected:

```python
async def handle_approvals(query: str, agent) -> str:
    """Handle function call approvals in a loop."""
    current_input = query
    
    while True:
        result = await agent.run(current_input)
        
        if not result.user_input_requests:
            # No more approvals needed, return the final result
            return result.text
        
        # Build new input with all context
        new_inputs = [query]
        
        for user_input_needed in result.user_input_requests:
            print(f"Approval needed for: {user_input_needed.function_call.name}")
            print(f"Arguments: {user_input_needed.function_call.arguments}")
            
            # Add the assistant message with the approval request
            new_inputs.append(ChatMessage(role=Role.ASSISTANT, contents=[user_input_needed]))
            
            # Get user approval (in practice, this would be interactive)
            user_approval = True  # Replace with actual user input
            
            # Add the user's approval response
            new_inputs.append(
                ChatMessage(role=Role.USER, contents=[user_input_needed.create_response(user_approval)])
            )
        
        # Continue with all the context
        current_input = new_inputs

# Usage
result_text = await handle_approvals("Get detailed weather for Seattle and Portland", agent)
print(result_text)
```

Whenever you are using function tools with human in the loop approvals, remember to check for user input requests in the response, after each agent run, until all function calls have been approved or rejected.

::: zone-end

## Next steps

> [!div class="nextstepaction"]
> [Producing Structured Output with agents](./structured-output.md)
