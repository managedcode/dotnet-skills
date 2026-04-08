---
title: Register Factories to Workflow Builder    
description: Learn how to register factories to the workflow builder.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 09/29/2025
ms.service: agent-framework
---

# Register Factories to Workflow Builder

Up to this point, we've been creating executor instances and passing them directly to the `WorkflowBuilder`. This approach works well for simple scenarios where you only need a single workflow instance. However, in more complex cases you may want to create multiple, isolated instances of the same workflow. To support this, each workflow instance must receive its own set of executor instances. Reusing the same executors would cause their internal state to be shared across workflows, resulting in unintended side effects. To avoid this, you can register executor factories with the `WorkflowBuilder`, ensuring that new executor instances are created for each workflow instance.

## Registering Factories to Workflow Builder

::: zone pivot="programming-language-csharp"

Coming soon...

::: zone-end

::: zone pivot="programming-language-python"

To register an executor factory to the `WorkflowBuilder`, you can use the `register_executor` method. This method takes two parameters: the factory function that creates instances of the executor (of type `Executor` or derivation of `Executor`) and the name of the factory to be used in the workflow configuration.

```python
class UpperCase(Executor):
    def __init__(self, id: str):
        super().__init__(id=id)

    @handler
    async def to_upper_case(self, text: str, ctx: WorkflowContext[str]) -> None:
        """Convert the input to uppercase and forward it to the next node."""
        result = text.upper()

        # Send the result to the next executor in the workflow.
        await ctx.send_message(result)

class Accumulate(Executor):
    def __init__(self, id: str):
        super().__init__(id=id)
        # Executor internal state that should not be shared among different workflow instances.
        self._text_length = 0

    @handler
    async def accumulate(self, text: str, ctx: WorkflowContext) -> None:
        """Accumulate the length of the input text and log it."""
        self._text_length += len(text)
        print(f"Accumulated text length: {self._text_length}")

@executor(id="reverse_text_executor")
async def reverse_text(text: str, ctx: WorkflowContext[str]) -> None:
    """Reverse the input string and send it downstream."""
    result = text[::-1]

    # Send the result to the next executor in the workflow.
    await ctx.yield_output(result)

workflow_builder = (
    WorkflowBuilder()
    .register_executor(
        factory_func=lambda: UpperCase(id="UpperCaseExecutor"),
        name="UpperCase",
    )
    .register_executor(
        factory_func=lambda: Accumulate(id="AccumulateExecutor"),
        name="Accumulate",
    )
    .register_executor(
        factory_func=lambda: reverse_text,
        name="ReverseText",
    )
    # Use the factory name to configure the workflow
    .add_fan_out_edges("UpperCase", ["Accumulate", "ReverseText"])
    .set_start_executor("UpperCase")
)
```

Build a workflow using the builder

```python
# Build the workflow using the builder
workflow_a = workflow_builder.build()
await workflow_a.run("hello world")
await workflow_a.run("hello world")
```

Expected output:

```plaintext
Accumulated text length: 22
```

Now let's create another workflow instance and run it. The `Accumulate` executor should have its own internal state and not share the state with the first workflow instance.

```python
# Build another workflow using the builder
# This workflow will have its own set of executors, including a new instance of the Accumulate executor.
workflow_b = workflow_builder.build()
await workflow_b.run("hello world")
```

Expected output:

```plaintext
Accumulated text length: 11
```

To register an agent factory to the `WorkflowBuilder`, you can use the `register_agent` method. This method takes two parameters: the factory function that creates instances of the agent (of types that implement `AgentProtocol`) and the name of the factory to be used in the workflow configuration.

```python
def create_agent() -> ChatAgent:
    """Factory function to create a Writer agent."""
    return AzureOpenAIChatClient(credential=AzureCliCredential()).as_agent(
        instructions=("You are a helpful assistant.",),
        name="assistant",
    )

workflow_builder = (
    WorkflowBuilder()
    .register_agent(
        factory_func=create_agent,
        name="Assistant",
    )
    # Register other executors or agents as needed and configure the workflow
    ...
)

# Build the workflow using the builder
workflow = workflow_builder.build()
```

Each time a new workflow instance is created, the agent in the workflow will be a new instance created by the factory function, and will get a new thread instance.

::: zone-end

## Workflow State Isolation

To learn more about workflow state isolation, refer to the [Workflow State Isolation](../../user-guide/workflows/state-isolation.md) documentation.
