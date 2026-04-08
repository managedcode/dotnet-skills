# Blazor Component Patterns

## Component Design Patterns

### Smart vs Presentational Components

Separate concerns by distinguishing between components that manage data and those that display it.

**Presentational Component (Dumb)**
```razor
@* ProductCard.razor - Only displays data *@
<div class="product-card">
    <img src="@Product.ImageUrl" alt="@Product.Name" />
    <h3>@Product.Name</h3>
    <p>@Product.Price.ToString("C")</p>
    <button @onclick="OnAddToCart">Add to Cart</button>
</div>

@code {
    [Parameter, EditorRequired] public Product Product { get; set; } = default!;
    [Parameter] public EventCallback OnAddToCart { get; set; }
}
```

**Smart Component (Container)**
```razor
@* ProductList.razor - Manages data and state *@
@inject IProductService ProductService
@inject ICartService CartService

<div class="product-list">
    @foreach (var product in products)
    {
        <ProductCard Product="@product" OnAddToCart="() => AddToCart(product)" />
    }
</div>

@code {
    private List<Product> products = [];

    protected override async Task OnInitializedAsync()
    {
        products = await ProductService.GetProductsAsync();
    }

    private async Task AddToCart(Product product)
    {
        await CartService.AddAsync(product);
    }
}
```

### Templated Components

Allow consumers to customize rendering with render fragments.

```razor
@* DataGrid.razor *@
@typeparam TItem

<table>
    <thead>
        <tr>@HeaderTemplate</tr>
    </thead>
    <tbody>
        @foreach (var item in Items)
        {
            <tr>@RowTemplate(item)</tr>
        }
    </tbody>
</table>

@code {
    [Parameter, EditorRequired] public IEnumerable<TItem> Items { get; set; } = [];
    [Parameter, EditorRequired] public RenderFragment HeaderTemplate { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment<TItem> RowTemplate { get; set; } = default!;
}
```

**Usage:**
```razor
<DataGrid Items="@products">
    <HeaderTemplate>
        <th>Name</th>
        <th>Price</th>
    </HeaderTemplate>
    <RowTemplate Context="product">
        <td>@product.Name</td>
        <td>@product.Price.ToString("C")</td>
    </RowTemplate>
</DataGrid>
```

### Generic Components

Create type-safe reusable components.

```razor
@* SelectList.razor *@
@typeparam TItem
@typeparam TValue

<select @onchange="OnSelectionChanged">
    @foreach (var item in Items)
    {
        <option value="@ValueSelector(item)" selected="@(EqualityComparer<TValue>.Default.Equals(ValueSelector(item), SelectedValue))">
            @DisplaySelector(item)
        </option>
    }
</select>

@code {
    [Parameter, EditorRequired] public IEnumerable<TItem> Items { get; set; } = [];
    [Parameter, EditorRequired] public Func<TItem, TValue> ValueSelector { get; set; } = default!;
    [Parameter, EditorRequired] public Func<TItem, string> DisplaySelector { get; set; } = default!;
    [Parameter] public TValue? SelectedValue { get; set; }
    [Parameter] public EventCallback<TValue> SelectedValueChanged { get; set; }

    private async Task OnSelectionChanged(ChangeEventArgs e)
    {
        var value = (TValue)Convert.ChangeType(e.Value, typeof(TValue))!;
        await SelectedValueChanged.InvokeAsync(value);
    }
}
```

### Cascading Values Pattern

Share data down the component tree without explicit parameter passing.

```razor
@* App.razor or Layout *@
<CascadingValue Value="@theme" Name="AppTheme">
    <CascadingValue Value="@currentUser">
        @Body
    </CascadingValue>
</CascadingValue>

@code {
    private Theme theme = new() { IsDarkMode = false };
    private User? currentUser;
}
```

```razor
@* Any nested component *@
@code {
    [CascadingParameter(Name = "AppTheme")]
    public Theme Theme { get; set; } = default!;

    [CascadingParameter]
    public User? CurrentUser { get; set; }
}
```

### Component Inheritance

Share logic across related components.

```csharp
// BaseFormComponent.cs
public abstract class BaseFormComponent<TModel> : ComponentBase
{
    [Parameter] public TModel? Model { get; set; }
    [Parameter] public EventCallback<TModel> OnSubmit { get; set; }

    protected bool IsSubmitting { get; set; }
    protected string? ErrorMessage { get; set; }

    protected async Task HandleSubmit()
    {
        IsSubmitting = true;
        ErrorMessage = null;

        try
        {
            await OnSubmit.InvokeAsync(Model);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
```

