---
title: Declarative Workflows - Expressions and Variables
description: Learn about variable namespaces and the expression language in declarative workflows.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 1/12/2026
ms.service: agent-framework
---

# Declarative Workflows - Expressions and Variables

This document covers the expression language and variable management system used in declarative workflows.

## Overview

Declarative workflows use a namespaced variable system and a PowerFx-like expression language to manage state and compute dynamic values. Understanding these concepts is essential for building effective workflows.

::: zone pivot="programming-language-csharp"

> [!NOTE]
> Documentation for declarative workflows in .NET is coming soon. Please check back for updates.

::: zone-end

::: zone pivot="programming-language-python"

## Variable Namespaces

Variables in declarative workflows are organized into namespaces that determine their scope and purpose.

### Available Namespaces

| Namespace | Description | Access |
|-----------|-------------|--------|
| `Local.*` | Workflow-local variables | Read/Write |
| `Workflow.Inputs.*` | Input parameters passed to the workflow | Read-only |
| `Workflow.Outputs.*` | Values returned from the workflow | Read/Write |
| `System.*` | System-provided values | Read-only |
| `Agent.*` | Results from agent invocations | Read-only |

### Local Variables

Use `Local.*` for temporary values during workflow execution:

```yaml
actions:
  - kind: SetVariable
    variable: Local.counter
    value: 0

  - kind: SetVariable
    variable: Local.message
    value: "Processing..."

  - kind: SetVariable
    variable: Local.items
    value: []
```

### Workflow Inputs

Access input parameters using `Workflow.Inputs.*`:

```yaml
name: process-order
inputs:
  orderId:
    type: string
    description: The order ID to process
  quantity:
    type: integer
    description: Number of items

actions:
  - kind: SetVariable
    variable: Local.order
    value: =Workflow.Inputs.orderId

  - kind: SetVariable
    variable: Local.total
    value: =Workflow.Inputs.quantity
```

### Workflow Outputs

Store results in `Workflow.Outputs.*` to return values from the workflow:

```yaml
actions:
  - kind: SetVariable
    variable: Local.result
    value: "Calculation complete"

  - kind: SetVariable
    variable: Workflow.Outputs.status
    value: success

  - kind: SetVariable
    variable: Workflow.Outputs.message
    value: =Local.result
```

### System Variables

Access system-provided values through the `System.*` namespace:

| Variable | Description |
|----------|-------------|
| `System.ConversationId` | Current conversation identifier |
| `System.LastMessage` | The most recent message |
| `System.Timestamp` | Current timestamp |

```yaml
actions:
  - kind: SetVariable
    variable: Local.conversationRef
    value: =System.ConversationId
```

### Agent Variables

After invoking an agent, access response data through `Agent.*`:

```yaml
actions:
  - kind: InvokeAzureAgent
    id: call_assistant
    agent:
      name: MyAgent
    output:
      responseObject: Local.AgentResult

  # Access agent response
  - kind: SendActivity
    activity:
      text: =Local.AgentResult.text
```

## Expression Language

Values prefixed with `=` are evaluated as expressions at runtime.

### Literal vs. Expression Values

```yaml
# Literal string (stored as-is)
value: Hello World

# Expression (evaluated at runtime)
value: =Concat("Hello ", Workflow.Inputs.name)

# Literal number
value: 42

# Expression returning a number
value: =Workflow.Inputs.quantity * 2
```

### String Operations

#### Concat

Concatenate multiple strings:

```yaml
value: =Concat("Hello, ", Workflow.Inputs.name, "!")
# Result: "Hello, Alice!" (if Workflow.Inputs.name is "Alice")

value: =Concat(Local.firstName, " ", Local.lastName)
# Result: "John Doe" (if firstName is "John" and lastName is "Doe")
```

#### IsBlank

Check if a value is empty or undefined:

```yaml
condition: =IsBlank(Workflow.Inputs.optionalParam)
# Returns true if the parameter is not provided

value: =If(IsBlank(Workflow.Inputs.name), "Guest", Workflow.Inputs.name)
# Returns "Guest" if name is blank, otherwise returns the name
```

### Conditional Expressions

#### If Function

Return different values based on a condition:

