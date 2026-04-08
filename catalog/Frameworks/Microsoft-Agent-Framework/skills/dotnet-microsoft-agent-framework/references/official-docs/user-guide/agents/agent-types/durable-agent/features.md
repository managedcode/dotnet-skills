---
title: Durable Agent Features
description: Learn about advanced features of the durable task extension for Microsoft Agent Framework including orchestrations, tool calls, and human-in-the-loop workflows.
zone_pivot_groups: programming-languages
author: anthonychu
ms.topic: tutorial
ms.author: antchu
ms.date: 11/05/2025
ms.service: agent-framework
---

# Durable Agent Features

When you build AI agents with Microsoft Agent Framework, the durable task extension for Microsoft Agent Framework adds advanced capabilities to your standard agents including automatic conversation state management, deterministic orchestrations, and human-in-the-loop patterns. The extension also makes it easy to host your agents on serverless compute provided by Azure Functions, delivering dynamic scaling and a cost-efficient per-request billing model.

## Deterministic Multi-Agent Orchestrations

The durable task extension supports building deterministic workflows that coordinate multiple agents using [Azure Durable Functions](/azure/azure-functions/durable/durable-functions-overview) orchestrations. 

**[Orchestrations](/azure/azure-functions/durable/durable-functions-orchestrations)** are code-based workflows that coordinate multiple operations (like agent calls, external API calls, or timers) in a reliable way. **Deterministic** means the orchestration code executes the same way when replayed after a failure, making workflows reliable and debuggable—when you replay an orchestration's history, you can see exactly what happened at each step.

Orchestrations execute reliably, surviving failures between agent calls, and provide predictable and repeatable processes. This makes them ideal for complex multi-agent scenarios where you need guaranteed execution order and fault tolerance.

### Sequential Orchestrations

In the sequential multi-agent pattern, specialized agents execute in a specific order, where each agent's output can influence the next agent's execution. This pattern supports conditional logic and branching based on agent responses.

::: zone pivot="programming-language-csharp"

When using agents in orchestrations, you must use the `context.GetAgent()` API to get a `DurableAIAgent` instance, which is a special subclass of the standard `AIAgent` type that wraps one of your registered agents. The `DurableAIAgent` wrapper ensures that agent calls are properly tracked and checkpointed by the durable orchestration framework.

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Agents.AI.DurableTask;

[Function(nameof(SpamDetectionOrchestration))]
public static async Task<string> SpamDetectionOrchestration(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    Email email = context.GetInput<Email>();

    // Check if the email is spam
    DurableAIAgent spamDetectionAgent = context.GetAgent("SpamDetectionAgent");
    AgentThread spamThread = await spamDetectionAgent.GetNewThreadAsync();

    AgentResponse<DetectionResult> spamDetectionResponse = await spamDetectionAgent.RunAsync<DetectionResult>(
        message: $"Analyze this email for spam: {email.EmailContent}",
        thread: spamThread);
    DetectionResult result = spamDetectionResponse.Result;

    if (result.IsSpam)
    {
        return await context.CallActivityAsync<string>(nameof(HandleSpamEmail), result.Reason);
    }

    // Generate response for legitimate email
    DurableAIAgent emailAssistantAgent = context.GetAgent("EmailAssistantAgent");
    AgentThread emailThread = await emailAssistantAgent.GetNewThreadAsync();

    AgentResponse<EmailResponse> emailAssistantResponse = await emailAssistantAgent.RunAsync<EmailResponse>(
        message: $"Draft a professional response to: {email.EmailContent}",
        thread: emailThread);

    return await context.CallActivityAsync<string>(nameof(SendEmail), emailAssistantResponse.Result.Response);
}
```

::: zone-end

::: zone pivot="programming-language-python"

When using agents in orchestrations, you must use the `app.get_agent()` method to get a durable agent instance, which is a special wrapper around one of your registered agents. The durable agent wrapper ensures that agent calls are properly tracked and checkpointed by the durable orchestration framework.

```python
import azure.durable_functions as df
from typing import cast
from agent_framework.azure import AgentFunctionApp
from pydantic import BaseModel

class SpamDetectionResult(BaseModel):
    is_spam: bool
    reason: str

class EmailResponse(BaseModel):
    response: str

app = AgentFunctionApp(agents=[spam_detection_agent, email_assistant_agent])

