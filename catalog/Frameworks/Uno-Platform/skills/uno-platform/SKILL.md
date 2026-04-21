---
name: uno-platform
description: "Build cross-platform .NET applications with Uno Platform targeting WebAssembly, iOS, Android, macOS, Linux, and Windows from a single XAML/C# codebase."
compatibility: "Requires Uno Platform SDK and workloads for target platforms."
---

# Uno Platform

## Trigger On

- building cross-platform apps from a single C# and XAML codebase
- targeting WebAssembly, iOS, Android, macOS, Linux, and Windows simultaneously
- migrating WPF or UWP applications to cross-platform
- implementing pixel-perfect UI across all platforms
- using WinUI/UWP APIs on non-Windows platforms

## Documentation

- [Uno Platform Overview](https://platform.uno/docs/articles/intro.html)
- [Getting Started](https://platform.uno/docs/articles/getting-started.html)
- [Using Uno Platform with WinUI](https://platform.uno/docs/articles/winui-doc-links.html)
- [Platform-Specific Code](https://platform.uno/docs/articles/platform-specific-xaml.html)
- [MVUX Pattern](https://platform.uno/docs/articles/external/uno.extensions/doc/Overview/Mvux/Overview.html)

## References

See detailed examples in the `references/` folder:
- [`patterns.md`](references/patterns.md) — MVUX, XAML, navigation, and performance patterns

## Platform Support

| Platform | Rendering | Notes |
|----------|-----------|-------|
| Windows | WinUI 3 | Native Windows App SDK |
| WebAssembly | Skia/Canvas | Runs in browser |
| iOS | Skia/Metal | Native iOS app |
| Android | Skia/OpenGL | Native Android app |
| macOS | Skia/Metal | Mac Catalyst or AppKit |
| Linux | Skia/X11 | GTK or Framebuffer |

## Workflow

1. **Choose the right template** — Uno Platform offers various templates for different scenarios
2. **Understand rendering modes** — Skia vs native rendering affects performance and fidelity
3. **Apply MVVM or MVUX patterns** — keep views dumb, logic in ViewModels
4. **Handle platform differences** — use conditional XAML or partial classes
5. **Test on all target platforms** — behavior varies across platforms

## Project Structure

```
MyApp/
├── MyApp/                    # Shared code
│   ├── App.xaml              # Application entry
│   ├── MainPage.xaml         # Main page
│   ├── Presentation/         # ViewModels (MVUX/MVVM)
│   ├── Business/             # Business logic
│   └── Services/             # Platform services
├── MyApp.Wasm/               # WebAssembly head
├── MyApp.Mobile/             # iOS and Android head
├── MyApp.Skia.Gtk/           # Linux head
├── MyApp.Skia.WPF/           # Windows Skia head
└── MyApp.Windows/            # Native WinUI head
```

## MVUX Pattern (Uno Extensions)

### Model Definition
```csharp
public partial record MainModel
{
    public IListFeed<TodoItem> Items => ListFeed.Async(LoadItems);

    private async ValueTask<IImmutableList<TodoItem>> LoadItems(CancellationToken ct)
    {
        var items = await _todoService.GetAllAsync(ct);
        return items.ToImmutableList();
    }
}
```

### View Binding with FeedView
```xml
<Page xmlns:uen="using:Uno.Extensions.Navigation.UI">
    <utu:FeedView Source="{Binding Items}">
        <utu:FeedView.ValueTemplate>
            <DataTemplate>
                <ListView ItemsSource="{Binding}">
                    <ListView.ItemTemplate>
                        <DataTemplate x:DataType="local:TodoItem">
                            <TextBlock Text="{Binding Title}" />
                        </DataTemplate>
                    </ListView.ItemTemplate>
                </ListView>
            </DataTemplate>
        </utu:FeedView.ValueTemplate>
        <utu:FeedView.ProgressTemplate>
            <DataTemplate>
                <ProgressRing IsActive="True" />
            </DataTemplate>
        </utu:FeedView.ProgressTemplate>
    </utu:FeedView>
</Page>
```

## Classic MVVM with MVVM Toolkit

### ViewModel
```csharp
public partial class MainViewModel(ITodoService todoService) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TodoItem> _items = [];

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var items = await todoService.GetAllAsync();
            Items = new ObservableCollection<TodoItem>(items);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

## Platform-Specific Code

### Conditional XAML
```xml
<TextBlock Text="Welcome"
           xmlns:win="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
           xmlns:android="http://uno.ui/android"
           xmlns:ios="http://uno.ui/ios"
           xmlns:wasm="http://uno.ui/wasm">

    <win:TextBlock.Foreground>
        <SolidColorBrush Color="Blue" />
    </win:TextBlock.Foreground>

    <android:TextBlock.Foreground>
        <SolidColorBrush Color="Green" />
    </android:TextBlock.Foreground>
</TextBlock>
```

### Partial Classes
```csharp
// Services/DeviceService.cs (shared)
public partial class DeviceService
{
    public partial string GetDeviceInfo();
}

// Services/DeviceService.wasm.cs
public partial class DeviceService
{
    public partial string GetDeviceInfo() => "WebAssembly";
}

// Services/DeviceService.Android.cs
public partial class DeviceService
{
    public partial string GetDeviceInfo() =>
        $"Android {Android.OS.Build.VERSION.Release}";
}
```

## Hot Reload and Development

```csharp
// Enable Hot Reload in App.xaml.cs
public App()
{
    this.InitializeComponent();

#if DEBUG
    // Enable Hot Reload
    this.EnableHotReload();
#endif
}
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Platform code in shared | Breaks compilation | Use partial classes or #if |
| Ignoring Skia differences | Visual bugs | Test on all renderers |
| WPF assumptions | Not all APIs exist | Check Uno API coverage |
| Heavy XAML | Slow on WASM | Virtualize, simplify |
| Synchronous loading | UI freezes | Always use async |

## Performance Best Practices

1. **Use virtualized lists:**
   ```xml
   <ListView ItemsSource="{Binding Items}"
             VirtualizingPanel.VirtualizationMode="Recycling" />
   ```

2. **Lazy load resources:**
   ```csharp
   // Load images on demand
   var image = await ImageSource.LoadFromUriAsync(uri);
   ```

3. **Minimize XAML complexity:**
   - Avoid deep nesting
   - Use compiled bindings (`x:Bind` where supported)
   - Consider Skia-specific optimizations

4. **WebAssembly specific:**
   - Minimize interop calls
   - Use `InvokeAsync` for JS interop
   - Consider AOT compilation for performance

## Uno Extensions

```csharp
// Use Uno.Extensions for enhanced patterns
var builder = this.CreateBuilder(args)
    .Configure(host => host
        .UseConfiguration()
        .UseLocalization()
        .UseNavigation()
        .UseMvux()
        .ConfigureServices(services =>
        {
            services.AddSingleton<ITodoService, TodoService>();
        }));
```

## Deliver

- single codebase running on web, mobile, and desktop
- consistent UI/UX across all platforms
- platform-specific optimizations where needed
- MVVM or MVUX patterns for testability

## Validate

- app builds and runs on all target platforms
- platform-specific features work correctly
- performance is acceptable on WebAssembly
- Hot Reload works during development
- no WPF/UWP-only APIs are used without fallbacks
