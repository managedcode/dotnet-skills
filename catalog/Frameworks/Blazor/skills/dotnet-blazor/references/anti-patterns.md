# Blazor Anti-Patterns

## Component Design Anti-Patterns

### Monolithic Components

**Problem:** Creating large components that handle multiple concerns.

```razor
@* BAD: One component doing everything *@
@code {
    private List<Product> products = [];
    private List<Category> categories = [];
    private Cart cart = new();
    private User? user;
    private bool showFilters = false;
    private string searchTerm = "";
    private decimal minPrice;
    private decimal maxPrice;
    // ... 500 more lines of mixed concerns
}
```

**Solution:** Split into focused components with single responsibilities.

```razor
@* GOOD: Composed from smaller components *@
<ProductPage>
    <ProductFilters />
    <ProductGrid Products="@products" />
    <CartSidebar />
</ProductPage>
```

### Parameter Drilling

**Problem:** Passing parameters through many component layers.

```razor
@* BAD: Drilling user through multiple levels *@
<Layout User="@user">
    <Sidebar User="@user">
        <UserMenu User="@user">
            <Avatar User="@user" />
        </UserMenu>
    </Sidebar>
</Layout>
```

**Solution:** Use cascading values for widely-needed data.

```razor
@* GOOD: Cascading value *@
<CascadingValue Value="@user">
    <Layout>
        <Sidebar>
            <UserMenu />  @* Accesses user via [CascadingParameter] *@
        </Sidebar>
    </Layout>
</CascadingValue>
```

### Mutable Parameter Objects

**Problem:** Modifying parameter objects directly, bypassing change detection.

```razor
@* BAD: Mutating parameter object *@
@code {
    [Parameter] public Product Product { get; set; } = default!;

    private void UpdatePrice()
    {
        Product.Price = 99.99m; // Parent won't know about this change
    }
}
```

**Solution:** Use events to notify parent of changes.

```razor
@* GOOD: Notify parent via callback *@
@code {
    [Parameter] public Product Product { get; set; } = default!;
    [Parameter] public EventCallback<Product> ProductChanged { get; set; }

    private async Task UpdatePrice()
    {
        var updated = Product with { Price = 99.99m };
        await ProductChanged.InvokeAsync(updated);
    }
}
```

### Missing EditorRequired

**Problem:** Forgetting to mark required parameters, leading to runtime errors.

```razor
@* BAD: No indication this is required *@
@code {
    [Parameter] public Product Product { get; set; } = default!;
}
```

**Solution:** Use EditorRequired for mandatory parameters.

```razor
@* GOOD: Compiler warns if not provided *@
@code {
    [Parameter, EditorRequired] public Product Product { get; set; } = default!;
}
```

## State Management Anti-Patterns

### Global Static State

**Problem:** Using static fields for state, causing cross-user data leakage in Server mode.

```csharp
// BAD: Static state is shared across ALL users in Server mode
public static class AppState
{
    public static User? CurrentUser { get; set; }
    public static List<CartItem> Cart { get; } = [];
}
```

**Solution:** Use scoped services.

```csharp
// GOOD: Scoped per-circuit in Server mode
public class AppState
{
    public User? CurrentUser { get; set; }
    public List<CartItem> Cart { get; } = [];
}

// Registration
services.AddScoped<AppState>();
```

### Forgetting to Dispose Event Subscriptions

**Problem:** Memory leaks from event subscriptions.

```razor
@* BAD: Never unsubscribes *@
@code {
    protected override void OnInitialized()
    {
        CartService.OnChange += StateHasChanged;
    }
}
```

**Solution:** Implement IDisposable.

```razor
@* GOOD: Clean up subscriptions *@
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        CartService.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        CartService.OnChange -= StateHasChanged;
    }
}
```

### Double Data Fetching with Prerendering

**Problem:** Fetching data twice (once during prerender, once during interactive).

```razor
@* BAD: Fetches twice *@
@code {
    private List<Product> products = [];

    protected override async Task OnInitializedAsync()
    {
        products = await Http.GetFromJsonAsync<List<Product>>("api/products") ?? [];
    }
}
```

**Solution:** Use PersistentState or PersistentComponentState.

```razor
@* GOOD: Data persists across prerender *@
@code {
    [PersistentState]
    public List<Product> Products { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        if (Products.Count == 0)
        {
            Products = await Http.GetFromJsonAsync<List<Product>>("api/products") ?? [];
        }
    }
}
```

## Render Mode Anti-Patterns

### Direct Database Access in WASM Components

