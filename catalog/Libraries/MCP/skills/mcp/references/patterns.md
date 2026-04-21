# MCP C# SDK Patterns

Use this file when the task needs concrete current patterns from the official MCP C# SDK rather than high-level routing guidance.

## Package and Transport Matrix

| Scenario | Package | Transport / API |
|----------|---------|-----------------|
| Minimal client or low-level host | `ModelContextProtocol.Core` | `McpClient`, low-level server APIs |
| Typical client or stdio server | `ModelContextProtocol` | `StdioClientTransport`, `WithStdioServerTransport()` |
| ASP.NET Core server | `ModelContextProtocol.AspNetCore` | `WithHttpTransport()`, `MapMcp()` |
| Remote client over HTTP | `ModelContextProtocol` or `Core` | `HttpClientTransport` |

## Minimal stdio server

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class EchoTools
{
    [McpServerTool, Description("Echoes the message back to the caller.")]
    public static string Echo([Description("Message to echo")] string message)
        => $"hello {message}";
}
```

Use `WithTools<T>()`, `WithResources<T>()`, and `WithPrompts<T>()` when you want explicit registration instead of assembly scanning.

## Minimal ASP.NET Core server

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<WeatherTools>()
    .WithResources<WeatherResources>()
    .WithPrompts<WeatherPrompts>();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

[McpServerToolType]
public static class WeatherTools
{
    [McpServerTool, Description("Returns the current weather for a city.")]
    public static string GetCurrentWeather(
        [Description("City name")] string city)
        => $"Current weather for {city}: sunny";
}
```

Notes:

- `MapMcp()` serves Streamable HTTP and legacy SSE endpoints.
- New remote clients should connect to the mapped route directly and prefer Streamable HTTP.
- Only point SSE clients to `{route}/sse`.

## Stdio client pattern

```csharp
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "Everything",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-everything"],
});

await using var client = await McpClient.CreateAsync(transport);

IList<McpClientTool> tools = await client.ListToolsAsync();

CallToolResult result = await client.CallToolAsync(
    "echo",
    new Dictionary<string, object?> { ["message"] = "Hello MCP!" });

Console.WriteLine(result.Content.OfType<TextContentBlock>().First().Text);
```

## HTTP client pattern

```csharp
using ModelContextProtocol.Client;

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://example.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    AdditionalHeaders = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer <token>"
    }
});

await using var client = await McpClient.CreateAsync(transport);
```

For mixed environments, `HttpTransportMode.AutoDetect` is the default. It tries Streamable HTTP first and falls back to SSE when needed.

## Session resumption

Use this only for Streamable HTTP sessions:

```csharp
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://example.com/mcp"),
    KnownSessionId = previousSessionId
});

await using var client = await McpClient.ResumeSessionAsync(
    transport,
    new ResumeClientSessionOptions
    {
        ServerCapabilities = previousServerCapabilities,
        ServerInfo = previousServerInfo
    });
```

## Tool pattern

```csharp
[McpServerToolType]
public sealed class BuildTools(IBuildService builds)
{
    [McpServerTool, Description("Queues a build for the requested branch.")]
    public async Task<string> QueueBuildAsync(
        [Description("Git branch to build")] string branch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new McpProtocolException("Branch is required.", McpErrorCode.InvalidParams);
        }

        var buildId = await builds.QueueAsync(branch, cancellationToken);
        return $"queued:{buildId}";
    }
}
```

Guidance:

- `string` results are wrapped as `TextContentBlock`.
- Use `ImageContentBlock`, `AudioContentBlock`, or `EmbeddedResourceBlock` when the tool returns richer content.
- Use `[Description]` on the method and parameters so hosts can build better schemas.
- Methods can accept `McpServer`, `ClaimsPrincipal`, `IProgress<ProgressNotificationValue>`, and DI-registered services in addition to normal arguments.

## Resource pattern

```csharp
[McpServerResourceType]
public static class RepoResources
{
    [McpServerResource(
        UriTemplate = "repo://readme",
        Name = "Repository README",
        MimeType = "text/markdown")]
    [Description("Returns the repository overview document.")]
    public static string ReadReadme()
        => File.ReadAllText("README.md");

    [McpServerResource(UriTemplate = "repo://files/{path}", Name = "Repository File")]
    [Description("Returns a file under the approved repository root.")]
    public static TextResourceContents ReadFile(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        var root = Path.GetFullPath(Environment.CurrentDirectory);

        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            throw new McpException("Requested file is outside the repository root.");
        }

        return new TextResourceContents
        {
            Uri = $"repo://files/{path}",
            MimeType = "text/plain",
            Text = File.ReadAllText(fullPath)
        };
    }
}
```

