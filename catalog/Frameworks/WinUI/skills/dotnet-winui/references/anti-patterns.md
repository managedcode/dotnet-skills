# WinUI 3 Anti-Patterns

Common mistakes to avoid when building WinUI 3 applications.

## MVVM Violations

### Logic in Code-Behind

**Problem:** Business logic placed directly in XAML code-behind.

```csharp
// Bad: Logic in code-behind
public sealed partial class OrderPage : Page
{
    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CustomerNameTextBox.Text))
        {
            await ShowError("Customer name is required");
            return;
        }

        var order = new Order
        {
            CustomerName = CustomerNameTextBox.Text,
            Total = decimal.Parse(TotalTextBox.Text)
        };

        using var client = new HttpClient();
        await client.PostAsJsonAsync("https://api.example.com/orders", order);
    }
}
```

**Solution:** Move logic to ViewModel with proper commands.

```csharp
// Good: Logic in ViewModel
public partial class OrderViewModel : ObservableObject
{
    private readonly IOrderService _orderService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _customerName = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        await _orderService.CreateOrderAsync(new Order { CustomerName = CustomerName });
    }

    private bool CanSubmit() => !string.IsNullOrEmpty(CustomerName);
}
```

### Manual Property Change Notifications

**Problem:** Writing boilerplate INotifyPropertyChanged code.

```csharp
// Bad: Manual implementation
public class ProductViewModel : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

**Solution:** Use MVVM Toolkit source generators.

```csharp
// Good: MVVM Toolkit
public partial class ProductViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    public string DisplayName => $"Product: {Name}";
}
```

## Binding Issues

### Using Binding Instead of x:Bind

**Problem:** Using traditional `{Binding}` instead of compiled `{x:Bind}`.

```xml
<!-- Bad: Classic binding (runtime, slower) -->
<TextBlock Text="{Binding Path=Title}"/>
<Button Command="{Binding SaveCommand}"/>
```

**Solution:** Use x:Bind for compile-time binding.

```xml
<!-- Good: Compiled binding (faster, type-safe) -->
<TextBlock Text="{x:Bind ViewModel.Title, Mode=OneWay}"/>
<Button Command="{x:Bind ViewModel.SaveCommand}"/>
```

### Missing Mode in x:Bind

**Problem:** Not specifying binding mode when needed.

```xml
<!-- Bad: Default is OneTime, won't update -->
<TextBlock Text="{x:Bind ViewModel.Status}"/>
```

**Solution:** Specify appropriate mode.

```xml
<!-- Good: Updates when property changes -->
<TextBlock Text="{x:Bind ViewModel.Status, Mode=OneWay}"/>

<!-- Good: Two-way for input controls -->
<TextBox Text="{x:Bind ViewModel.Name, Mode=TwoWay}"/>
```

## Threading Issues

### Blocking the UI Thread

**Problem:** Performing synchronous I/O on the UI thread.

```csharp
// Bad: Blocks UI
private void LoadData()
{
    var client = new HttpClient();
    var response = client.GetAsync("https://api.example.com/data").Result;
    var data = response.Content.ReadAsStringAsync().Result;
    ProcessData(data);
}
```

**Solution:** Use async/await properly.

```csharp
// Good: Non-blocking
private async Task LoadDataAsync()
{
    using var client = new HttpClient();
    var response = await client.GetAsync("https://api.example.com/data");
    var data = await response.Content.ReadAsStringAsync();
    ProcessData(data);
}
```

### Updating UI from Background Thread

**Problem:** Modifying UI elements from non-UI thread.

```csharp
// Bad: Direct UI update from background
Task.Run(() =>
{
    var data = LoadExpensiveData();
    StatusTextBlock.Text = "Loaded"; // Crashes or undefined behavior
});
```

**Solution:** Use DispatcherQueue to marshal to UI thread.

```csharp
// Good: Marshal to UI thread
Task.Run(() =>
{
    var data = LoadExpensiveData();
    DispatcherQueue.TryEnqueue(() =>
    {
        StatusTextBlock.Text = "Loaded";
    });
});
```

## Dialog and Picker Issues

### Missing XamlRoot

**Problem:** Not setting XamlRoot on dialogs and pickers.

```csharp
// Bad: Missing XamlRoot
var dialog = new ContentDialog
{
    Title = "Confirm",
    Content = "Are you sure?"
};
await dialog.ShowAsync(); // Throws exception
```

**Solution:** Always set XamlRoot.

```csharp
// Good: XamlRoot set
var dialog = new ContentDialog
{
    Title = "Confirm",
    Content = "Are you sure?",
    XamlRoot = Content.XamlRoot // Or rootElement.XamlRoot
};
await dialog.ShowAsync();
```

### Pickers Without Window Handle

**Problem:** Using file pickers without initializing with window handle.

```csharp
// Bad: Missing initialization
var picker = new FileOpenPicker();
picker.FileTypeFilter.Add(".txt");
var file = await picker.PickSingleFileAsync(); // Fails
```

**Solution:** Initialize picker with window handle.

```csharp
// Good: Properly initialized
var picker = new FileOpenPicker();
var hWnd = WindowNative.GetWindowHandle(App.MainWindow);
InitializeWithWindow.Initialize(picker, hWnd);
picker.FileTypeFilter.Add(".txt");
var file = await picker.PickSingleFileAsync();
```

## Resource and Styling Issues

### Hardcoded Colors and Sizes

**Problem:** Using hardcoded values instead of resources.

```xml
<!-- Bad: Hardcoded values -->
<TextBlock Foreground="#333333" FontSize="14"/>
<Border Background="#0078D4"/>
```

**Solution:** Use theme resources.

```xml
<!-- Good: Theme-aware resources -->
<TextBlock Foreground="{ThemeResource TextFillColorPrimary}"
           Style="{StaticResource BodyTextBlockStyle}"/>
