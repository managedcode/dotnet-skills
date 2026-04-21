# MAUI Anti-Patterns

## Navigation Anti-Patterns

### Coupling Views to Navigation

```csharp
// BAD: Navigation logic in code-behind
public partial class ProductsPage : ContentPage
{
    private async void OnProductTapped(object sender, EventArgs e)
    {
        var product = (sender as View).BindingContext as Product;
        await Navigation.PushAsync(new ProductDetailPage(product)); // Direct coupling
    }
}

// GOOD: Navigation through ViewModel with service
public partial class ProductsViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [RelayCommand]
    private async Task SelectProduct(Product product)
    {
        await _navigation.NavigateToAsync<ProductDetailViewModel>(product.Id);
    }
}
```

### Hardcoded Route Strings Everywhere

```csharp
// BAD: Magic strings scattered across codebase
await Shell.Current.GoToAsync("ProductDetailPage?id=123");
await Shell.Current.GoToAsync("productdetail?id=123"); // Inconsistent casing
await Shell.Current.GoToAsync("product-detail?id=123"); // Different format

// GOOD: Centralized route constants
public static class Routes
{
    public const string ProductDetail = nameof(ProductDetailPage);
    public const string Checkout = nameof(CheckoutPage);
    public const string OrderConfirmation = nameof(OrderConfirmationPage);
}

// Usage
await Shell.Current.GoToAsync($"{Routes.ProductDetail}?id={product.Id}");
```

### Navigation State Leaks

```csharp
// BAD: Not cleaning up when navigating away
public partial class CameraPage : ContentPage
{
    private CameraPreview _camera;

    protected override void OnAppearing()
    {
        _camera.Start(); // Camera keeps running when navigating away
    }
}

// GOOD: Proper lifecycle management
public partial class CameraPage : ContentPage
{
    private CameraPreview _camera;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _camera.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _camera.Stop();
    }
}
```

## MVVM Anti-Patterns

### God ViewModel

```csharp
// BAD: One ViewModel doing everything
public class MainViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; }
    public ObservableCollection<CartItem> CartItems { get; }
    public User CurrentUser { get; }
    public ObservableCollection<Order> Orders { get; }
    public Settings AppSettings { get; }

    // Hundreds of commands and properties for all features
    public ICommand LoadProductsCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand UpdateSettingsCommand { get; }
    // ... 50 more commands
}

// GOOD: Focused ViewModels
public partial class ProductsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Product> _products;

    [RelayCommand]
    private async Task LoadProducts() { }

    [RelayCommand]
    private async Task SelectProduct(Product product) { }
}
```

### Logic in Code-Behind

```csharp
// BAD: Business logic in code-behind
public partial class CheckoutPage : ContentPage
{
    private async void OnCheckoutClicked(object sender, EventArgs e)
    {
        var total = _items.Sum(i => i.Price * i.Quantity);
        if (total > 1000)
        {
            total *= 0.9; // Apply discount
        }

        var order = new Order { Total = total, Items = _items };
        await _orderService.CreateOrderAsync(order);

        await DisplayAlert("Success", "Order placed!", "OK");
        await Navigation.PopToRootAsync();
    }
}

// GOOD: Logic in ViewModel, code-behind is thin
public partial class CheckoutPage : ContentPage
{
    public CheckoutPage(CheckoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

public partial class CheckoutViewModel : ObservableObject
{
    [RelayCommand]
    private async Task Checkout()
    {
        var order = _orderService.CalculateOrder(Items);
        await _orderService.CreateOrderAsync(order);
        await _navigation.NavigateToAsync<OrderConfirmationViewModel>(order.Id);
    }
}
```

### Not Using Compiled Bindings