Use resource templates when the URI contains parameters. Clients can enumerate them with `ListResourceTemplatesAsync()` and materialize them with `ReadResourceAsync(...)`.

## Prompt pattern

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

[McpServerPromptType]
public static class ReviewPrompts
{
    [McpServerPrompt, Description("Builds a code-review prompt.")]
    public static IEnumerable<ChatMessage> CodeReview(
        [Description("Programming language")] string language,
        [Description("Code to review")] string code) =>
        [
            new(ChatRole.User, $"Review this {language} code:\n\n```{language}\n{code}\n```")
        ];

    [McpServerPrompt, Description("Builds a document-review prompt with an embedded resource.")]
    public static IEnumerable<PromptMessage> ReviewDocument(
        [Description("Document identifier")] string id)
        =>
        [
            new()
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = "Review the attached document."
                }
            },
            new()
            {
                Role = Role.User,
                Content = new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = $"docs://documents/{id}",
                        MimeType = "text/plain",
                        Text = LoadDocument(id)
                    }
                }
            }
        ];
}
```

Use `ChatMessage` for normal text/image flows and `PromptMessage` when you need protocol-specific content such as embedded resources.

## Capability-aware client pattern

```csharp
var options = new McpClientOptions
{
    Capabilities = new ClientCapabilities
    {
        Roots = new RootsCapability { ListChanged = true },
        Sampling = new SamplingCapability(),
        Elicitation = new ElicitationCapability
        {
            Form = new FormElicitationCapability(),
            Url = new UrlElicitationCapability()
        }
    }
};

await using var client = await McpClient.CreateAsync(transport, options);

if (client.ServerCapabilities.Resources is { Subscribe: true })
{
    await client.SubscribeToResourceAsync("repo://readme");
}

if (client.ServerCapabilities.Logging is not null)
{
    await client.SetLoggingLevelAsync(LoggingLevel.Info);
}
```

Check `client.ServerCapabilities` before using:

- resource subscriptions
- prompt/resource list-change notifications
- completions
- logging
- any feature that is optional in the spec

## Passing MCP tools into a chat client

`McpClientTool` inherits from `AIFunction`, so discovered tools can be passed directly into `IChatClient`:

```csharp
IList<McpClientTool> tools = await client.ListToolsAsync();

IChatClient chatClient = ...;
var response = await chatClient.GetResponseAsync(
    "Use the MCP tools to answer the question.",
    new() { Tools = [.. tools] });
```

## Filters for cross-cutting behavior

Use filters for audit, custom JSON-RPC routing, or policy, not for normal domain logic:

```csharp
builder.Services
    .AddMcpServer()
    .WithMessageFilters(messageFilters =>
    {
        messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
        {
            if (context.JsonRpcMessage is JsonRpcRequest request)
            {
                Console.Error.WriteLine($"Incoming MCP method: {request.Method}");
            }

            await next(context, cancellationToken);
        });
    })
    .WithRequestFilters(requestFilters =>
    {
        requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
        {
            Console.Error.WriteLine($"Executing tool: {context.Params?.Name}");
            return await next(context, cancellationToken);
        });
    })
    .WithTools<WeatherTools>();
```

## Experimental APIs and serialization

When using experimental MCP APIs:

- suppress only the relevant `MCPEXP...` diagnostic ids
- avoid blanket `NoWarn` entries for unrelated code
- if you supply a custom `JsonSerializerContext`, prepend `McpJsonUtilities.DefaultOptions.TypeInfoResolver` so MCP protocol types continue to serialize with the SDK's contract

## Validation Checklist

- client and server transport choices match the deployment topology
- stdio servers keep stdout protocol-clean
- HTTP endpoints are tested at the real final route
- tool/resource/prompt descriptions are explicit
- optional features are guarded by capability checks
- filter usage is cross-cutting rather than replacing normal handlers

static string LoadDocument(string id) => $"Document {id}";