**Problem:** Trying to use DbContext in WebAssembly.

```csharp
// BAD: DbContext doesn't work in browser
@inject AppDbContext Db

@code {
    protected override async Task OnInitializedAsync()
    {
        products = await Db.Products.ToListAsync(); // Will fail in WASM
    }
}
```

**Solution:** Use HTTP API abstraction.

```csharp
// GOOD: Works in both Server and WASM
@inject IProductService ProductService

@code {
    protected override async Task OnInitializedAsync()
    {
        products = await ProductService.GetProductsAsync();
    }
}

// Server implementation uses DbContext
// Client implementation uses HttpClient
```

### Ignoring Render Mode Boundaries

**Problem:** Assuming all components run in the same mode.

```razor
@* BAD: Child assumes parent's render mode *@
<InteractiveParent>
    <StaticChild />  @* May not behave as expected *@
</InteractiveParent>
```

**Solution:** Explicitly set render modes and understand boundaries.

```razor
@* GOOD: Explicit render mode *@
<div>
    <StaticHeader />
    <InteractiveContent @rendermode="InteractiveServer" />
    <StaticFooter />
</div>
```

### Auto Mode Without Dual Implementation

**Problem:** Using Auto render mode without supporting both environments.

```csharp
// BAD: Only works on server
services.AddScoped<IDataService, ServerOnlyDataService>();
```

**Solution:** Register environment-specific implementations.

```csharp
// Server project
services.AddScoped<IDataService, ServerDataService>();

// Client project
services.AddScoped<IDataService, ClientDataService>();
```

## Performance Anti-Patterns

### Missing @key on Lists

**Problem:** Blazor recreates all list items on changes.

```razor
@* BAD: No key, poor diffing *@
@foreach (var item in items)
{
    <ItemComponent Item="@item" />
}
```

**Solution:** Use @key for efficient updates.

```razor
@* GOOD: Efficient list diffing *@
@foreach (var item in items)
{
    <ItemComponent @key="item.Id" Item="@item" />
}
```

### Rendering Large Lists Without Virtualization

**Problem:** Rendering thousands of items at once.

```razor
@* BAD: Renders all 10,000 items *@
@foreach (var item in allItems)
{
    <ItemRow Item="@item" />
}
```

**Solution:** Use Virtualize component.

```razor
@* GOOD: Only renders visible items *@
<Virtualize Items="@allItems" Context="item">
    <ItemRow Item="@item" />
</Virtualize>
```

### Unnecessary Re-renders

**Problem:** Components re-render when they don't need to.

```razor
@* BAD: Re-renders on every parent change *@
<ExpensiveComponent Data="@unchangingData" />
```

**Solution:** Override ShouldRender for expensive components.

```razor
@* GOOD: Controlled re-rendering *@
@code {
    private object? previousData;

    [Parameter] public object? Data { get; set; }

    protected override bool ShouldRender()
    {
        var shouldRender = !ReferenceEquals(Data, previousData);
        previousData = Data;
        return shouldRender;
    }
}
```

### Blocking Async Operations

**Problem:** Using synchronous waits that block the render thread.

```csharp
// BAD: Blocks the thread
protected override void OnInitialized()
{
    var data = Http.GetFromJsonAsync<Data>("api/data").Result; // BLOCKS!
}
```

**Solution:** Use proper async patterns.

```csharp
// GOOD: Non-blocking
protected override async Task OnInitializedAsync()
{
    var data = await Http.GetFromJsonAsync<Data>("api/data");
}
```

## JavaScript Interop Anti-Patterns

### Synchronous JS Calls in Server Mode

**Problem:** Synchronous JS interop blocks the SignalR circuit.

```csharp
// BAD: Blocks in Server mode
var result = ((IJSInProcessRuntime)JS).Invoke<string>("getValue");
```

**Solution:** Always use async JS interop.

```csharp
// GOOD: Non-blocking
var result = await JS.InvokeAsync<string>("getValue");
```

### JS Interop During Prerendering

**Problem:** Calling JS during prerender when there's no browser.

```csharp
// BAD: Fails during prerender
protected override async Task OnInitializedAsync()
{
    await JS.InvokeVoidAsync("initializeMap"); // No JS runtime during prerender
}
```

**Solution:** Call JS only after first interactive render.

```csharp
// GOOD: Only when interactive
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await JS.InvokeVoidAsync("initializeMap");
    }
}
```

### Not Disposing JS Object References

**Problem:** Memory leaks from JS object references.