```razor
@* ProductForm.razor *@
@inherits BaseFormComponent<Product>

<EditForm Model="@Model" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />
    @* Form fields *@
    <button type="submit" disabled="@IsSubmitting">Save</button>
    @if (ErrorMessage is not null)
    {
        <p class="error">@ErrorMessage</p>
    }
</EditForm>
```

## State Management Patterns

### Component-Level State

For isolated, component-specific state.

```razor
@code {
    private int count = 0;
    private string message = "";

    private void Increment() => count++;
}
```

### Service-Based Shared State

For state shared across multiple components using DI.

```csharp
// CartState.cs
public class CartState
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public decimal Total => _items.Sum(i => i.Price * i.Quantity);

    public event Action? OnChange;

    public void AddItem(Product product, int quantity = 1)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _items.Add(new CartItem(product.Id, product.Name, product.Price, quantity));
        }
        NotifyStateChanged();
    }

    public void RemoveItem(int productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

```razor
@* CartIcon.razor *@
@inject CartState Cart
@implements IDisposable

<span class="cart-icon">
    Cart (@Cart.Items.Count)
</span>

@code {
    protected override void OnInitialized()
    {
        Cart.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        Cart.OnChange -= StateHasChanged;
    }
}
```

### Fluxor Pattern (Redux-like)

For complex applications needing predictable state management.

```csharp
// State
public record CounterState(int Count);

// Actions
public record IncrementAction;
public record DecrementAction;
public record SetCountAction(int Value);

// Reducer
public static class CounterReducers
{
    [ReducerMethod]
    public static CounterState OnIncrement(CounterState state, IncrementAction action)
        => state with { Count = state.Count + 1 };

    [ReducerMethod]
    public static CounterState OnDecrement(CounterState state, DecrementAction action)
        => state with { Count = state.Count - 1 };

    [ReducerMethod]
    public static CounterState OnSetCount(CounterState state, SetCountAction action)
        => state with { Count = action.Value };
}

// Effects (side effects)
public class CounterEffects
{
    [EffectMethod]
    public async Task HandleSetCountAsync(SetCountAction action, IDispatcher dispatcher)
    {
        await Task.Delay(100); // Simulate async work
        // Dispatch additional actions if needed
    }
}
```

```razor
@inject IState<CounterState> CounterState
@inject IDispatcher Dispatcher

<p>Count: @CounterState.Value.Count</p>
<button @onclick="Increment">+</button>

@code {
    private void Increment() => Dispatcher.Dispatch(new IncrementAction());
}
```

### Persistent State (Prerendering)

Handle state that must survive the prerender-to-interactive transition.

```razor
@inject PersistentComponentState ApplicationState

@code {
    private List<Product>? products;
    private PersistingComponentStateSubscription persistingSubscription;

    protected override async Task OnInitializedAsync()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<List<Product>>("products", out var restored))
        {
            products = await FetchProducts();
        }
        else
        {
            products = restored;
        }
    }

    private Task PersistData()
    {
        ApplicationState.PersistAsJson("products", products);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        persistingSubscription.Dispose();
    }
}
```

### .NET 8+ Simplified Persistent State

```razor
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

## JavaScript Interop Patterns

### Module Isolation

Encapsulate JS code in ES6 modules for better organization.

```javascript
// wwwroot/js/map.js
export function initializeMap(elementId, options) {
    const map = new MapLibrary(document.getElementById(elementId), options);
    return DotNet.createJSObjectReference(map);
}

export function setMarker(map, lat, lng) {
    map.addMarker({ lat, lng });
}

export function dispose(map) {
    map.destroy();
}
```

```razor
@inject IJSRuntime JS
@implements IAsyncDisposable

<div id="map-container"></div>

@code {
    private IJSObjectReference? module;
    private IJSObjectReference? mapInstance;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./js/map.js");
            mapInstance = await module.InvokeAsync<IJSObjectReference>(
                "initializeMap", "map-container", new { zoom = 10 });
        }
    }

    private async Task AddMarker(double lat, double lng)
    {
        if (module is not null && mapInstance is not null)
        {
            await module.InvokeVoidAsync("setMarker", mapInstance, lat, lng);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            if (mapInstance is not null)
            {
                await module.InvokeVoidAsync("dispose", mapInstance);
            }
            await module.DisposeAsync();
        }
    }
}
```

### JS Interop Abstraction Service

