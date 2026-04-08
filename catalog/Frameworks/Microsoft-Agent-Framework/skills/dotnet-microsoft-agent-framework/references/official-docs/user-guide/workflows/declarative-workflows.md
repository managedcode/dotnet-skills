---
title: Declarative Workflows - Overview
description: Learn how to define workflows using YAML configuration files instead of programmatic code in Microsoft Agent Framework.
zone_pivot_groups: programming-languages
author: moonbox3
ms.topic: tutorial
ms.author: evmattso
ms.date: 1/12/2026
ms.service: agent-framework
---

# Declarative Workflows - Overview

Declarative workflows allow you to define workflow logic using YAML configuration files instead of writing programmatic code. This approach makes workflows easier to read, modify, and share across teams.

## Overview

With declarative workflows, you describe *what* your workflow should do rather than *how* to implement it. The framework handles the underlying execution, converting your YAML definitions into executable workflow graphs.

**Key benefits:**

- **Readable format**: YAML syntax is easy to understand, even for non-developers
- **Portable**: Workflow definitions can be shared, versioned, and modified without code changes
- **Rapid iteration**: Modify workflow behavior by editing configuration files
- **Consistent structure**: Predefined action types ensure workflows follow best practices

## When to Use Declarative vs. Programmatic Workflows

| Scenario | Recommended Approach |
|----------|---------------------|
| Standard orchestration patterns | Declarative |
| Workflows that change frequently | Declarative |
| Non-developers need to modify workflows | Declarative |
| Complex custom logic | Programmatic |
| Maximum flexibility and control | Programmatic |
| Integration with existing Python code | Programmatic |

::: zone pivot="programming-language-csharp"

> [!NOTE]
> Documentation for declarative workflows in .NET is coming soon. Please check back for updates.

::: zone-end

::: zone pivot="programming-language-python"

## Prerequisites

Before you begin, ensure you have:

- Python 3.10 - 3.13 (Python 3.14 is not yet supported due to PowerFx compatibility)
- The Agent Framework declarative package installed:

```bash
pip install agent-framework-declarative --pre
```

This package pulls in the underlying `agent-framework-core` automatically.

- Basic familiarity with YAML syntax
- Understanding of [workflow concepts](./overview.md)

## Basic YAML Structure

A declarative workflow consists of a few key elements:

```yaml
name: my-workflow
description: A brief description of what this workflow does

inputs:
  parameterName:
    type: string
    description: Description of the parameter

actions:
  - kind: ActionType
    id: unique_action_id
    displayName: Human readable name
    # Action-specific properties
```

### Structure Elements

| Element | Required | Description |
|---------|----------|-------------|
| `name` | Yes | Unique identifier for the workflow |
| `description` | No | Human-readable description |
| `inputs` | No | Input parameters the workflow accepts |
| `actions` | Yes | List of actions to execute |

## Your First Declarative Workflow

Let's create a simple workflow that greets a user by name.

### Step 1: Create the YAML File

Create a file named `greeting-workflow.yaml`:

```yaml
name: greeting-workflow
description: A simple workflow that greets the user

inputs:
  name:
    type: string
    description: The name of the person to greet

actions:
  # Set a greeting prefix
  - kind: SetVariable
    id: set_greeting
    displayName: Set greeting prefix
    variable: Local.greeting
    value: Hello

  # Build the full message using an expression
  - kind: SetVariable
    id: build_message
    displayName: Build greeting message
    variable: Local.message
    value: =Concat(Local.greeting, ", ", Workflow.Inputs.name, "!")

  # Send the greeting to the user
  - kind: SendActivity
    id: send_greeting
    displayName: Send greeting to user
    activity:
      text: =Local.message

  # Store the result in outputs
  - kind: SetVariable
    id: set_output
    displayName: Store result in outputs
    variable: Workflow.Outputs.greeting
    value: =Local.message
```

### Step 2: Load and Run the Workflow

Create a Python file to execute the workflow:

```python
import asyncio
from pathlib import Path

from agent_framework.declarative import WorkflowFactory


async def main() -> None:
    """Run the greeting workflow."""
    # Create a workflow factory
    factory = WorkflowFactory()

    # Load the workflow from YAML
    workflow_path = Path(__file__).parent / "greeting-workflow.yaml"
    workflow = factory.create_workflow_from_yaml_path(workflow_path)

    print(f"Loaded workflow: {workflow.name}")
    print("-" * 40)

    # Run with a name input
    result = await workflow.run({"name": "Alice"})
    for output in result.get_outputs():
        print(f"Output: {output}")


if __name__ == "__main__":
    asyncio.run(main())
```

### Expected Output

```
Loaded workflow: greeting-workflow
----------------------------------------
Output: Hello, Alice!
```

## Core Concepts

### Variable Namespaces

Declarative workflows use namespaced variables to organize state:

| Namespace | Description | Example |
|-----------|-------------|---------|
| `Local.*` | Variables local to the workflow | `Local.message` |
| `Workflow.Inputs.*` | Input parameters | `Workflow.Inputs.name` |
| `Workflow.Outputs.*` | Output values | `Workflow.Outputs.result` |
| `System.*` | System-provided values | `System.ConversationId` |

### Expression Language

Values prefixed with `=` are evaluated as expressions:

```yaml
# Literal value (no evaluation)
value: Hello

# Expression (evaluated at runtime)
value: =Concat("Hello, ", Workflow.Inputs.name)
```

Common functions include:
- `Concat(str1, str2, ...)` - Concatenate strings
- `If(condition, trueValue, falseValue)` - Conditional expression
- `IsBlank(value)` - Check if value is empty

### Action Types

Declarative workflows support various action types:

| Category | Actions |
|----------|---------|
| Variable Management | `SetVariable`, `AppendValue`, `ResetVariable` |
| Control Flow | `If`, `ConditionGroup`, `Foreach`, `RepeatUntil` |
| Output | `SendActivity`, `EmitEvent` |
| Agent Invocation | `InvokeAzureAgent` |
| Human-in-the-Loop | `Question`, `Confirmation`, `RequestExternalInput` |
| Workflow Control | `EndWorkflow`, `EndConversation` |

::: zone-end

## Next Steps

- [Expressions and Variables](./declarative-workflows/expressions.md) - Learn the expression language and variable namespaces
- [Actions Reference](./declarative-workflows/actions-reference.md) - Complete reference for all action types
- [Advanced Patterns](./declarative-workflows/advanced-patterns.md) - Multi-agent orchestration and complex scenarios
- [Python Declarative Workflow Samples](https://github.com/microsoft/agent-framework/tree/main/python/samples/getting_started/workflows/declarative) - Explore complete working examples