<Border Background="{ThemeResource AccentFillColorDefaultBrush}"/>
```

### Not Supporting Theme Changes

**Problem:** App doesn't respond to system theme changes.

```csharp
// Bad: Fixed theme
rootElement.RequestedTheme = ElementTheme.Light;
```

**Solution:** Support theme switching and system theme.

```csharp
// Good: Respect user/system preference
public void ApplyTheme(ElementTheme theme)
{
    if (Content is FrameworkElement root)
    {
        root.RequestedTheme = theme; // Default follows system
    }
}
```

## List and Collection Issues

### Not Virtualizing Large Lists

**Problem:** Loading all items without virtualization.

```xml
<!-- Bad: No virtualization, loads all items -->
<StackPanel>
    <ItemsControl ItemsSource="{x:Bind ViewModel.LargeCollection}">
        <!-- All items created immediately -->
    </ItemsControl>
</StackPanel>
```

**Solution:** Use virtualizing panels.

```xml
<!-- Good: Virtualized list -->
<ListView ItemsSource="{x:Bind ViewModel.LargeCollection, Mode=OneWay}"
          VirtualizingStackPanel.VirtualizationMode="Recycling"/>
```

### Replacing Entire Collection

**Problem:** Replacing collection instead of updating items.

```csharp
// Bad: Causes full UI refresh
Items = new ObservableCollection<Item>(await _service.GetItemsAsync());
```

**Solution:** Update items incrementally when possible.

```csharp
// Good: Incremental update for better UX
var newItems = await _service.GetItemsAsync();
foreach (var item in newItems.Except(Items))
{
    Items.Add(item);
}
foreach (var item in Items.Except(newItems).ToList())
{
    Items.Remove(item);
}
```

## Navigation Issues

### Tightly Coupled Navigation

**Problem:** Direct Frame access scattered throughout code.

```csharp
// Bad: Direct coupling to Frame
public sealed partial class ProductPage : Page
{
    private void GoToDetails(Product product)
    {
        Frame.Navigate(typeof(ProductDetailPage), product.Id);
    }
}
```

**Solution:** Use a navigation service.

```csharp
// Good: Decoupled via service
public partial class ProductViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [RelayCommand]
    private void GoToDetails(Product product)
    {
        _navigation.NavigateTo<ProductDetailViewModel>(product.Id);
    }
}
```

### Not Handling Back Navigation

**Problem:** Ignoring back navigation and history.

```csharp
// Bad: No back navigation support
```

**Solution:** Handle system back button and navigation history.

```csharp
// Good: Handle back navigation
public MainWindow()
{
    InitializeComponent();

    var navigationView = FindName("NavigationViewControl") as NavigationView;
    navigationView.BackRequested += (s, e) =>
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    };
}
```

## Packaging and Deployment Issues

### Ignoring Packaging Choice Impact

**Problem:** Assuming packaged and unpackaged apps work identically.

```csharp
// Bad: Using packaged-only API in unpackaged app
var localFolder = ApplicationData.Current.LocalFolder; // Throws in unpackaged
```

**Solution:** Check packaging state and use appropriate APIs.

```csharp
// Good: Handle both scenarios
public string GetStorageFolder()
{
    if (IsPackaged())
    {
        return ApplicationData.Current.LocalFolder.Path;
    }
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyApp");
}

