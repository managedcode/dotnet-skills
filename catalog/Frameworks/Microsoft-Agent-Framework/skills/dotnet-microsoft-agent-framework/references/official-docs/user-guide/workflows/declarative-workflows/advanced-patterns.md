---
title: Declarative Workflows - Advanced Patterns
description: Learn advanced orchestration patterns including multi-agent workflows, loops, and human-in-the-loop scenarios.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 1/12/2026
ms.service: agent-framework
---

# Declarative Workflows - Advanced Patterns

This document covers advanced patterns for building sophisticated declarative workflows, including multi-agent orchestration, loop control, and human-in-the-loop scenarios.

## Overview

As your workflows grow in complexity, you'll need patterns that handle multi-step processes, agent coordination, and interactive scenarios. This guide provides templates and best practices for common advanced use cases.

::: zone pivot="programming-language-csharp"

> [!NOTE]
> Documentation for declarative workflows in .NET is coming soon. Please check back for updates.

::: zone-end

::: zone pivot="programming-language-python"

## Multi-Agent Orchestration

### Sequential Agent Pipeline

Pass work through multiple agents in sequence, where each agent builds on the previous agent's output.

**Use case**: Content creation pipelines where different specialists handle research, writing, and editing.

```yaml
name: content-pipeline
description: Sequential agent pipeline for content creation

kind: Workflow
trigger:
  kind: OnConversationStart
  id: content_workflow
  actions:
    # First agent: Research and analyze
    - kind: InvokeAzureAgent
      id: invoke_researcher
      displayName: Research phase
      conversationId: =System.ConversationId
      agent:
        name: ResearcherAgent

    # Second agent: Write draft based on research
    - kind: InvokeAzureAgent
      id: invoke_writer
      displayName: Writing phase
      conversationId: =System.ConversationId
      agent:
        name: WriterAgent

    # Third agent: Edit and polish
    - kind: InvokeAzureAgent
      id: invoke_editor
      displayName: Editing phase
      conversationId: =System.ConversationId
      agent:
        name: EditorAgent
```

**Python setup**:

```python
from agent_framework.declarative import WorkflowFactory

# Create factory and register agents
factory = WorkflowFactory()
factory.register_agent("ResearcherAgent", researcher_agent)
factory.register_agent("WriterAgent", writer_agent)
factory.register_agent("EditorAgent", editor_agent)

# Load and run
workflow = factory.create_workflow_from_yaml_path("content-pipeline.yaml")
result = await workflow.run({"topic": "AI in healthcare"})
```

### Conditional Agent Routing

Route requests to different agents based on the input or intermediate results.

**Use case**: Support systems that route to specialized agents based on issue type.

```yaml
name: support-router
description: Route to specialized support agents

inputs:
  category:
    type: string
    description: Support category (billing, technical, general)

actions:
  - kind: ConditionGroup
    id: route_request
    displayName: Route to appropriate agent
    conditions:
      - condition: =Workflow.Inputs.category = "billing"
        id: billing_route
        actions:
          - kind: InvokeAzureAgent
            id: billing_agent
            agent:
              name: BillingAgent
            conversationId: =System.ConversationId
      - condition: =Workflow.Inputs.category = "technical"
        id: technical_route
        actions:
          - kind: InvokeAzureAgent
            id: technical_agent
            agent:
              name: TechnicalAgent
            conversationId: =System.ConversationId
    elseActions:
      - kind: InvokeAzureAgent
        id: general_agent
        agent:
          name: GeneralAgent
        conversationId: =System.ConversationId
```

### Agent with External Loop

Continue agent interaction until a condition is met, such as the issue being resolved.

**Use case**: Support conversations that continue until the user's problem is solved.

```yaml
name: support-conversation
description: Continue support until resolved

actions:
  - kind: SetVariable
    variable: Local.IsResolved
    value: false

  - kind: InvokeAzureAgent
    id: support_agent
    displayName: Support agent with external loop
    agent:
      name: SupportAgent
    conversationId: =System.ConversationId
    input:
      externalLoop:
        when: =Not(Local.IsResolved)
    output:
      responseObject: Local.SupportResult

  - kind: SendActivity
    activity:
      text: "Thank you for contacting support. Your issue has been resolved."
```

## Loop Control Patterns

### Iterative Agent Conversation

Create back-and-forth conversations between agents with controlled iteration.

**Use case**: Student-teacher scenarios, debate simulations, or iterative refinement.

