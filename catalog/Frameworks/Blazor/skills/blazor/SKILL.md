---
name: blazor
description: "Build and review Blazor applications across server, WebAssembly, web app, and hybrid scenarios with correct component design, state flow, rendering, and hosting choices."
compatibility: "Requires Blazor project (.NET 6+, preferably .NET 8+ for unified model)."
---

# Blazor

## Trigger On

- building interactive web UIs with C# instead of JavaScript
- choosing between Server, WebAssembly, or Auto render modes
- designing component hierarchies and state management
- handling prerendering and hydration
- integrating with JavaScript when necessary

## Documentation

- [Blazor Overview](https://learn.microsoft.com/en-us/aspnet/core/blazor/?view=aspnetcore-10.0)
- [Render Modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)
- [Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance?view=aspnetcore-10.0)
- [State Management](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management?view=aspnetcore-10.0)
- [JS Interop](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0)

### References

- [patterns.md](references/patterns.md) - Detailed component patterns, state management strategies, and JS interop techniques
- [anti-patterns.md](references/anti-patterns.md) - Common Blazor mistakes and how to avoid them

## Render Modes (.NET 8+)

| Mode | Where It Runs | Best For |
|------|---------------|----------|
| `Static` | Server (no interactivity) | SEO pages, marketing content |
| `InteractiveServer` | Server via SignalR | Real-time apps, thin clients |
| `InteractiveWebAssembly` | Browser via WASM | Offline-capable, client-heavy |
| `InteractiveAuto` | Server first, then WASM | Best of both worlds |

### Applying Render Modes

```razor
@* Per-component *@
@rendermode InteractiveServer

@* Or in App.razor for global *@
<Routes @rendermode="InteractiveAuto" />
```

### InteractiveAuto Architecture

```
First Request:
  Browser → Server (Interactive Server) → Fast response

Subsequent Requests:
  Browser → WASM (downloaded in background) → No server needed
```

## Workflow

1. **Choose render mode based on requirements:**
   - Need SEO? Start with Static or prerendering
   - Need real-time? Use InteractiveServer
   - Need offline? Use InteractiveWebAssembly
   - Want both? Use InteractiveAuto

2. **Design components for reusability:**
   - Small, focused components
   - Parameters for customization
   - Events for communication

3. **Handle state correctly:**
   - Component state lives in component
   - Shared state via services (DI)
   - Persist state across prerender with `[PersistentState]`

4. **Validate in both environments** (for Auto mode)

## Component Patterns

### Basic Component
```razor
@* Counter.razor *@
<button @onclick="IncrementCount">
    Clicked @count times
</button>

@code {
    private int count = 0;

    [Parameter]
    public int InitialCount { get; set; } = 0;

    protected override void OnInitialized()
    {
        count = InitialCount;
    }

    private void IncrementCount() => count++;
}
```

### Parameter and Event Callbacks
```razor
@* Parent.razor *@
<ChildComponent Value="@value" ValueChanged="@OnValueChanged" />

@* ChildComponent.razor *@
@code {
    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    private async Task UpdateValue(string newValue)
    {
        await ValueChanged.InvokeAsync(newValue);
    }
}
```

### State Persistence (.NET 8+)
```razor
@* Prevents double-fetch during prerender + hydration *@
@code {
    [PersistentState]
    public List<Product> Products { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        // Only fetches once, persisted across prerender
        Products ??= await Http.GetFromJsonAsync<List<Product>>("api/products");
    }
}
```

## Data Access Pattern for Auto Mode

```csharp
// Shared interface
public interface IProductService
{
    Task<List<Product>> GetProductsAsync();
}

// Server implementation (direct DB access)
public class ServerProductService : IProductService
{
    private readonly AppDbContext _db;
    public async Task<List<Product>> GetProductsAsync()
        => await _db.Products.ToListAsync();
}

// Client implementation (HTTP call)
public class ClientProductService : IProductService
{
    private readonly HttpClient _http;
    public async Task<List<Product>> GetProductsAsync()
        => await _http.GetFromJsonAsync<List<Product>>("api/products");
}

// Registration
// Server: builder.Services.AddScoped<IProductService, ServerProductService>();
// Client: builder.Services.AddScoped<IProductService, ClientProductService>();
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Large components | Hard to maintain, slow renders | Split into smaller components |
| Direct DB access in WASM | No DB in browser | Use HTTP API |
| Ignoring `ShouldRender` | Unnecessary re-renders | Override when needed |
| Sync JS interop in Server | Blocks SignalR circuit | Use `IJSRuntime` async |
| No error boundaries | One error crashes app | Use `<ErrorBoundary>` |
| Forgetting prerender state | Double API calls | Use `[PersistentState]` |

## Performance Best Practices

1. **Virtualize large lists:**
   ```razor
   <Virtualize Items="@products" Context="product">
       <ProductCard Product="@product" />
   </Virtualize>
   ```

2. **Use `@key` for list diffing:**
   ```razor
   @foreach (var item in items)
   {
       <ItemComponent @key="item.Id" Item="@item" />
   }
   ```

3. **Debounce rapid events:**
   ```csharp
   private Timer? _debounceTimer;

   private void OnInput(ChangeEventArgs e)
   {
       _debounceTimer?.Dispose();
       _debounceTimer = new Timer(_ => InvokeAsync(DoSearch), null, 300, Timeout.Infinite);
   }
   ```

4. **Lazy load assemblies (WASM):**
   ```csharp
   var assemblies = await LazyAssemblyLoader
       .LoadAssembliesAsync(["MyHeavyFeature.wasm"]);
   ```

## JS Interop

### Calling JavaScript from C#
```csharp
@inject IJSRuntime JS

await JS.InvokeVoidAsync("alert", "Hello from Blazor!");
var result = await JS.InvokeAsync<string>("prompt", "Enter name:");
```

### Calling C# from JavaScript
```csharp
[JSInvokable]
public static string GetMessage() => "Hello from C#!";
```

```javascript
DotNet.invokeMethodAsync('MyAssembly', 'GetMessage')
    .then(result => console.log(result));
```

## Deliver

- interactive Blazor components with appropriate render mode
- efficient state management and data flow
- proper handling of prerendering scenarios
- performant list rendering with virtualization

## Validate

- components render correctly in chosen mode
- state persists correctly across prerender/hydration
- no unnecessary re-renders (check with browser tools)
- JS interop works in both Server and WASM
- error boundaries catch component failures
- Auto mode works in both environments