```xml
<!-- BAD: Reflection-based binding (slow, no compile-time checking) -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui">
    <Label Text="{Binding ProductName}" />
    <Label Text="{Binding Price}" /> <!-- Typo won't be caught -->
</ContentPage>

<!-- GOOD: Compiled bindings (fast, compile-time checked) -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:vm="clr-namespace:MyApp.ViewModels"
             x:DataType="vm:ProductViewModel">
    <Label Text="{Binding Name}" />
    <Label Text="{Binding Price, StringFormat='{0:C}'}" />
</ContentPage>
```

## Platform Code Anti-Patterns

### Preprocessor Directives Everywhere

```csharp
// BAD: Conditional compilation scattered throughout codebase
public class ProductService
{
    public async Task<string> GetDeviceToken()
    {
#if ANDROID
        var token = await FirebaseMessaging.Instance.GetToken();
        return token.ToString();
#elif IOS
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        if (settings.AuthorizationStatus == UNAuthorizationStatus.Authorized)
        {
            return UIApplication.SharedApplication.ValueForKey(
                new NSString("deviceToken")).ToString();
        }
        return null;
#elif WINDOWS
        var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
        return channel.Uri;
#endif
    }

    // This pattern repeated in dozens of methods...
}

// GOOD: Platform abstraction with DI
public interface IPushNotificationService
{
    Task<string> GetDeviceTokenAsync();
}

// Platforms/Android/PushNotificationService.cs
public class AndroidPushNotificationService : IPushNotificationService
{
    public async Task<string> GetDeviceTokenAsync()
    {
        var token = await FirebaseMessaging.Instance.GetToken();
        return token.ToString();
    }
}

// Register in MauiProgram.cs
#if ANDROID
builder.Services.AddSingleton<IPushNotificationService, AndroidPushNotificationService>();
#endif
```

### Direct Platform API Calls in Shared Code

```csharp
// BAD: Android-specific code in shared ViewModel
public partial class SettingsViewModel : ObservableObject
{
    [RelayCommand]
    private void OpenAppSettings()
    {
        var intent = new Android.Content.Intent(
            Android.Provider.Settings.ActionApplicationDetailsSettings);
        intent.SetData(Android.Net.Uri.Parse("package:" +
            Android.App.Application.Context.PackageName));
        Android.App.Application.Context.StartActivity(intent);
    }
}

// GOOD: Use MAUI Essentials or abstraction
public partial class SettingsViewModel : ObservableObject
{
    [RelayCommand]
    private async Task OpenAppSettings()
    {
        await Launcher.OpenAsync(new Uri("app-settings:"));
    }
}
```

## Performance Anti-Patterns

### Synchronous Operations on UI Thread

```csharp
// BAD: Blocking UI thread
public partial class ProductsViewModel : ObservableObject
{
    [RelayCommand]
    private void LoadProducts()
    {
        var json = File.ReadAllText("products.json"); // Blocks UI
        var products = JsonSerializer.Deserialize<List<Product>>(json);
        Products = new ObservableCollection<Product>(products);
    }
}

// GOOD: Async operations
public partial class ProductsViewModel : ObservableObject
{
    [RelayCommand]
    private async Task LoadProducts()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("products.json");
        var products = await JsonSerializer.DeserializeAsync<List<Product>>(stream);
        Products = new ObservableCollection<Product>(products);
    }
}
```

### Creating New Collections Instead of Updating

```csharp
// BAD: Replacing entire collection (causes full UI refresh)
public partial class ProductsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Product> _products;

    [RelayCommand]
    private async Task RefreshProducts()
    {
        var items = await _service.GetProductsAsync();
        Products = new ObservableCollection<Product>(items); // Full rebind
    }
}

// GOOD: Update existing collection
public partial class ProductsViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; } = new();

    [RelayCommand]
    private async Task RefreshProducts()
    {
        var items = await _service.GetProductsAsync();
        Products.Clear();
        foreach (var item in items)
        {
            Products.Add(item);
        }
    }

    // Or use batch updates for large collections
    [RelayCommand]
    private async Task RefreshProductsBatched()
    {
        var items = await _service.GetProductsAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Products.Clear();
            foreach (var item in items)
            {
                Products.Add(item);
            }
        });
    }
}
```