Wrap JS interop in a typed service for better testability.

```csharp
// ILocalStorage.cs
public interface ILocalStorage
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}

// LocalStorageService.cs
public class LocalStorageService : ILocalStorage
{
    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js) => _js = js;

    public async Task<T?> GetItemAsync<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        await _js.InvokeVoidAsync("localStorage.setItem", key, json);
    }

    public async Task RemoveItemAsync(string key)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
```

### Handling JS Interop in Prerendering

```razor
@inject IJSRuntime JS

@code {
    private bool isInteractive = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isInteractive = true;
            StateHasChanged();

            // Safe to call JS here
            await JS.InvokeVoidAsync("console.log", "Component is interactive");
        }
    }
}
```

### .NET to JS Streaming

```csharp
// Stream large data to JavaScript
using var streamRef = new DotNetStreamReference(stream: myLargeDataStream);
await JS.InvokeVoidAsync("receiveStream", streamRef);
```

```javascript
async function receiveStream(streamRef) {
    const data = await streamRef.arrayBuffer();
    // Process data
}
```

## Advanced Patterns

### Dynamic Component Loading

```razor
<DynamicComponent Type="@componentType" Parameters="@parameters" />

@code {
    private Type? componentType;
    private Dictionary<string, object>? parameters;

    private void LoadComponent(string name)
    {
        componentType = name switch
        {
            "chart" => typeof(ChartComponent),
            "table" => typeof(TableComponent),
            _ => typeof(PlaceholderComponent)
        };

        parameters = new Dictionary<string, object>
        {
            { "Data", currentData }
        };
    }
}
```

### Render Mode Boundary Pattern

Isolate interactive components from static content.

```razor
@* StaticLayout.razor - No render mode *@
<header>
    <nav>Static navigation</nav>
</header>

<main>
    @Body
</main>

<footer>Static footer</footer>
```

```razor
@* InteractiveDashboard.razor *@
@rendermode InteractiveServer

<div class="dashboard">
    <RealTimeChart />
    <LiveNotifications />
</div>
```

### Error Boundary Pattern

```razor
<ErrorBoundary @ref="errorBoundary">
    <ChildContent>
        <RiskyComponent />
    </ChildContent>
    <ErrorContent Context="exception">
        <div class="error-panel">
            <h3>Something went wrong</h3>
            <p>@exception.Message</p>
            <button @onclick="Recover">Try Again</button>
        </div>
    </ErrorContent>
</ErrorBoundary>

@code {
    private ErrorBoundary? errorBoundary;

    private void Recover()
    {
        errorBoundary?.Recover();
    }
}
```

### Section Pattern (.NET 8+)

Define content slots that can be filled from nested components.

```razor
@* MainLayout.razor *@
<header>
    <SectionOutlet SectionName="PageHeader" />
</header>

<main>@Body</main>

<aside>
    <SectionOutlet SectionName="Sidebar" />
</aside>
```

```razor
@* ProductPage.razor *@
<SectionContent SectionName="PageHeader">
    <h1>Products</h1>
    <SearchBar />
</SectionContent>

<SectionContent SectionName="Sidebar">
    <CategoryFilter />
    <PriceRangeFilter />
</SectionContent>

<ProductGrid Products="@products" />
```

### Render Optimization with ShouldRender

```razor
@code {
    private string? previousValue;

    [Parameter] public string? Value { get; set; }

    protected override bool ShouldRender()
    {
        // Only re-render if Value actually changed
        var shouldRender = Value != previousValue;
        previousValue = Value;
        return shouldRender;
    }
}
```

### Form Validation Pattern

```razor
<EditForm Model="@model" OnValidSubmit="HandleSubmit" FormName="ProductForm">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <div class="form-group">
        <label for="name">Name</label>
        <InputText id="name" @bind-Value="model.Name" class="form-control" />
        <ValidationMessage For="() => model.Name" />
    </div>

    <div class="form-group">
        <label for="price">Price</label>
        <InputNumber id="price" @bind-Value="model.Price" class="form-control" />
        <ValidationMessage For="() => model.Price" />
    </div>

    <button type="submit" disabled="@isSubmitting">
        @(isSubmitting ? "Saving..." : "Save")
    </button>
</EditForm>

@code {
    [SupplyParameterFromForm]
    private ProductModel model { get; set; } = new();

    private bool isSubmitting = false;

    private async Task HandleSubmit()
    {
        isSubmitting = true;
        try
        {
            await ProductService.SaveAsync(model);
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
```
