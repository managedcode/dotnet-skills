# Windows Workflow Foundation Maintenance Patterns

## Host Management Patterns

### Isolated Workflow Host

Keep WF hosting separate from application logic:

```
┌─────────────────────────────────────────┐
│           Application Layer             │
├─────────────────────────────────────────┤
│        Workflow Service API             │
├─────────────────────────────────────────┤
│         WF Host Process                 │
│  ┌─────────┐ ┌─────────┐ ┌──────────┐  │
│  │ Runtime │ │ Persist │ │ Tracking │  │
│  └─────────┘ └─────────┘ └──────────┘  │
└─────────────────────────────────────────┘
```

Benefits:
- Clear deployment boundary
- Independent scaling and lifecycle
- Easier to replace or isolate later

### Workflow Service Facade

Wrap WF operations behind a service interface:

```csharp
public interface IWorkflowService
{
    Task<Guid> StartWorkflowAsync(string workflowType, object input);
    Task<WorkflowStatus> GetStatusAsync(Guid instanceId);
    Task ResumeBookmarkAsync(Guid instanceId, string bookmarkName, object value);
    Task CancelAsync(Guid instanceId);
}
```

Benefits:
- Decouples callers from WF internals
- Enables future replacement without client changes
- Simplifies testing and mocking

## Persistence Patterns

### Controlled Persistence Points

Avoid implicit persistence; make persistence decisions explicit:

- Use NoPersistScope for transient operations
- Document persistence assumptions in activity code
- Test recovery from each explicit persistence point

### Persistence Health Monitoring

Track persistence store health:

```csharp
public class PersistenceHealthCheck
{
    public async Task<bool> CanPersistAsync()
    {
        // Attempt lightweight write/read cycle
        // Monitor persistence latency
        // Alert on degradation
    }
}
```

### Instance Lifecycle Management

Define clear policies for:
- Maximum instance age before forced completion or termination
- Orphaned instance detection and cleanup
- Persisted state backup and retention

## Activity Patterns

### Idempotent Activities

Design activities to be safely re-executed:

```csharp
public sealed class IdempotentActivity : CodeActivity
{
    protected override void Execute(CodeActivityContext context)
    {
        var operationId = context.WorkflowInstanceId.ToString() + "_" + context.ActivityInstanceId;

        if (AlreadyExecuted(operationId))
        {
            // Return cached result
            return;
        }

        // Execute and record result
    }
}
```

### Compensation-Aware Activities

Structure activities to support rollback:

```csharp
public sealed class BookableResourceActivity : NativeActivity
{
    protected override void Execute(NativeActivityContext context)
    {
        // Book resource
        var booking = BookResource();

        // Store booking for potential compensation
        context.Properties.Add("Booking", booking);
    }

    protected override void Cancel(NativeActivityContext context)
    {
        // Release resource on cancellation
        var booking = context.Properties.Find("Booking") as Booking;
        ReleaseResource(booking);
    }
}
```

### Timeout and Retry Handling

Wrap external calls with explicit timeout and retry logic:

```csharp
public sealed class ResilientServiceCall : NativeActivity
{
    public InArgument<TimeSpan> Timeout { get; set; }
    public InArgument<int> MaxRetries { get; set; }

    protected override void Execute(NativeActivityContext context)
    {
        var timeout = Timeout.Get(context);
        var maxRetries = MaxRetries.Get(context);

        // Implement retry with exponential backoff
        // Respect timeout boundaries
        // Log each attempt for debugging
    }
}
```

## Designer Artifact Patterns

### Minimal Designer Changes

When modifying designer-backed workflows:

1. Make the smallest possible change
2. Test the exact scenario being fixed
3. Verify no unintended side effects on other paths
4. Document the change with before/after screenshots if visual

### Version Isolation

Keep workflow versions isolated:

```
workflows/
├── OrderProcessing_v1.xaml  # Legacy, read-only
├── OrderProcessing_v2.xaml  # Current production
└── OrderProcessing_v3.xaml  # Development
```

Route new instances to the current version; let old instances complete on their original version.

### Designer to Code Migration

When designer complexity becomes unmanageable:

1. Extract the core logic into testable code activities
2. Simplify the designer workflow to orchestration only
3. Document the mapping between designer elements and code

## Testing Patterns

### Workflow Unit Testing

Test activities in isolation:

```csharp
[Test]
public void Activity_WithValidInput_ProducesExpectedOutput()
{
    var activity = new MyActivity();
    var inputs = new Dictionary<string, object>
    {
        { "Input", testValue }
    };

    var outputs = WorkflowInvoker.Invoke(activity, inputs);

    Assert.That(outputs["Result"], Is.EqualTo(expectedValue));
}
```

### Scenario Capture

Capture production scenarios for regression testing:

1. Log workflow inputs and decision points
2. Record external service responses
3. Replay scenarios in test environment
4. Compare outcomes to production baseline

### Long-Running Workflow Testing

Test persistence and recovery:

```csharp
[Test]
public async Task Workflow_AfterHostRestart_ResumesCorrectly()
{
    // Start workflow
    var instanceId = await StartWorkflowAsync();

    // Wait for persistence point
    await WaitForPersistenceAsync(instanceId);

    // Simulate host restart
    await RestartHostAsync();

    // Resume and verify completion
    await ResumeAndVerifyAsync(instanceId);
}
```

## Monitoring Patterns

### Instance Health Dashboard

Track key metrics:

- Active instance count by workflow type
- Instance age distribution
- Persistence queue depth
- Bookmark wait times
- Failure and cancellation rates

### Stuck Instance Detection

Identify workflows that are not progressing:

```csharp
public IEnumerable<StuckInstance> FindStuckInstances(TimeSpan threshold)
{
    // Query persistence store for instances
    // where last activity timestamp exceeds threshold
    // and instance is not in a known waiting state
}
```

### Audit Trail Continuity

Ensure tracking records support compliance:

- Capture all state transitions
- Record decision inputs and outputs
- Preserve timestamps with consistent timezone handling
- Support query by instance, time range, or outcome

## Operational Patterns

### Graceful Shutdown

Drain workflows before host shutdown:

1. Stop accepting new workflow starts
2. Wait for in-flight activities to complete
3. Persist current state for all active instances
4. Verify persistence success before shutdown

### Instance Recovery Procedures

Document recovery steps for common failures:

- Persistence store unavailable
- External service timeout
- Activity exception
- Host crash during execution

### Capacity Planning

Monitor and plan for:

- Persistence store growth rate
- Peak concurrent instance count
- Activity execution latency trends
- Tracking record volume
