---
title: Microsoft Agent Framework Workflows - Checkpoints
description: In-depth look at Checkpoints in Microsoft Agent Framework Workflows.
zone_pivot_groups: programming-languages
author: TaoChenOSU
ms.topic: tutorial
ms.author: taochen
ms.date: 07/30/2026
ms.service: agent-framework
---

<!--
  Language parity table – keep in sync when adding/removing sections.

    | Section                        | C# | Python | Go | Notes |
    |--------------------------------|:--:|:------:|:--:|-------|
    | Overview                       | ✅ |   ✅   | ✅ |       |
    | When Are Checkpoints Created?  | ✅ |   ✅   | ✅ |       |
    | Capturing Checkpoints          | ✅ |   ✅   | ✅ |       |
    | Resuming from Checkpoints      | ✅ |   ✅   | ✅ |       |
    | Rehydrating from Checkpoints   | ✅ |   ✅   | ✅ |       |
    | Save Executor States           | ✅ |   ✅   | ✅ |       |
    | Security Considerations        | ✅ |   ✅   | ✅ |       |
    | Next Steps                     | ✅ |   ✅   | ✅ |       |
-->

# Microsoft Agent Framework Workflows - Checkpoints

This page provides an overview of **Checkpoints** in the Microsoft Agent Framework Workflow system.

## Overview

Checkpoints allow you to save the state of a workflow at specific points during its execution, and resume from those points later. This feature is particularly useful for the following scenarios:

- Long-running workflows where you want to avoid losing progress in case of failures.
- Long-running workflows where you want to pause and resume execution at a later time.
- Workflows that require periodic state saving for auditing or compliance purposes.
- Workflows that need to be migrated across different environments or instances.

## When Are Checkpoints Created?