```yaml
name: student-teacher
description: Iterative learning conversation between student and teacher

kind: Workflow
trigger:
  kind: OnConversationStart
  id: learning_session
  actions:
    # Initialize turn counter
    - kind: SetVariable
      id: init_counter
      path: Local.TurnCount
      value: 0

    - kind: SendActivity
      id: start_message
      activity:
        text: =Concat("Starting session for: ", Workflow.Inputs.problem)

    # Student attempts solution (loop entry point)
    - kind: SendActivity
      id: student_label
      activity:
        text: "\n[Student]:"

    - kind: InvokeAzureAgent
      id: student_attempt
      conversationId: =System.ConversationId
      agent:
        name: StudentAgent

    # Teacher reviews
    - kind: SendActivity
      id: teacher_label
      activity:
        text: "\n[Teacher]:"

    - kind: InvokeAzureAgent
      id: teacher_review
      conversationId: =System.ConversationId
      agent:
        name: TeacherAgent
      output:
        messages: Local.TeacherResponse

    # Increment counter
    - kind: SetVariable
      id: increment
      path: Local.TurnCount
      value: =Local.TurnCount + 1

    # Check completion conditions
    - kind: ConditionGroup
      id: check_completion
      conditions:
        # Success: Teacher congratulated student
        - condition: =Not(IsBlank(Find("congratulations", Local.TeacherResponse)))
          id: success_check
          actions:
            - kind: SendActivity
              activity:
                text: "Session complete - student succeeded!"
            - kind: SetVariable
              variable: Workflow.Outputs.result
              value: success
        # Continue: Under turn limit
        - condition: =Local.TurnCount < 4
          id: continue_check
          actions:
            - kind: GotoAction
              actionId: student_label
      elseActions:
        # Timeout: Reached turn limit
        - kind: SendActivity
          activity:
            text: "Session ended - turn limit reached."
        - kind: SetVariable
          variable: Workflow.Outputs.result
          value: timeout
```

### Counter-Based Loops

Implement traditional counting loops using variables and GotoAction.

```yaml
name: counter-loop
description: Process items with a counter

actions:
  - kind: SetVariable
    variable: Local.counter
    value: 0

  - kind: SetVariable
    variable: Local.maxIterations
    value: 5

  # Loop start
  - kind: SetVariable
    id: loop_start
    variable: Local.counter
    value: =Local.counter + 1

  - kind: SendActivity
    activity:
      text: =Concat("Processing iteration ", Local.counter)

  # Your processing logic here
  - kind: SetVariable
    variable: Local.result
    value: =Concat("Result from iteration ", Local.counter)

  # Check if should continue
  - kind: If
    condition: =Local.counter < Local.maxIterations
    then:
      - kind: GotoAction
        actionId: loop_start
    else:
      - kind: SendActivity
        activity:
          text: "Loop complete!"
```

### Early Exit with BreakLoop

Use BreakLoop to exit iterations early when a condition is met.

```yaml
name: search-workflow
description: Search through items and stop when found

actions:
  - kind: SetVariable
    variable: Local.found
    value: false

  - kind: Foreach
    source: =Workflow.Inputs.items
    itemName: currentItem
    actions:
      # Check if this is the item we're looking for
      - kind: If
        condition: =currentItem.id = Workflow.Inputs.targetId
        then:
          - kind: SetVariable
            variable: Local.found
            value: true
          - kind: SetVariable
            variable: Local.result
            value: =currentItem
          - kind: BreakLoop

      - kind: SendActivity
        activity:
          text: =Concat("Checked item: ", currentItem.name)

  - kind: If
    condition: =Local.found
    then:
      - kind: SendActivity
        activity:
          text: =Concat("Found: ", Local.result.name)
    else:
      - kind: SendActivity
        activity:
          text: "Item not found"
```

## Human-in-the-Loop Patterns

### Interactive Survey

Collect multiple pieces of information from the user.

```yaml
name: customer-survey
description: Interactive customer feedback survey

actions:
  - kind: SendActivity
    activity:
      text: "Welcome to our customer feedback survey!"

  # Collect name
  - kind: Question
    id: ask_name
    question:
      text: "What is your name?"
    variable: Local.userName
    default: "Anonymous"

  - kind: SendActivity
    activity:
      text: =Concat("Nice to meet you, ", Local.userName, "!")

  # Collect rating
  - kind: Question
    id: ask_rating
    question:
      text: "How would you rate our service? (1-5)"
    variable: Local.rating
    default: "3"

  # Respond based on rating
  - kind: If
    condition: =Local.rating >= 4
    then:
      - kind: SendActivity
        activity:
          text: "Thank you for the positive feedback!"
    else:
      - kind: Question
        id: ask_improvement
        question:
          text: "What could we improve?"
        variable: Local.feedback

  # Collect additional feedback
  - kind: RequestExternalInput
    id: additional_comments
    prompt:
      text: "Any additional comments? (optional)"
    variable: Local.comments
    default: ""

  # Summary
  - kind: SendActivity
    activity:
      text: =Concat("Thank you, ", Local.userName, "! Your feedback has been recorded.")

  - kind: SetVariable
    variable: Workflow.Outputs.survey
    value:
      name: =Local.userName
      rating: =Local.rating
      feedback: =Local.feedback
      comments: =Local.comments
```

### Approval Workflow

Request approval before proceeding with an action.