### Not Virtualizing Large Lists

```xml
<!-- BAD: StackLayout doesn't virtualize -->
<ScrollView>
    <StackLayout BindableLayout.ItemsSource="{Binding Products}">
        <BindableLayout.ItemTemplate>
            <DataTemplate>
                <Label Text="{Binding Name}" />
            </DataTemplate>
        </BindableLayout.ItemTemplate>
    </StackLayout>
</ScrollView>

<!-- GOOD: CollectionView virtualizes items -->
<CollectionView ItemsSource="{Binding Products}"
                ItemSizingStrategy="MeasureFirstItem">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="models:Product">
            <Label Text="{Binding Name}" />
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### Loading Full-Size Images

```csharp
// BAD: Loading original high-res images
<Image Source="{Binding ImageUrl}" />

// GOOD: Use appropriate size and caching
<Image>
    <Image.Source>
        <UriImageSource Uri="{Binding ThumbnailUrl}"
                        CacheValidity="7"
                        CachingEnabled="True" />
    </Image.Source>
</Image>

// Or resize in code
public static ImageSource GetOptimizedImage(string url, int width, int height)
{
    // Use image CDN or resize parameter
    return ImageSource.FromUri(new Uri($"{url}?w={width}&h={height}"));
}
```

## Lifecycle Anti-Patterns

### Not Handling App Lifecycle

```csharp
// BAD: Ignoring lifecycle events
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
    // No lifecycle handling
}

// GOOD: Proper lifecycle management
public partial class App : Application
{
    private readonly IAppStateService _stateService;

    public App(IAppStateService stateService)
    {
        InitializeComponent();
        _stateService = stateService;
        MainPage = new AppShell();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _stateService.SaveState();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _stateService.RestoreState();
    }
}
```

### Memory Leaks from Event Handlers

```csharp
// BAD: Event handler not unsubscribed
public partial class ProductsPage : ContentPage
{
    protected override void OnAppearing()
    {
        base.OnAppearing();
        MessagingCenter.Subscribe<CartViewModel>(this, "CartUpdated", OnCartUpdated);
    }

    // OnDisappearing never called or handler not removed
}

// GOOD: Proper subscription management
public partial class ProductsPage : ContentPage
{
    protected override void OnAppearing()
    {
        base.OnAppearing();
        WeakReferenceMessenger.Default.Register<CartUpdatedMessage>(this, OnCartUpdated);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<CartUpdatedMessage>(this);
    }
}
```

### Timer Not Disposed

```csharp
// BAD: Timer keeps running forever
public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
        var timer = new System.Timers.Timer(5000);
        timer.Elapsed += async (s, e) => await RefreshData();
        timer.Start(); // Never stopped
    }
}

// GOOD: Timer properly managed
public partial class DashboardPage : ContentPage
{
    private IDispatcherTimer _timer;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        _timer = null;
    }

    private async void OnTimerTick(object sender, EventArgs e)
    {
        await RefreshData();
    }
}
```

## Dependency Injection Anti-Patterns

### Service Locator Pattern

```csharp
// BAD: Service locator anti-pattern
public class ProductsViewModel
{
    private readonly IProductService _service;

    public ProductsViewModel()
    {
        _service = App.Services.GetService<IProductService>(); // Hidden dependency
    }
}

// GOOD: Constructor injection
public partial class ProductsViewModel : ObservableObject
{
    private readonly IProductService _service;

    public ProductsViewModel(IProductService service)
    {
        _service = service; // Explicit dependency
    }
}
```

### Not Registering Pages

```csharp
// BAD: Creating pages manually
public class NavigationService
{
    public async Task NavigateToProductDetail(int productId)
    {
        var viewModel = App.Services.GetService<ProductDetailViewModel>();
        var page = new ProductDetailPage { BindingContext = viewModel };
        await Shell.Current.Navigation.PushAsync(page);
    }
}