@app.orchestration_trigger(context_name="context")
def spam_detection_orchestration(context: df.DurableOrchestrationContext):
    email = context.get_input()

    # Check if the email is spam
    spam_agent = app.get_agent(context, "SpamDetectionAgent")
    spam_thread = spam_agent.get_new_thread()

    spam_result_raw = yield spam_agent.run(
        messages=f"Analyze this email for spam: {email['content']}",
        thread=spam_thread,
        response_format=SpamDetectionResult
    )
    spam_result = cast(SpamDetectionResult, spam_result_raw.get("structured_response"))

    if spam_result.is_spam:
        result = yield context.call_activity("handle_spam_email", spam_result.reason)
        return result

    # Generate response for legitimate email
    email_agent = app.get_agent(context, "EmailAssistantAgent")
    email_thread = email_agent.get_new_thread()

    email_response_raw = yield email_agent.run(
        messages=f"Draft a professional response to: {email['content']}",
        thread=email_thread,
        response_format=EmailResponse
    )
    email_response = cast(EmailResponse, email_response_raw.get("structured_response"))

    result = yield context.call_activity("send_email", email_response.response)
    return result
```

::: zone-end

Orchestrations coordinate work across multiple agents, surviving failures between agent calls. The orchestration context provides methods to retrieve and interact with hosted agents within orchestrations.

### Parallel Orchestrations

In the parallel multi-agent pattern, you execute multiple agents concurrently and then aggregate their results. This pattern is useful for gathering diverse perspectives or processing independent subtasks simultaneously.

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Agents.AI.DurableTask;

[Function(nameof(ResearchOrchestration))]
public static async Task<string> ResearchOrchestration(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    string topic = context.GetInput<string>();

    // Execute multiple research agents in parallel
    DurableAIAgent technicalAgent = context.GetAgent("TechnicalResearchAgent");
    DurableAIAgent marketAgent = context.GetAgent("MarketResearchAgent");
    DurableAIAgent competitorAgent = context.GetAgent("CompetitorResearchAgent");

    // Start all agent runs concurrently
    Task<AgentResponse<TextResponse>> technicalTask = 
        technicalAgent.RunAsync<TextResponse>($"Research technical aspects of {topic}");
    Task<AgentResponse<TextResponse>> marketTask = 
        marketAgent.RunAsync<TextResponse>($"Research market trends for {topic}");
    Task<AgentResponse<TextResponse>> competitorTask = 
        competitorAgent.RunAsync<TextResponse>($"Research competitors in {topic}");

    // Wait for all tasks to complete
    await Task.WhenAll(technicalTask, marketTask, competitorTask);

    // Aggregate results
    string allResearch = string.Join("\n\n", 
        technicalTask.Result.Result.Text,
        marketTask.Result.Result.Text,
        competitorTask.Result.Result.Text);
    
    DurableAIAgent summaryAgent = context.GetAgent("SummaryAgent");
    AgentResponse<TextResponse> summaryResponse = 
        await summaryAgent.RunAsync<TextResponse>($"Summarize this research:\n{allResearch}");
    
    return summaryResponse.Result.Text;
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
import azure.durable_functions as df
from agent_framework.azure import AgentFunctionApp

app = AgentFunctionApp(agents=[technical_agent, market_agent, competitor_agent, summary_agent])

@app.orchestration_trigger(context_name="context")
def research_orchestration(context: df.DurableOrchestrationContext):
    topic = context.get_input()

    # Execute multiple research agents in parallel
    technical_agent = app.get_agent(context, "TechnicalResearchAgent")
    market_agent = app.get_agent(context, "MarketResearchAgent")
    competitor_agent = app.get_agent(context, "CompetitorResearchAgent")

    technical_task = technical_agent.run(messages=f"Research technical aspects of {topic}")
    market_task = market_agent.run(messages=f"Research market trends for {topic}")
    competitor_task = competitor_agent.run(messages=f"Research competitors in {topic}")

    # Wait for all tasks to complete
    results = yield context.task_all([technical_task, market_task, competitor_task])

    # Aggregate results
    all_research = "\n\n".join([r.get('response', '') for r in results])
    
    summary_agent = app.get_agent(context, "SummaryAgent")
    summary = yield summary_agent.run(messages=f"Summarize this research:\n{all_research}")
    
    return summary.get('response', '')
```

::: zone-end

The parallel execution is tracked using a list of tasks. Automatic checkpointing ensures that completed agent executions are not repeated or lost if a failure occurs during aggregation.

### Human-in-the-Loop Orchestrations

Deterministic agent orchestrations can pause for human input, approval, or review without consuming compute resources. Durable execution enables orchestrations to wait for days or even weeks while waiting for human responses. When combined with serverless hosting, all compute resources are spun down during the wait period, eliminating compute costs until the human provides their input.

::: zone pivot="programming-language-csharp"

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Agents.AI.DurableTask;

