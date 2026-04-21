---
name: maui
description: "Build, review, or migrate .NET MAUI applications across Android, iOS, macOS, and Windows with correct cross-platform UI, platform integration, and native packaging assumptions."
compatibility: "Requires .NET MAUI workload (.NET 8+)."
---

# .NET MAUI

## Trigger On

- working on cross-platform mobile or desktop UI in .NET MAUI
- integrating device capabilities, navigation, or platform-specific code
- migrating Xamarin.Forms or aligning a shared codebase across targets
- implementing MVVM patterns in mobile apps

## Documentation

- [.NET MAUI Overview](https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui)
- [Enterprise Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/maui/)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [Controls Reference](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/)
- [Platform Integration](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/)

### References

- [patterns.md](references/patterns.md) - Shell navigation, platform-specific code, messaging, lifecycle, data binding, and CollectionView patterns
- [anti-patterns.md](references/anti-patterns.md) - Common MAUI mistakes and how to avoid them

## Platform Targets

| Platform | Build Host | Notes |
|----------|------------|-------|
| Android | Windows/Mac | Emulator or device |
| iOS | Mac only | Requires Xcode |
| macOS | Mac only | Catalyst |
| Windows | Windows | WinUI 3 |

## Workflow

1. **Confirm target platforms** — behavior differs across Android, iOS, Mac, Windows
2. **Separate shared UI and platform code** — use handlers and DI
3. **Follow MVVM pattern** — keep views dumb, logic in ViewModels
4. **Handle lifecycle and permissions** — platform contracts need testing
5. **Test on real devices** — emulators don't catch everything

## Project Structure

```
MyApp/
├── MyApp/                    # Shared code
│   ├── App.xaml              # Application entry
│   ├── MauiProgram.cs        # DI and configuration
│   ├── Views/                # XAML pages
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Models/               # Domain models
│   ├── Services/             # Business logic
│   └── Platforms/            # Platform-specific code
│       ├── Android/
│       ├── iOS/
│       ├── MacCatalyst/
│       └── Windows/
└── MyApp.Tests/
```

## MVVM Pattern

### ViewModel with MVVM Toolkit
```csharp
public partial class ProductsViewModel(IProductService productService) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Product> _products = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProductsCommand))]
    private bool _isLoading;

    [RelayCommand(CanExecute = nameof(CanLoadProducts))]
    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        try
        {
            var items = await productService.GetAllAsync();
            Products = new ObservableCollection<Product>(items);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoadProducts() => !IsLoading;
}
```

### View Binding
```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:MyApp.ViewModels"
             x:Class="MyApp.Views.ProductsPage"
             x:DataType="vm:ProductsViewModel">

    <RefreshView Command="{Binding LoadProductsCommand}"
                 IsRefreshing="{Binding IsLoading}">
        <CollectionView ItemsSource="{Binding Products}">
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="models:Product">
                    <VerticalStackLayout Padding="10">
                        <Label Text="{Binding Name}" FontSize="18" />
                        <Label Text="{Binding Price, StringFormat='{0:C}'}" />
                    </VerticalStackLayout>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </RefreshView>
</ContentPage>
```

## Dependency Injection

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Services
        builder.Services.AddSingleton<IProductService, ProductService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        builder.Services.AddTransient<ProductsViewModel>();
        builder.Services.AddTransient<ProductDetailViewModel>();

        // Pages
        builder.Services.AddTransient<ProductsPage>();
        builder.Services.AddTransient<ProductDetailPage>();

        return builder.Build();
    }
}
```

## Navigation

### Shell Navigation
```csharp
// Register routes
Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));

// Navigate with parameters
await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?id={product.Id}");

// Receive parameters
[QueryProperty(nameof(ProductId), "id")]
public partial class ProductDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _productId;

    partial void OnProductIdChanged(string value)
    {
        LoadProduct(value);
    }
}
```

### Navigation Service
```csharp
public interface INavigationService
{
    Task NavigateToAsync<TViewModel>(object? parameter = null);
    Task GoBackAsync();
}

public class NavigationService : INavigationService
{
    public async Task NavigateToAsync<TViewModel>(object? parameter = null)
    {
        var route = typeof(TViewModel).Name.Replace("ViewModel", "Page");
        var query = parameter is null ? "" : $"?id={parameter}";
        await Shell.Current.GoToAsync($"{route}{query}");
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
```

## Platform-Specific Code

### Using Partial Classes
```csharp
// Services/DeviceService.cs (shared)
public partial class DeviceService
{
    public partial string GetDeviceId();
}

// Platforms/Android/DeviceService.cs
public partial class DeviceService
{
    public partial string GetDeviceId()
    {
        return Android.Provider.Settings.Secure.GetString(
            Android.App.Application.Context.ContentResolver,
            Android.Provider.Settings.Secure.AndroidId);
    }
}

// Platforms/iOS/DeviceService.cs
public partial class DeviceService
{
    public partial string GetDeviceId()
    {
        return UIKit.UIDevice.CurrentDevice.IdentifierForVendor?.ToString() ?? "";
    }
}
```

### Conditional Compilation
```csharp
public string GetPlatformInfo()
{
#if ANDROID
    return $"Android {Android.OS.Build.VERSION.Release}";
#elif IOS
    return $"iOS {UIKit.UIDevice.CurrentDevice.SystemVersion}";
#elif MACCATALYST
    return "macOS Catalyst";
#elif WINDOWS
    return "Windows";
#else
    return "Unknown";
#endif
}
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| God ViewModel | Unmaintainable | Split into focused ViewModels |
| Logic in code-behind | Hard to test | Use MVVM and commands |
| Platform code everywhere | Defeats cross-platform | Use handlers/DI |
| Direct service calls in Views | Tight coupling | Use ViewModel |
| Ignoring lifecycle | Crashes, leaks | Handle lifecycle events |

## Performance Best Practices

1. **Use compiled bindings:**
   ```xml
   <ContentPage x:DataType="vm:ProductsViewModel">
   ```

2. **Virtualize long lists:**
   ```xml
   <CollectionView ItemsSource="{Binding Items}"
                   ItemSizingStrategy="MeasureFirstItem" />
   ```

3. **Optimize images:**
   ```csharp
   var image = ImageSource.FromFile("image.png");
   // Use appropriate resolution for platform
   ```

4. **Avoid synchronous work on UI thread:**
   ```csharp
   // Bad
   var data = service.GetData(); // Blocks UI

   // Good
   var data = await service.GetDataAsync();
   ```

## Testing

```csharp
[Fact]
public async Task LoadProducts_UpdatesCollection()
{
    var mockService = new Mock<IProductService>();
    mockService.Setup(s => s.GetAllAsync())
        .ReturnsAsync(new[] { new Product { Name = "Test" } });

    var viewModel = new ProductsViewModel(mockService.Object);

    await viewModel.LoadProductsCommand.ExecuteAsync(null);

    Assert.Single(viewModel.Products);
    Assert.Equal("Test", viewModel.Products[0].Name);
}
```

## Deliver

- shared MAUI code with explicit platform seams
- MVVM pattern with testable ViewModels
- navigation and lifecycle behavior that fits each target
- a realistic build and deployment path for the chosen platforms

## Validate

- cross-platform reuse is real, not superficial
- platform-specific behavior is isolated and testable
- MVVM pattern is followed consistently
- build assumptions for Mac/iOS and Windows are explicit
- performance is acceptable on target devices