```yaml
value: =If(Workflow.Inputs.age < 18, "minor", "adult")

value: =If(Local.count > 0, "Items found", "No items")

# Nested conditions
value: =If(Workflow.Inputs.role = "admin", "Full access", If(Workflow.Inputs.role = "user", "Limited access", "No access"))
```

### Logical Operations

#### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equal to | `=Workflow.Inputs.status = "active"` |
| `<>` | Not equal to | `=Workflow.Inputs.status <> "deleted"` |
| `<` | Less than | `=Workflow.Inputs.age < 18` |
| `>` | Greater than | `=Workflow.Inputs.count > 0` |
| `<=` | Less than or equal | `=Workflow.Inputs.score <= 100` |
| `>=` | Greater than or equal | `=Workflow.Inputs.quantity >= 1` |

#### Boolean Functions

```yaml
# Or - returns true if any condition is true
condition: =Or(Workflow.Inputs.role = "admin", Workflow.Inputs.role = "moderator")

# And - returns true if all conditions are true
condition: =And(Workflow.Inputs.age >= 18, Workflow.Inputs.hasConsent)

# Not - negates a condition
condition: =Not(IsBlank(Workflow.Inputs.email))
```

### Mathematical Operations

```yaml
# Addition
value: =Workflow.Inputs.price + Workflow.Inputs.tax

# Subtraction
value: =Workflow.Inputs.total - Workflow.Inputs.discount

# Multiplication
value: =Workflow.Inputs.quantity * Workflow.Inputs.unitPrice

# Division
value: =Workflow.Inputs.total / Workflow.Inputs.count
```

## Practical Examples

### Example 1: User Categorization

```yaml
name: categorize-user
inputs:
  age:
    type: integer
    description: User's age

actions:
  - kind: SetVariable
    variable: Local.age
    value: =Workflow.Inputs.age

  - kind: SetVariable
    variable: Local.category
    value: =If(Local.age < 13, "child", If(Local.age < 20, "teenager", If(Local.age < 65, "adult", "senior")))

  - kind: SendActivity
    activity:
      text: =Concat("You are categorized as: ", Local.category)

  - kind: SetVariable
    variable: Workflow.Outputs.category
    value: =Local.category
```

### Example 2: Conditional Greeting

```yaml
name: smart-greeting
inputs:
  name:
    type: string
    description: User's name (optional)
  timeOfDay:
    type: string
    description: morning, afternoon, or evening

actions:
  # Set the greeting based on time of day
  - kind: SetVariable
    variable: Local.timeGreeting
    value: =If(Workflow.Inputs.timeOfDay = "morning", "Good morning", If(Workflow.Inputs.timeOfDay = "afternoon", "Good afternoon", "Good evening"))

  # Handle optional name
  - kind: SetVariable
    variable: Local.userName
    value: =If(IsBlank(Workflow.Inputs.name), "friend", Workflow.Inputs.name)

  # Build the full greeting
  - kind: SetVariable
    variable: Local.fullGreeting
    value: =Concat(Local.timeGreeting, ", ", Local.userName, "!")

  - kind: SendActivity
    activity:
      text: =Local.fullGreeting
```

### Example 3: Input Validation

```yaml
name: validate-order
inputs:
  quantity:
    type: integer
    description: Number of items to order
  email:
    type: string
    description: Customer email

actions:
  # Check if inputs are valid
  - kind: SetVariable
    variable: Local.isValidQuantity
    value: =And(Workflow.Inputs.quantity > 0, Workflow.Inputs.quantity <= 100)

  - kind: SetVariable
    variable: Local.hasEmail
    value: =Not(IsBlank(Workflow.Inputs.email))

  - kind: SetVariable
    variable: Local.isValid
    value: =And(Local.isValidQuantity, Local.hasEmail)

  - kind: If
    condition: =Local.isValid
    then:
      - kind: SendActivity
        activity:
          text: "Order validated successfully!"
    else:
      - kind: SendActivity
        activity:
          text: =If(Not(Local.isValidQuantity), "Invalid quantity (must be 1-100)", "Email is required")
```

::: zone-end

## Next Steps

- [Actions Reference](./actions-reference.md) - Complete reference for all action types
- [Advanced Patterns](./advanced-patterns.md) - Multi-agent orchestration and complex scenarios