private static bool IsPackaged()
{
    try
    {
        return Package.Current.Id != null;
    }
    catch
    {
        return false;
    }
}
```

### Wrong Target Framework

**Problem:** Using incompatible target framework for Windows App SDK.

```xml
<!-- Bad: Missing Windows version -->
<TargetFramework>net8.0</TargetFramework>
```

**Solution:** Use correct Windows target framework.

```xml
<!-- Good: Correct TFM for WinUI 3 -->
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<UseWinUI>true</UseWinUI>
```

## Service and Dependency Issues

### Creating Services in Views

**Problem:** Instantiating services directly in views.

```csharp
// Bad: Tight coupling, hard to test
public sealed partial class OrderPage : Page
{
    private readonly HttpClient _client = new();
    private readonly JsonSerializerOptions _options = new();

    private async void LoadOrders()
    {
        var response = await _client.GetAsync("...");
        // ...
    }
}
```

**Solution:** Inject services via constructor.

```csharp
// Good: Dependency injection
public sealed partial class OrderPage : Page
{
    public OrderViewModel ViewModel { get; }

    public OrderPage()
    {
        ViewModel = App.GetService<OrderViewModel>();
        InitializeComponent();
    }
}
```

### Not Disposing Resources

**Problem:** Not disposing IDisposable resources.

```csharp
// Bad: Resource leak
public async Task DownloadFileAsync(string url)
{
    var client = new HttpClient();
    var stream = await client.GetStreamAsync(url);
    // client never disposed
}
```

**Solution:** Use using statements or patterns.

```csharp
// Good: Proper disposal
public async Task DownloadFileAsync(string url)
{
    using var client = new HttpClient();
    await using var stream = await client.GetStreamAsync(url);
    // ...
}
```

## Windowing Issues

### Not Handling DPI Changes

**Problem:** Fixed pixel sizes that don't scale.

```csharp
// Bad: Fixed pixel size
_appWindow.Resize(new SizeInt32(800, 600));
```

**Solution:** Consider DPI-aware sizing when appropriate.

```csharp
// Good: Consider display scale factor
var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
var scaleFactor = GetScaleFactor(); // Get from DisplayInformation
var width = (int)(800 * scaleFactor);
var height = (int)(600 * scaleFactor);
_appWindow.Resize(new SizeInt32(width, height));
```

### Multiple Window Confusion

**Problem:** Not tracking window instances properly.

```csharp
// Bad: Lost reference to additional windows
private void OpenNewWindow()
{
    var window = new SecondaryWindow();
    window.Activate();
    // Window reference lost, may be GC'd
}
```

**Solution:** Track window instances.

```csharp
// Good: Track windows
private readonly List<Window> _windows = [];

private void OpenNewWindow()
{
    var window = new SecondaryWindow();
    _windows.Add(window);
    window.Closed += (s, e) => _windows.Remove((Window)s);
    window.Activate();
}
```

## Summary Table

| Category | Anti-Pattern | Impact | Solution |
|----------|-------------|--------|----------|
| MVVM | Code-behind logic | Untestable | Use ViewModels |
| Binding | Using {Binding} | Slower, no type safety | Use {x:Bind} |
| Threading | Blocking UI | Frozen app | Use async/await |
| Dialogs | Missing XamlRoot | Runtime crash | Set XamlRoot |
| Styling | Hardcoded values | Poor theming | Use resources |
| Lists | No virtualization | Poor performance | Use ListView |
| Navigation | Tight coupling | Hard to test | Use service |
| Packaging | Wrong TFM | Build failures | Use correct TFM |
| Services | Direct instantiation | Tight coupling | Use DI |
| Windows | Lost references | GC issues | Track instances |