// GOOD: Register pages and ViewModels
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Register ViewModels
        builder.Services.AddTransient<ProductDetailViewModel>();

        // Register Pages
        builder.Services.AddTransient<ProductDetailPage>();

        // Register routes
        Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));

        return builder.Build();
    }
}
```

## Testing Anti-Patterns

### ViewModels Dependent on Platform

```csharp
// BAD: ViewModel uses platform APIs directly
public class SettingsViewModel
{
    public string DeviceId => DeviceInfo.Current.Idiom.ToString(); // Hard to test
}

// GOOD: Abstract platform dependencies
public interface IDeviceInfoService
{
    string GetDeviceIdiom();
}

public class SettingsViewModel
{
    private readonly IDeviceInfoService _deviceInfo;

    public SettingsViewModel(IDeviceInfoService deviceInfo)
    {
        _deviceInfo = deviceInfo;
    }

    public string DeviceId => _deviceInfo.GetDeviceIdiom();
}

// In tests
var mockDeviceInfo = new Mock<IDeviceInfoService>();
mockDeviceInfo.Setup(d => d.GetDeviceIdiom()).Returns("Phone");
var viewModel = new SettingsViewModel(mockDeviceInfo.Object);
```

### Not Testing Commands

```csharp
// BAD: Commands that are hard to test
public class ProductsViewModel
{
    public ICommand LoadCommand => new Command(async () =>
    {
        var products = await _service.GetProductsAsync();
        Products = new ObservableCollection<Product>(products);
    });
}

// GOOD: Use RelayCommand with testable methods
public partial class ProductsViewModel : ObservableObject
{
    [RelayCommand]
    private async Task LoadProducts()
    {
        var products = await _service.GetProductsAsync();
        Products = new ObservableCollection<Product>(products);
    }
}

// Test
[Fact]
public async Task LoadProducts_PopulatesCollection()
{
    var mockService = new Mock<IProductService>();
    mockService.Setup(s => s.GetProductsAsync())
        .ReturnsAsync(new[] { new Product { Name = "Test" } });

    var viewModel = new ProductsViewModel(mockService.Object);

    await viewModel.LoadProductsCommand.ExecuteAsync(null);

    Assert.Single(viewModel.Products);
}
```

## Resource Anti-Patterns

### Hardcoded Colors and Sizes

```xml
<!-- BAD: Hardcoded values -->
<Button BackgroundColor="#512BD4"
        TextColor="White"
        FontSize="16"
        Padding="16,10"
        CornerRadius="8" />

<Button BackgroundColor="#512BD4"
        TextColor="White"
        FontSize="16"
        Padding="16,10"
        CornerRadius="8" />

<!-- GOOD: Use resources and styles -->
<ContentPage.Resources>
    <Color x:Key="PrimaryColor">#512BD4</Color>
    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="BackgroundColor" Value="{StaticResource PrimaryColor}" />
        <Setter Property="TextColor" Value="White" />
        <Setter Property="FontSize" Value="16" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="CornerRadius" Value="8" />
    </Style>
</ContentPage.Resources>

<Button Style="{StaticResource PrimaryButton}" Text="Submit" />
<Button Style="{StaticResource PrimaryButton}" Text="Save" />
```

### Not Supporting Dark Mode

```xml
<!-- BAD: Fixed colors that don't adapt -->
<ContentPage BackgroundColor="White">
    <Label TextColor="Black" Text="Hello" />
</ContentPage>

<!-- GOOD: Theme-aware colors -->
<ContentPage BackgroundColor="{AppThemeBinding Light=White, Dark=#1E1E1E}">
    <Label TextColor="{AppThemeBinding Light=Black, Dark=White}" Text="Hello" />
</ContentPage>

<!-- Or use semantic colors from resources -->
<ContentPage BackgroundColor="{DynamicResource PageBackgroundColor}">
    <Label TextColor="{DynamicResource PrimaryTextColor}" Text="Hello" />
</ContentPage>
```