[Function(nameof(ContentApprovalWorkflow))]
public static async Task<string> ContentApprovalWorkflow(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    string topic = context.GetInput<string>();

    // Generate content using an agent
    DurableAIAgent contentAgent = context.GetAgent("ContentGenerationAgent");
    AgentResponse<GeneratedContent> contentResponse = 
        await contentAgent.RunAsync<GeneratedContent>($"Write an article about {topic}");
    GeneratedContent draftContent = contentResponse.Result;

    // Send for human review
    await context.CallActivityAsync(nameof(NotifyReviewer), draftContent);

    // Wait for approval with timeout
    HumanApprovalResponse approvalResponse;
    try
    {
        approvalResponse = await context.WaitForExternalEvent<HumanApprovalResponse>(
            eventName: "ApprovalDecision",
            timeout: TimeSpan.FromHours(24));
    }
    catch (OperationCanceledException)
    {
        // Timeout occurred - escalate for review
        return await context.CallActivityAsync<string>(nameof(EscalateForReview), draftContent);
    }
    
    if (approvalResponse.Approved)
    {
        return await context.CallActivityAsync<string>(nameof(PublishContent), draftContent);
    }
    
    return "Content rejected";
}
```

::: zone-end

::: zone pivot="programming-language-python"

```python
import azure.durable_functions as df
from datetime import timedelta
from agent_framework.azure import AgentFunctionApp

app = AgentFunctionApp(agents=[content_agent])

@app.orchestration_trigger(context_name="context")
def content_approval_workflow(context: df.DurableOrchestrationContext):
    topic = context.get_input()

    # Generate content using an agent
    content_agent = app.get_agent(context, "ContentGenerationAgent")
    draft_content = yield content_agent.run(
        messages=f"Write an article about {topic}"
    )

    # Send for human review
    yield context.call_activity("notify_reviewer", draft_content)

    # Wait for approval with timeout
    approval_task = context.wait_for_external_event("ApprovalDecision")
    timeout_task = context.create_timer(
        context.current_utc_datetime + timedelta(hours=24)
    )

    winner = yield context.task_any([approval_task, timeout_task])

    if winner == approval_task:
        timeout_task.cancel()
        approval_data = approval_task.result
        if approval_data.get("approved"):
            result = yield context.call_activity("publish_content", draft_content)
            return result
        return "Content rejected"

    # Timeout occurred - escalate for review
    result = yield context.call_activity("escalate_for_review", draft_content)
    return result
```

::: zone-end

Deterministic agent orchestrations can wait for external events, durably persisting their state while waiting for human feedback, surviving failures, restarts, and extended waiting periods. When the human response arrives, the orchestration automatically resumes with full conversation context and execution state intact.

### Providing Human Input

To send approval or input to a waiting orchestration, you'll need to raise an external event to the orchestration instance using the Durable Functions client SDK. For example, a reviewer might approve content through a web form that calls:

::: zone pivot="programming-language-csharp"

```csharp
await client.RaiseEventAsync(instanceId, "ApprovalDecision", new HumanApprovalResponse 
{ 
    Approved = true,
    Feedback = "Looks great!"
});
```

::: zone-end

::: zone pivot="programming-language-python"

```python
approval_data = {
    "approved": True,
    "feedback": "Looks great!"
}
await client.raise_event(instance_id, "ApprovalDecision", approval_data)
```

::: zone-end

### Cost Efficiency

Human-in-the-loop workflows with durable agents are extremely cost-effective when hosted on the [Azure Functions Flex Consumption plan](/azure/azure-functions/flex-consumption-plan). For a workflow waiting 24 hours for approval, you only pay for a few seconds of execution time (the time to generate content, send notification, and process the response)—not the 24 hours of waiting. During the wait period, no compute resources are consumed.

## Observability with Durable Task Scheduler

The [Durable Task Scheduler](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler) (DTS) is the recommended durable backend for your durable agents, offering the best performance, fully managed infrastructure, and built-in observability through a UI dashboard. While Azure Functions can use other storage backends (like Azure Storage), DTS is optimized specifically for durable workloads and provides superior performance and monitoring capabilities.

### Agent Thread Insights

- **Conversation history**: View complete conversation threads for each agent thread, including all messages, tool calls, and conversation context at any point in time
- **Task timing**: Monitor how long specific tasks and agent interactions take to complete


### Orchestration Insights

- **Multi-agent visualization**: See the execution flow when calling multiple specialized agents with visual representation of parallel executions and conditional branching
- **Execution history**: Access detailed execution logs
- **Real-time monitoring**: Track active orchestrations, queued work items, and agent states across your deployment
- **Performance metrics**: Monitor agent response times, token usage, and orchestration duration


### Debugging Capabilities

- View structured agent outputs and tool call results
- Trace tool invocations and their outcomes
- Monitor external event handling for human-in-the-loop scenarios

The dashboard enables you to understand exactly what your agents are doing, diagnose issues quickly, and optimize performance based on real execution data.

## Related Content

- [User guide: create a Durable Agent](create-durable-agent.md)
- [Tutorial: Create and run a durable agent](../../../../tutorials/agents/create-and-run-durable-agent.md)
- [Durable Task Scheduler Overview](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler)
- [Durable Task Scheduler Dashboard](/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler-dashboard)
- [Azure Functions Overview](/azure/azure-functions/functions-overview)
