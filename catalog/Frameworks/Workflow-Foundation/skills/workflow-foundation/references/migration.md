# Windows Workflow Foundation Migration Guidance

## Migration Decision Framework

### When to Keep WF

- Long-lived workflow instances with active persisted state
- Complex compensation and rollback logic that is well-tested
- Designer artifacts that encode irreplaceable business rules
- Compliance or audit requirements tied to existing WF persistence
- No clear business driver to modernize

### When to Replace WF

- .NET Core or .NET 5+ migration is required
- Workflow logic is simple and can be expressed as state machines or sagas
- Active development is needed but WF expertise is unavailable
- Hosting infrastructure is being decommissioned
- Persisted state can be drained or migrated

### When to Isolate WF

- Core application moves to modern .NET but workflow behavior must survive
- WF host can run as a standalone service behind an API boundary
- Migration is planned but not immediate
- Risk of touching designer artifacts is too high

## Migration Targets

### Durable Task Framework

- Best fit for long-running orchestrations in Azure
- Supports fan-out, fan-in, and human interaction patterns
- State persistence via Azure Storage or SQL
- Works with Azure Functions or standalone hosts

### Elsa Workflows

- Open-source workflow engine for .NET
- Designer support via web-based workflow builder
- Supports persistence, versioning, and long-running workflows
- Good fit when visual design continuity matters

### MassTransit Sagas

- Best fit for event-driven process orchestration
- State machine semantics with explicit states and transitions
- Persistence via Entity Framework or other stores
- Good fit when messaging is already part of the architecture

### Custom State Machines

- Best fit for simple, well-understood process logic
- No external dependencies beyond your own code
- Suitable when WF was overkill for the original problem

## Migration Steps

### 1. Inventory Current State

- List all workflow types and their purpose
- Identify persisted instances and their expected lifespan
- Document integration points: triggers, external calls, compensation
- Map designer artifacts to business rules they encode

### 2. Define Equivalence Criteria

- What triggers must produce the same downstream effects?
- What compensation or rollback behavior must be preserved?
- What audit or compliance records must survive migration?
- What monitoring or alerting depends on current workflow state?

### 3. Drain or Migrate Persisted State

Options:
- Wait for all instances to complete naturally
- Export state and replay into new system
- Run dual systems with routing based on instance creation date
- Accept state loss with stakeholder approval

### 4. Build Replacement Logic

- Start with the simplest possible implementation
- Add compensation and rollback only where equivalence requires it
- Test with representative scenarios from production
- Preserve audit trail expectations

### 5. Validate and Cut Over

- Run parallel comparison if feasible
- Validate edge cases: failures, timeouts, retries
- Monitor for unexpected behavior post-cutover
- Keep WF host available for rollback window

## Common Pitfalls

### Half-Migrations

Running two orchestration systems without clear ownership leads to:
- Duplicated business rules
- Inconsistent state
- Unclear error handling responsibility
- Increased operational burden

### Designer Artifact Assumptions

Designer-generated code may contain:
- Implicit ordering assumptions
- Hidden state transitions
- Compensation logic that is not obvious from the visual representation
- Version-specific serialization behavior

### Persistence Format Changes

WF persistence stores:
- Serialized workflow instance state
- Bookmark and continuation data
- Custom tracking records

Migration must account for:
- Deserialization compatibility
- Bookmark resolution in the new system
- Tracking data continuity

### Underestimating Compensation

WF compensation scopes may encode:
- Multi-step rollback sequences
- External system reversals
- Audit record generation
- Notification triggers

Replacement systems must explicitly handle these cases.

## References

- [Windows Workflow Foundation Docs](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/)
- [Durable Task Framework](https://github.com/Azure/durabletask)
- [Elsa Workflows](https://elsa-workflows.github.io/elsa-core/)
- [MassTransit Sagas](https://masstransit.io/documentation/patterns/saga)