```yaml
name: approval-workflow
description: Request approval before processing

inputs:
  requestType:
    type: string
    description: Type of request
  amount:
    type: number
    description: Request amount

actions:
  - kind: SendActivity
    activity:
      text: =Concat("Processing ", inputs.requestType, " request for $", inputs.amount)

  # Check if approval is needed
  - kind: If
    condition: =Workflow.Inputs.amount > 1000
    then:
      - kind: SendActivity
        activity:
          text: "This request requires manager approval."

      - kind: Confirmation
        id: get_approval
        question:
          text: =Concat("Do you approve this ", inputs.requestType, " request for $", inputs.amount, "?")
        variable: Local.approved

      - kind: If
        condition: =Local.approved
        then:
          - kind: SendActivity
            activity:
              text: "Request approved. Processing..."
          - kind: SetVariable
            variable: Workflow.Outputs.status
            value: approved
        else:
          - kind: SendActivity
            activity:
              text: "Request denied."
          - kind: SetVariable
            variable: Workflow.Outputs.status
            value: denied
    else:
      - kind: SendActivity
        activity:
          text: "Request auto-approved (under threshold)."
      - kind: SetVariable
        variable: Workflow.Outputs.status
        value: auto_approved
```

## Complex Orchestration

### Support Ticket Workflow

A comprehensive example combining multiple patterns: agent routing, conditional logic, and conversation management.

```yaml
name: support-ticket-workflow
description: Complete support ticket handling with escalation

kind: Workflow
trigger:
  kind: OnConversationStart
  id: support_workflow
  actions:
    # Initial self-service agent
    - kind: InvokeAzureAgent
      id: self_service
      displayName: Self-service agent
      agent:
        name: SelfServiceAgent
      conversationId: =System.ConversationId
      input:
        externalLoop:
          when: =Not(Local.ServiceResult.IsResolved)
      output:
        responseObject: Local.ServiceResult

    # Check if resolved by self-service
    - kind: If
      condition: =Local.ServiceResult.IsResolved
      then:
        - kind: SendActivity
          activity:
            text: "Issue resolved through self-service."
        - kind: SetVariable
          variable: Workflow.Outputs.resolution
          value: self_service
        - kind: EndWorkflow
          id: end_resolved

    # Create support ticket
    - kind: SendActivity
      activity:
        text: "Creating support ticket..."

    - kind: SetVariable
      variable: Local.TicketId
      value: =Concat("TKT-", System.ConversationId)

    # Route to appropriate team
    - kind: ConditionGroup
      id: route_ticket
      conditions:
        - condition: =Local.ServiceResult.Category = "technical"
          id: technical_route
          actions:
            - kind: InvokeAzureAgent
              id: technical_support
              agent:
                name: TechnicalSupportAgent
              conversationId: =System.ConversationId
              output:
                responseObject: Local.TechResult
        - condition: =Local.ServiceResult.Category = "billing"
          id: billing_route
          actions:
            - kind: InvokeAzureAgent
              id: billing_support
              agent:
                name: BillingSupportAgent
              conversationId: =System.ConversationId
              output:
                responseObject: Local.BillingResult
      elseActions:
        # Escalate to human
        - kind: SendActivity
          activity:
            text: "Escalating to human support..."
        - kind: SetVariable
          variable: Workflow.Outputs.resolution
          value: escalated

    - kind: SendActivity
      activity:
        text: =Concat("Ticket ", Local.TicketId, " has been processed.")
```

## Best Practices

### Naming Conventions

Use clear, descriptive names for actions and variables:

```yaml
# Good
- kind: SetVariable
  id: calculate_total_price
  variable: Local.orderTotal

# Avoid
- kind: SetVariable
  id: sv1
  variable: Local.x
```

### Organizing Large Workflows

Break complex workflows into logical sections with comments:

```yaml
actions:
  # === INITIALIZATION ===
  - kind: SetVariable
    id: init_status
    variable: Local.status
    value: started

  # === DATA COLLECTION ===
  - kind: Question
    id: collect_name
    # ...

  # === PROCESSING ===
  - kind: InvokeAzureAgent
    id: process_request
    # ...

  # === OUTPUT ===
  - kind: SendActivity
    id: send_result
    # ...
```

### Error Handling

Use conditional checks to handle potential issues:

```yaml
actions:
  - kind: SetVariable
    variable: Local.hasError
    value: false

  - kind: InvokeAzureAgent
    id: call_agent
    agent:
      name: ProcessingAgent
    output:
      responseObject: Local.AgentResult

  - kind: If
    condition: =IsBlank(Local.AgentResult)
    then:
      - kind: SetVariable
        variable: Local.hasError
        value: true
      - kind: SendActivity
        activity:
          text: "An error occurred during processing."
    else:
      - kind: SendActivity
        activity:
          text: =Local.AgentResult.message
```

### Testing Strategies

1. **Start simple**: Test basic flows before adding complexity
2. **Use default values**: Provide sensible defaults for inputs
3. **Add logging**: Use SendActivity for debugging during development
4. **Test edge cases**: Verify behavior with missing or invalid inputs

```yaml
# Debug logging example
- kind: SendActivity
  id: debug_log
  activity:
    text: =Concat("[DEBUG] Current state: counter=", Local.counter, ", status=", Local.status)
```

::: zone-end

## Next Steps

- [Declarative Workflows Overview](../declarative-workflows.md) - Return to the overview
- [Expressions and Variables](./expressions.md) - Learn the expression language
- [Actions Reference](./actions-reference.md) - Complete reference for all action types