Remember that workflows are executed in **supersteps**, as documented in the [workflow execution model](../concepts/workflows/builder-and-execution.md#execution-model-supersteps). Checkpoints are created at the end of each superstep, after all executors in that superstep have completed their execution. A checkpoint captures the entire state of the workflow, including:

- The current state of all executors
- All pending messages in the workflow for the next superstep
- Pending requests and responses
- Shared states

::: zone pivot="programming-language-python"

> [!NOTE]
> Starting in Python version 1.13.0, workflows also create an entry checkpoint before the first superstep to record the workflow input, and another entry checkpoint when responses to request events are delivered. These checkpoints make the complete workflow run replayable. This release includes minor breaking changes for applications that depend on iteration counts, message source IDs, or checkpoint ordering. Existing checkpoints remain supported. For migration details, see [Upgrade Python workflow checkpoints to 1.13.0](../support/upgrade/python-1.13.0-workflow-checkpoint-upgrade-guide.md).

::: zone-end

## Capturing Checkpoints

::: zone pivot="programming-language-csharp"

To enable checkpointing, a `CheckpointManager` needs to be provided when running the workflow. A checkpoint can then be accessed via a `SuperStepCompletedEvent`, or through the `Checkpoints` property on the run.

```csharp
using Microsoft.Agents.AI.Workflows;

// Create a checkpoint manager to manage checkpoints
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

// Run the workflow with checkpointing enabled
StreamingRun run = await InProcessExecution
    .RunStreamingAsync(workflow, input, checkpointManager)
    .ConfigureAwait(false);
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    if (evt is SuperStepCompletedEvent superStepCompletedEvt)
    {
        // Access the checkpoint
        CheckpointInfo? checkpoint = superStepCompletedEvt.CompletionInfo?.Checkpoint;
    }
}

// Checkpoints can also be accessed from the run directly
IReadOnlyList<CheckpointInfo> checkpoints = run.Checkpoints;
```

::: zone-end

::: zone pivot="programming-language-python"

To enable checkpointing, a `CheckpointStorage` needs to be provided when creating a workflow. A checkpoint can then be accessed via the storage. Agent Framework ships three built-in implementations — pick the one that matches your durability and deployment needs:

| Provider | Package | Durability | Best for |
|---|---|---|---|
| `InMemoryCheckpointStorage` | `agent-framework` | In-process only | Tests, demos, short-lived workflows |
| `FileCheckpointStorage` | `agent-framework` | Local disk | Single-machine workflows, local development |
| `CosmosCheckpointStorage` | `agent-framework-azure-cosmos` | Azure Cosmos DB | Production, distributed, cross-process workflows |

All three implement the same `CheckpointStorage` protocol, so you can swap providers without changing workflow or executor code.

# [In-Memory](#tab/py-ckpt-inmemory)

`InMemoryCheckpointStorage` keeps checkpoints in process memory. Best for tests, demos, and short-lived workflows where you do not need durability across restarts.

```python
from agent_framework import (
    InMemoryCheckpointStorage,
    WorkflowBuilder,
)

# Create a checkpoint storage to manage checkpoints
checkpoint_storage = InMemoryCheckpointStorage()

# Build a workflow with checkpointing enabled
builder = WorkflowBuilder(start_executor=start_executor, checkpoint_storage=checkpoint_storage)
builder.add_edge(start_executor, executor_b)
builder.add_edge(executor_b, executor_c)
builder.add_edge(executor_b, end_executor)
workflow = builder.build()

# Run the workflow
async for event in workflow.run(input, stream=True):
    ...

# Access checkpoints from the storage
checkpoints = await checkpoint_storage.list_checkpoints(workflow_name=workflow.name)
```

# [File](#tab/py-ckpt-file)

`FileCheckpointStorage` persists checkpoints to a local directory on disk. Best for single-machine workflows that need to survive process restarts, and for local development.

```python
from agent_framework import (
    FileCheckpointStorage,
    WorkflowBuilder,
)

# Create a checkpoint storage backed by a directory on disk.
# storage_path is required — there is no default directory.
checkpoint_storage = FileCheckpointStorage("/var/lib/agent-framework/checkpoints")

# Build a workflow with checkpointing enabled
builder = WorkflowBuilder(start_executor=start_executor, checkpoint_storage=checkpoint_storage)
builder.add_edge(start_executor, executor_b)
builder.add_edge(executor_b, executor_c)
builder.add_edge(executor_b, end_executor)
workflow = builder.build()

# Run the workflow
async for event in workflow.run(input, stream=True):
    ...

# Access checkpoints from the storage
checkpoints = await checkpoint_storage.list_checkpoints(workflow_name=workflow.name)
```

See the [Security Considerations](#security-considerations) section for guidance on restricting which Python types can be deserialized via the `allowed_checkpoint_types` parameter.

# [Azure Cosmos DB](#tab/py-ckpt-cosmos)

`CosmosCheckpointStorage` persists checkpoints to Azure Cosmos DB NoSQL. Best for production and distributed workflows that need durable, cross-process checkpointing. Install the optional provider package:

```bash
pip install agent-framework-azure-cosmos --pre
```

The database and container are created automatically on first use, with `/workflow_name` as the partition key for efficient per-workflow queries. The recommended authentication mode is managed identity / RBAC via an Azure `TokenCredential` such as `DefaultAzureCredential`:

```python
from azure.identity.aio import DefaultAzureCredential
from agent_framework import WorkflowBuilder
from agent_framework_azure_cosmos import CosmosCheckpointStorage

# CosmosCheckpointStorage is an async context manager — it closes the underlying
# Cosmos client on exit when it created the client itself.
async with (
    DefaultAzureCredential() as credential,
    CosmosCheckpointStorage(
        endpoint="https://<account>.documents.azure.com:443/",
        credential=credential,
        database_name="agent-framework",
        container_name="workflow-checkpoints",
    ) as checkpoint_storage,
):
    # Build a workflow with checkpointing enabled
    builder = WorkflowBuilder(start_executor=start_executor, checkpoint_storage=checkpoint_storage)
    builder.add_edge(start_executor, executor_b)
    builder.add_edge(executor_b, executor_c)
    builder.add_edge(executor_b, end_executor)
    workflow = builder.build()

    # Run the workflow
    async for event in workflow.run(input, stream=True):
        ...

    # Access checkpoints from the storage
    checkpoints = await checkpoint_storage.list_checkpoints(workflow_name=workflow.name)
```

Account key authentication is also supported by passing the key directly as the `credential` argument:

```python
from agent_framework_azure_cosmos import CosmosCheckpointStorage

checkpoint_storage = CosmosCheckpointStorage(
    endpoint="https://<account>.documents.azure.com:443/",
    credential="<your-account-key>",
    database_name="agent-framework",
    container_name="workflow-checkpoints",
)
```

Connection details can also be supplied entirely through environment variables:

| Variable | Description |
|---|---|
| `AZURE_COSMOS_ENDPOINT` | Cosmos DB account endpoint |
| `AZURE_COSMOS_DATABASE_NAME` | Database name |
| `AZURE_COSMOS_CONTAINER_NAME` | Container name |
| `AZURE_COSMOS_KEY` | Account key (optional if using Azure credentials) |

`CosmosCheckpointStorage` also accepts a pre-created `CosmosClient` (via `cosmos_client=`) or `ContainerProxy` (via `container_client=`) if your application already manages the Cosmos client lifecycle.

---

::: zone-end

::: zone pivot="programming-language-go"

To enable checkpointing, configure the execution environment with a checkpoint manager. A checkpoint can then be accessed from `workflow.SuperStepCompletedEvent`, or through the run's checkpoint list.

```go
checkpointManager := checkpoint.NewInMemoryManager()

run, err := inproc.Default.
    WithCheckpointing(checkpointManager).
    RunStreaming(ctx, wf, input)
if err != nil {
    return err
}
defer run.Close(ctx)

var checkpoints []workflow.CheckpointInfo
for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }
    if completed, ok := evt.(workflow.SuperStepCompletedEvent); ok && completed.CompletionInfo != nil {
        if completed.CompletionInfo.CheckpointInfo != nil {
            checkpoints = append(checkpoints, *completed.CompletionInfo.CheckpointInfo)
        }
    }
}

// Checkpoints can also be accessed from the run directly.
checkpoints = run.Checkpoints()
```

::: zone-end

## Resuming from Checkpoints

::: zone pivot="programming-language-csharp"

You can resume a workflow from a specific checkpoint directly on the same run.

```csharp
// Assume we want to resume from the 6th checkpoint
CheckpointInfo savedCheckpoint = run.Checkpoints[5];
// Restore the state directly on the same run instance.
await run.RestoreCheckpointAsync(savedCheckpoint).ConfigureAwait(false);
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    if (evt is WorkflowOutputEvent workflowOutputEvt)
    {
        Console.WriteLine($"Workflow completed with result: {workflowOutputEvt.Data}");
    }
}
```

::: zone-end

::: zone pivot="programming-language-python"

You can resume a workflow from a specific checkpoint directly on the same workflow instance.

```python
# Assume we want to resume from the 6th checkpoint
saved_checkpoint = checkpoints[5]
async for event in workflow.run(checkpoint_id=saved_checkpoint.checkpoint_id, stream=True):
    ...
```

::: zone-end

::: zone pivot="programming-language-go"

You can restore a streaming run to a specific checkpoint directly on the same run.

```go
// Assume we want to resume from the 6th checkpoint.
savedCheckpoint := checkpoints[5]
if err := run.RestoreCheckpoint(ctx, savedCheckpoint); err != nil {
    return err
}

for evt, err := range run.WatchStream(ctx) {
    if err != nil {
        return err
    }
    if outputEvent, ok := evt.(workflow.OutputEvent); ok {
        fmt.Printf("Workflow completed with result: %v\n", outputEvent.Output)
    }
}
```

::: zone-end

## Rehydrating from Checkpoints

A rehydrated workflow must preserve the topology and executor identities of the workflow that created the checkpoint. How executor identity is resolved depends on the SDK and executor type.

::: zone pivot="programming-language-csharp"

Or you can rehydrate a workflow from a checkpoint into a new run instance.

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/03-workflows/Checkpoint/CheckpointAndRehydrate/Program.cs" id="rehydrate_workflow":::

> [!IMPORTANT]
> The workflow passed to `ResumeStreamingAsync` must have the same structure and executor identities as the workflow that created the checkpoint. If the workflow contains local `ChatClientAgent` instances that are reconstructed across requests, dependency injection scopes, processes, or deployments, assign each agent a stable `ChatClientAgentOptions.Id`. If an agent also sets a `Name`, keep that `Name` unchanged as well.

For example, assign an ID that represents the agent's logical role:

:::code language="csharp" source="~/../agent-framework-code/dotnet/samples/03-workflows/Orchestration/Handoff/AgentRegistry.cs" id="stable_agent_identity":::

Apply this pattern to every agent that participates in the workflow. Agent IDs must be unique within the workflow and must be reused when reconstructing the same logical agent. Don't use conversation IDs, request IDs, user IDs, personally identifiable information, or secrets as agent IDs.

When an agent `Name` is set, the current .NET workflow executor identity is derived from both its `Name` and `Id`, so changing either value makes the rebuilt workflow incompatible with the checkpoint. Assigning stable values does not repair checkpoints created with different or randomly generated IDs; start a new session and checkpoint lineage instead.

For related scenarios, see [Workflows as Agents](./as-agents.md#session-serialization-and-resumption) and [Handoff orchestration](./orchestrations/handoff.md#define-your-specialized-agents).

::: zone-end

::: zone pivot="programming-language-python"

Or you can rehydrate a new workflow instance from a checkpoint.

```python
from agent_framework import WorkflowBuilder

builder = WorkflowBuilder(start_executor=start_executor)
builder.add_edge(start_executor, executor_b)
builder.add_edge(executor_b, executor_c)
builder.add_edge(executor_b, end_executor)
# This workflow instance doesn't require checkpointing enabled.
workflow = builder.build()

# Assume we want to resume from the 6th checkpoint
saved_checkpoint = checkpoints[5]
async for event in workflow.run(
    checkpoint_id=saved_checkpoint.checkpoint_id,
    checkpoint_storage=checkpoint_storage,
    stream=True,
):
    ...
```

::: zone-end

::: zone pivot="programming-language-go"

Or you can rehydrate a new workflow instance from a checkpoint.

```go
// Assume we want to resume from the 6th checkpoint
savedCheckpoint := checkpoints[5]
newWorkflow := buildWorkflow()

newRun, err := inproc.Default.
    WithCheckpointing(checkpointManager).
    ResumeStreaming(ctx, newWorkflow, savedCheckpoint)
if err != nil {
    return err
}
defer newRun.Close(ctx)

for evt, err := range newRun.WatchStream(ctx) {
    if err != nil {
        return err
    }
    if outputEvent, ok := evt.(workflow.OutputEvent); ok {
        fmt.Printf("Workflow completed with result: %v\n", outputEvent.Output)
    }
}
```

::: zone-end

## Save Executor States

::: zone pivot="programming-language-csharp"

To ensure that the state of an executor is captured in a checkpoint, the executor must override the `OnCheckpointingAsync` method and save its state to the workflow context.

```csharp
using Microsoft.Agents.AI.Workflows;

internal sealed partial class CustomExecutor() : Executor("CustomExecutor")
{
    private const string StateKey = "CustomExecutorState";

    private List<string> messages = new();

    [MessageHandler]
    private async ValueTask HandleAsync(string message, IWorkflowContext context)
    {
        this.messages.Add(message);
        // Executor logic...
    }

    protected override ValueTask OnCheckpointingAsync(IWorkflowContext context, CancellationToken cancellation = default)
    {
        return context.QueueStateUpdateAsync(StateKey, this.messages);
    }
}
```

Also, to ensure the state is correctly restored when resuming from a checkpoint, the executor must override the `OnCheckpointRestoredAsync` method and load its state from the workflow context.

```csharp
protected override async ValueTask OnCheckpointRestoredAsync(IWorkflowContext context, CancellationToken cancellation = default)
{
    this.messages = await context.ReadStateAsync<List<string>>(StateKey).ConfigureAwait(false);
}
```

::: zone-end

::: zone pivot="programming-language-python"

To ensure that the state of an executor is captured in a checkpoint, the executor must override the `on_checkpoint_save` method and return its state as a dictionary.

```python
class CustomExecutor(Executor):
    def __init__(self, id: str) -> None:
        super().__init__(id=id)
        self._messages: list[str] = []

    @handler
    async def handle(self, message: str, ctx: WorkflowContext):
        self._messages.append(message)
        # Executor logic...

    async def on_checkpoint_save(self) -> dict[str, Any]:
        return {"messages": self._messages}
```

Also, to ensure the state is correctly restored when resuming from a checkpoint, the executor must override the `on_checkpoint_restore` method and restore its state from the provided state dictionary.

```python
async def on_checkpoint_restore(self, state: dict[str, Any]) -> None:
    self._messages = state.get("messages", [])
```

::: zone-end

::: zone pivot="programming-language-go"

To ensure that executor state is captured in a checkpoint, attach checkpoint hooks to the executor and store state through the workflow context.

```go
type customExecutor struct {
    messages []string
}

func (e *customExecutor) Handle(message string) {
    e.messages = append(e.messages, message)
}

func (e *customExecutor) OnCheckpoint(ctx *workflow.Context) error {
    return ctx.QueueStateUpdate("CustomExecutorState", "", slices.Clone(e.messages))
}
```

Restore the state in `OnCheckpointRestoredFunc`:

```go
func (e *customExecutor) OnCheckpointRestored(ctx *workflow.Context) error {
    value, err := ctx.ReadState("CustomExecutorState", "")
    if err != nil {
        return err
    }
    if value == nil {
        e.messages = nil
        return nil
    }

    messages, ok := value.([]string)
    if !ok {
        return fmt.Errorf("unexpected custom executor state type %T", value)
    }
    e.messages = slices.Clone(messages)
    return nil
}

executorState := &customExecutor{}
custom := workflow.NewExecutor("CustomExecutor", executorState).Extend(&workflow.Executor{
    OnCheckpointFunc:         executorState.OnCheckpoint,
    OnCheckpointRestoredFunc: executorState.OnCheckpointRestored,
}).Bind()
```

::: zone-end

## Security Considerations

> [!IMPORTANT]
> Checkpoint storage is a trust boundary. Whether you use the built-in storage implementations or a custom one, the storage backend must be treated as trusted, private infrastructure. **Never load checkpoints from untrusted or potentially tampered sources.**

::: zone pivot="programming-language-csharp"

Ensure that the storage location used for checkpoints is secured appropriately. Only authorized services and users should have read or write access to checkpoint data.

::: zone-end

::: zone pivot="programming-language-python"

### Pickle serialization

Both `FileCheckpointStorage` and `CosmosCheckpointStorage` use Python's [`pickle`](https://docs.python.org/3/library/pickle.html) module to serialize non-JSON-native state such as dataclasses, datetimes, and custom objects. To mitigate the risks of arbitrary code execution during deserialization, both providers use a **restricted unpickler** by default. Only a built-in set of safe Python types (primitives, `datetime`, `uuid`, `Decimal`, common collections, etc.) and supported Agent Framework or OpenAI SDK types are permitted during deserialization. Module-prefix allowlisting is type-only: helper functions and other non-type globals are rejected. Any unsupported type causes deserialization to fail with a `WorkflowCheckpointException`.

To allow additional application-specific types, pass them via the `allowed_checkpoint_types` parameter using `"module:qualname"` format:

```python
from agent_framework import FileCheckpointStorage

storage = FileCheckpointStorage(
    "/tmp/checkpoints",
    allowed_checkpoint_types=[
        "my_app.models:SafeState",
        "my_app.models:UserProfile",
    ],
)
```

Each `allowed_checkpoint_types` entry must resolve to a type. Adding a module-level function or another non-type global doesn't make that global deserializable.

`CosmosCheckpointStorage` accepts the same parameter:

```python
from azure.identity.aio import DefaultAzureCredential
from agent_framework_azure_cosmos import CosmosCheckpointStorage

storage = CosmosCheckpointStorage(
    endpoint="https://my-account.documents.azure.com:443/",
    credential=DefaultAzureCredential(),
    database_name="agent-db",
    container_name="checkpoints",
    allowed_checkpoint_types=[
        "my_app.models:SafeState",
        "my_app.models:UserProfile",
    ],
)
```

If your threat model does not permit pickle-based serialization at all, use `InMemoryCheckpointStorage` or implement a custom `CheckpointStorage` with an alternative serialization strategy.

### Storage location responsibility

`FileCheckpointStorage` requires an explicit `storage_path` parameter — there is no default directory. While the framework validates against path traversal attacks, securing the storage directory itself (file permissions, encryption at rest, access controls) is the developer's responsibility. Only authorized processes should have read or write access to the checkpoint directory.

`CosmosCheckpointStorage` relies on Azure Cosmos DB for storage. Use managed identity / RBAC where possible, scope the database and container to the workflow service, and rotate account keys if you use key-based auth. As with file storage, only authorized principals should have read or write access to the Cosmos DB container that holds checkpoint documents.

::: zone-end

::: zone pivot="programming-language-go"

Go checkpoint managers serialize checkpoint state as JSON, but checkpoint storage is still trusted application state. If you use `checkpoint.NewFileSystemJSONStore`, store checkpoint files in a protected directory and restrict read/write access to authorized processes only. Custom stores are responsible for their own access control, integrity, and durability guarantees.

::: zone-end

## Next Steps

- [Learn how to monitor workflows](./observability.md).
- [Learn about state isolation in workflows](../concepts/workflows/state.md).
- [Learn how to visualize workflows](./visualization.md).
