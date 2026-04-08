# MCP C# SDK Security Notes

Use this file when the task involves safe server/client design, auth boundaries, or data exposure rules for MCP.

## Security Priorities

1. Keep the MCP transport clean and predictable.
2. Limit what tools and resources can reach.
3. Return safe protocol errors instead of leaking internals.
4. Authenticate and authorize at the transport boundary.
5. Make optional capabilities explicit.

## stdio hygiene

For stdio servers, anything written to stdout can corrupt the protocol stream. Route logs to stderr:

```csharp
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

Do not:

- write banner text to stdout
- print debug tracing with `Console.WriteLine`
- mix app startup messaging into the MCP pipe

## Parameter validation

Treat every tool/resource/prompt argument as untrusted input.

```csharp
[McpServerToolType]
public sealed class FileTools(IFileSystem files)
{
    [McpServerTool, Description("Reads a text file below the approved workspace root.")]
    public async Task<string> ReadTextFileAsync(
        [Description("Relative path below the workspace root")] string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new McpProtocolException("Path is required.", McpErrorCode.InvalidParams);
        }

        var root = Path.GetFullPath("/path/to/approved/workspace");
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            throw new McpException("Requested path is outside the allowed workspace.");
        }

        return await files.File.ReadAllTextAsync(fullPath, cancellationToken);
    }
}
```

Patterns:

- normalize paths before checking the root
- whitelist supported operations and file types
- clamp numeric limits and pagination inputs
- reject blank or ambiguous identifiers early

## Error boundaries

Use the right exception type for the right kind of failure:

- `McpProtocolException` for JSON-RPC or contract-level failures such as invalid parameters
- `McpException` for domain errors whose message is safe to surface
- ordinary exceptions only for unexpected faults; they will be converted into generic tool errors

This distinction matters because tool failures are exposed differently from protocol failures.

## Authorization

For HTTP servers, prefer normal ASP.NET Core auth middleware and endpoint policy around `MapMcp()`:

- bearer tokens, cookies, or mutual TLS belong at the HTTP boundary
- rate limiting belongs in ASP.NET Core middleware or infrastructure, not ad hoc inside every tool
- map unauthenticated requests to standard HTTP auth behavior before MCP handlers run

Inside MCP handlers, use injected principals for per-operation authorization:

```csharp
[McpServerToolType]
public sealed class DeploymentTools(IDeploymentService deployments)
{
    [McpServerTool, Description("Cancels a deployment owned by the current user.")]
    public async Task<string> CancelDeploymentAsync(
        [Description("Deployment identifier")] string deploymentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            throw new McpException("Authentication is required.");
        }

        await deployments.CancelAsync(deploymentId, user, cancellationToken);
        return $"Cancelled deployment {deploymentId}.";
    }
}
```

## Capability minimization

Only enable features you are prepared to support safely:

- roots: only if the client should disclose filesystem roots
- sampling: only if the server should request LLM completions from the client
- elicitation: only if the server is allowed to prompt the user for more input
- resource subscriptions: only if you can track and notify subscribers correctly

Do not assume a host/client supports these features. Capability negotiation is part of the security boundary.

## Tool and resource output discipline

Keep payloads small and deliberate.

Prefer:

- summaries plus identifiers
- paginated lists
- direct resources for large text or binary data
- explicit MIME types

Avoid:

- dumping whole databases or repositories into one tool result
- returning secrets or internal stack traces
- embedding large binary payloads when a resource URI is enough

## Remote transport guidance

For remote servers:

- prefer Streamable HTTP
- use HTTPS
- pass auth via standard HTTP headers or ASP.NET Core auth
- use SSE only for legacy compatibility

For local-only integrations:

- prefer stdio
- keep environment variables explicit
- avoid inheriting more process privileges than the child server needs

## Filters as policy points

Filters are appropriate for audit, tracing, and global policy checks:

```csharp
builder.Services
    .AddMcpServer()
    .WithRequestFilters(filters =>
    {
        filters.AddCallToolFilter(next => async (context, cancellationToken) =>
        {
            var toolName = context.Params?.Name;
            if (toolName is "delete_all_data")
            {
                throw new McpException("This tool is disabled in the current environment.");
            }

            return await next(context, cancellationToken);
        });
    });
```

Do not hide primary business rules in filters if the tool handler itself can express them clearly.

## Experimental APIs

Experimental MCP APIs can change outside normal patch-level expectations. Before adopting them:

- suppress only the specific `MCPEXP...` diagnostic you intend to accept
- document why the suppression exists
- isolate experimental usage behind an internal abstraction if the project needs a stable surface

If you use source-generated JSON serialization, prepend `McpJsonUtilities.DefaultOptions.TypeInfoResolver` so MCP protocol types keep the SDK's serialization contract, including experimental fields when required by the wire protocol.

## Review Checklist

- stdout remains protocol-clean for stdio servers
- every externally supplied argument is validated and normalized
- auth happens at the HTTP boundary and is rechecked inside sensitive handlers
- sensitive operations are scoped to the caller's identity or allowed root
- optional capabilities are enabled intentionally rather than by accident
- tool/resource outputs exclude secrets, internal stack traces, and oversized payloads