```csharp
// BAD: Never disposed
private IJSObjectReference? module;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        module = await JS.InvokeAsync<IJSObjectReference>("import", "./module.js");
    }
}
```

**Solution:** Implement IAsyncDisposable.

```csharp
// GOOD: Proper cleanup
@implements IAsyncDisposable

private IJSObjectReference? module;

public async ValueTask DisposeAsync()
{
    if (module is not null)
    {
        await module.DisposeAsync();
    }
}
```

### Large Data in JS Interop

**Problem:** Passing large objects through JS interop serialization.

```csharp
// BAD: Serializes entire dataset
await JS.InvokeVoidAsync("processData", hugeDataSet);
```

**Solution:** Use streaming or pass references.

```csharp
// GOOD: Stream large data
using var streamRef = new DotNetStreamReference(dataStream);
await JS.InvokeVoidAsync("processStream", streamRef);
```

## Form Handling Anti-Patterns

### Missing Validation

**Problem:** Forms without proper validation.

```razor
@* BAD: No validation *@
<EditForm Model="@model" OnSubmit="Submit">
    <InputText @bind-Value="model.Email" />
    <button type="submit">Submit</button>
</EditForm>
```

**Solution:** Add validators.

```razor
@* GOOD: With validation *@
<EditForm Model="@model" OnValidSubmit="Submit">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <InputText @bind-Value="model.Email" />
    <ValidationMessage For="() => model.Email" />

    <button type="submit">Submit</button>
</EditForm>
```

### Not Handling Form Submission State

**Problem:** Double submissions and no loading indication.

```razor
@* BAD: Can submit multiple times *@
<button type="submit">Submit</button>
```

**Solution:** Track and display submission state.

```razor
@* GOOD: Prevents double submit, shows status *@
<button type="submit" disabled="@isSubmitting">
    @(isSubmitting ? "Submitting..." : "Submit")
</button>

@code {
    private bool isSubmitting = false;

    private async Task Submit()
    {
        isSubmitting = true;
        try
        {
            await SubmitFormAsync();
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
```

## Error Handling Anti-Patterns

### No Error Boundaries

**Problem:** One component error crashes the whole application.

```razor
@* BAD: Unhandled exception crashes circuit *@
<RiskyComponent />
```

**Solution:** Wrap risky components in ErrorBoundary.

```razor
@* GOOD: Contained errors *@
<ErrorBoundary>
    <ChildContent>
        <RiskyComponent />
    </ChildContent>
    <ErrorContent Context="ex">
        <p>Something went wrong: @ex.Message</p>
    </ErrorContent>
</ErrorBoundary>
```

### Swallowing Exceptions

**Problem:** Catching exceptions without proper handling.

```csharp
// BAD: Silent failure
try
{
    await SaveDataAsync();
}
catch
{
    // Swallowed - user has no idea it failed
}
```

**Solution:** Provide feedback and logging.

```csharp
// GOOD: User feedback and logging
try
{
    await SaveDataAsync();
    message = "Saved successfully";
}
catch (Exception ex)
{
    Logger.LogError(ex, "Failed to save data");
    errorMessage = "Failed to save. Please try again.";
}
```

## Security Anti-Patterns

### Client-Side Authorization Only

**Problem:** Relying solely on client-side security checks.

```razor
@* BAD: Client-side only - easily bypassed *@
@if (isAdmin)
{
    <AdminPanel />
}
```

**Solution:** Always validate on server.

```csharp
// GOOD: Server-side authorization
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    // Server validates every request
}
```

### Exposing Sensitive Data in Component State

**Problem:** Keeping secrets in component state visible to users.

```razor
@* BAD: API key visible in browser state *@
@code {
    private string apiKey = "secret-api-key-12345";
}
```

**Solution:** Keep secrets server-side only.

```csharp
// GOOD: Server-side service holds secrets
public class SecureService
{
    private readonly string _apiKey;

    public SecureService(IConfiguration config)
    {
        _apiKey = config["ApiKey"]!;
    }

    public async Task CallApiAsync()
    {
        // Uses _apiKey internally, never exposed to client
    }
}
```

### Trusting Client Input

**Problem:** Using client input without validation.

```csharp
// BAD: Direct use of user input
var userId = userIdFromClient;
var data = await Db.GetUserData(userId); // Can access any user's data
```

**Solution:** Validate against authenticated user.

```csharp
// GOOD: Validate ownership
var authenticatedUserId = GetAuthenticatedUserId();
if (requestedUserId != authenticatedUserId)
{
    throw new UnauthorizedAccessException();
}
```
