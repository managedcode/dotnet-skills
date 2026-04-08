# Uno Platform Patterns Reference

## MVUX Patterns

### Basic Model with Feed
```csharp
public partial record ProductsModel(IProductService Products)
{
    public IListFeed<Product> Items => ListFeed.Async(Products.GetAllAsync);
}
```

### Model with State
```csharp
public partial record ProductDetailModel(IProductService Products)
{
    public IState<int> ProductId => State<int>.Empty(this);

    public IFeed<Product> Product => ProductId
        .SelectAsync(async (id, ct) => await Products.GetAsync(id, ct));
}
```

### Model with Commands
```csharp
public partial record OrderModel(IOrderService Orders)
{
    public IState<string> CustomerName => State<string>.Value(this, () => string.Empty);
    public IState<string> ProductId => State<string>.Value(this, () => string.Empty);
    public IState<int> Quantity => State<int>.Value(this, () => 1);

    public async ValueTask SubmitOrder(CancellationToken ct)
    {
        var order = new Order
        {
            CustomerName = await CustomerName,
            ProductId = await ProductId,
            Quantity = await Quantity
        };
        await Orders.CreateAsync(order, ct);
    }
}
```

### Pagination Pattern
```csharp
public partial record PaginatedModel(IProductService Products)
{
    public IState<int> CurrentPage => State<int>.Value(this, () => 0);
    public IState<int> PageSize => State<int>.Value(this, () => 20);

    public IListFeed<Product> Items =>
        CurrentPage.CombineWith(PageSize)
            .SelectAsync(async (state, ct) =>
            {
                var (page, size) = state;
                return await Products.GetPageAsync(page, size, ct);
            })
            .AsListFeed();

    public async ValueTask NextPage()
    {
        await CurrentPage.Update(p => p + 1);
    }

    public async ValueTask PreviousPage()
    {
        await CurrentPage.Update(p => Math.Max(0, p - 1));
    }
}
```

## XAML Patterns

### Responsive Layout
```xml
<Grid>
    <VisualStateManager.VisualStateGroups>
        <VisualStateGroup>
            <VisualState x:Name="Narrow">
                <VisualState.StateTriggers>
                    <AdaptiveTrigger MinWindowWidth="0" />
                </VisualState.StateTriggers>
                <VisualState.Setters>
                    <Setter Target="MainContent.Orientation" Value="Vertical" />
                    <Setter Target="SidePanel.Visibility" Value="Collapsed" />
                </VisualState.Setters>
            </VisualState>
            <VisualState x:Name="Wide">
                <VisualState.StateTriggers>
                    <AdaptiveTrigger MinWindowWidth="800" />
                </VisualState.StateTriggers>
                <VisualState.Setters>
                    <Setter Target="MainContent.Orientation" Value="Horizontal" />
                    <Setter Target="SidePanel.Visibility" Value="Visible" />
                </VisualState.Setters>
            </VisualState>
        </VisualStateGroup>
    </VisualStateManager.VisualStateGroups>

    <StackPanel x:Name="MainContent">
        <Grid x:Name="SidePanel" Width="300" />
        <Grid x:Name="ContentPanel" />
    </StackPanel>
</Grid>
```

### Platform-Specific Styling
```xml
<Style TargetType="Button" x:Key="PlatformButton">
    <Setter Property="Padding" Value="16,8" />

    <!-- Windows-specific -->
    <win:Setter Property="CornerRadius" Value="4" />

    <!-- Android-specific -->
    <android:Setter Property="Background" Value="{StaticResource MaterialPrimary}" />

    <!-- iOS-specific -->
    <ios:Setter Property="Background" Value="{StaticResource iOSBlue}" />
</Style>
```

### FeedView with All States
```xml
<utu:FeedView Source="{Binding Products}">
    <!-- Loading state -->
    <utu:FeedView.ProgressTemplate>
        <DataTemplate>
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <ProgressRing IsActive="True" Width="40" Height="40" />
                <TextBlock Text="Loading..." Margin="0,8,0,0" />
            </StackPanel>
        </DataTemplate>
    </utu:FeedView.ProgressTemplate>

    <!-- Error state -->
    <utu:FeedView.ErrorTemplate>
        <DataTemplate>
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <SymbolIcon Symbol="Warning" />
                <TextBlock Text="{Binding Message}" Margin="0,8,0,0" />
                <Button Content="Retry" Command="{Binding RetryCommand}" />
            </StackPanel>
        </DataTemplate>
    </utu:FeedView.ErrorTemplate>

    <!-- Empty state -->
    <utu:FeedView.NoneTemplate>
        <DataTemplate>
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <SymbolIcon Symbol="List" />
                <TextBlock Text="No items found" Margin="0,8,0,0" />
            </StackPanel>
        </DataTemplate>
    </utu:FeedView.NoneTemplate>

    <!-- Success state -->
    <utu:FeedView.ValueTemplate>
        <DataTemplate>
            <ListView ItemsSource="{Binding}">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="local:Product">
                        <Grid Padding="12">
                            <TextBlock Text="{x:Bind Name}" />
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </DataTemplate>
    </utu:FeedView.ValueTemplate>
</utu:FeedView>
```

## Platform Service Patterns

### Abstracted Platform Service
```csharp
// Shared interface
public interface IPlatformService
{
    string GetPlatformName();
    Task<bool> RequestPermissionAsync(string permission);
    Task ShareAsync(string text, string? title = null);
}

// Shared implementation with partial methods
public partial class PlatformService : IPlatformService
{
    public partial string GetPlatformName();
    public partial Task<bool> RequestPermissionAsync(string permission);
    public partial Task ShareAsync(string text, string? title = null);
}
```

```csharp
// Platforms/Android/PlatformService.cs
public partial class PlatformService
{
    public partial string GetPlatformName() => "Android";

    public async partial Task<bool> RequestPermissionAsync(string permission)
    {
        var status = await Permissions.RequestAsync<Permissions.StorageRead>();
        return status == PermissionStatus.Granted;
    }

    public async partial Task ShareAsync(string text, string? title)
    {
        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraText, text);
        Platform.CurrentActivity.StartActivity(Intent.CreateChooser(intent, title));
    }
}
```

```csharp
// Platforms/iOS/PlatformService.cs
public partial class PlatformService
{
    public partial string GetPlatformName() => "iOS";

    public async partial Task<bool> RequestPermissionAsync(string permission)
    {
        // iOS permission handling
    }

    public async partial Task ShareAsync(string text, string? title)
    {
        var controller = new UIActivityViewController(
            new NSObject[] { new NSString(text) }, null);
        await UIApplication.SharedApplication.KeyWindow
            .RootViewController.PresentViewControllerAsync(controller, true);
    }
}
```

## Navigation Patterns

### Region-Based Navigation
```csharp
// App.xaml.cs
public App()
{
    this.InitializeComponent();

    var builder = this.CreateBuilder(args)
        .Configure(host => host
            .UseNavigation(RegisterRoutes));
}

void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
{
    views.Register(
        new ViewMap<MainPage, MainModel>(),
        new ViewMap<ProductPage, ProductModel>(),
        new ViewMap<SettingsPage, SettingsModel>()
    );

    routes.Register(
        new RouteMap("", View: views.FindByViewModel<MainModel>()),
        new RouteMap("Product", View: views.FindByViewModel<ProductModel>()),
        new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>())
    );
}
```

### Navigation with Parameters
```csharp
public partial record ProductListModel(INavigator Navigator)
{
    public async ValueTask NavigateToProduct(Product product)
    {
        await Navigator.NavigateViewModelAsync<ProductModel>(
            this, data: new { ProductId = product.Id });
    }
}

public partial record ProductModel(INavigator Navigator)
{
    // Receives ProductId from navigation data
    public IState<int> ProductId => State<int>.Value(this, () => 0);
}
```

## Performance Patterns

### Virtualized List
```xml
<ListView ItemsSource="{Binding Items}"
          VirtualizingStackPanel.VirtualizationMode="Recycling"
          VirtualizingStackPanel.IsVirtualizing="True">
    <ListView.ItemsPanel>
        <ItemsPanelTemplate>
            <ItemsStackPanel Orientation="Vertical" />
        </ItemsPanelTemplate>
    </ListView.ItemsPanel>
</ListView>
```

### Deferred Loading
```xml
<Grid>
    <!-- Load expensive content only when visible -->
    <Grid x:Name="ExpensiveContent"
          x:Load="{x:Bind ViewModel.ShowDetails, Mode=OneWay}">
        <local:ExpensiveUserControl />
    </Grid>
</Grid>
```

### Image Optimization
```csharp
// Use appropriate image sizes per platform
public static ImageSource GetOptimizedImage(string basePath)
{
#if __ANDROID__
    var density = Android.Content.Res.Resources.System.DisplayMetrics.Density;
    var suffix = density switch
    {
        < 1.5f => "mdpi",
        < 2.0f => "hdpi",
        < 3.0f => "xhdpi",
        _ => "xxhdpi"
    };
    return ImageSource.FromFile($"{basePath}_{suffix}.png");
#elif __IOS__
    return ImageSource.FromFile($"{basePath}@2x.png");
#else
    return ImageSource.FromFile($"{basePath}.png");
#endif
}
```

## Theme Patterns

### Light/Dark Theme Support
```csharp
public class ThemeService : IThemeService
{
    public void SetTheme(AppTheme theme)
    {
        var resources = Application.Current.Resources;
        var mergedDictionaries = resources.MergedDictionaries;

        mergedDictionaries.Clear();
        mergedDictionaries.Add(theme switch
        {
            AppTheme.Light => new LightTheme(),
            AppTheme.Dark => new DarkTheme(),
            _ => new SystemTheme()
        });
    }
}
```

```xml
<!-- LightTheme.xaml -->
<ResourceDictionary>
    <Color x:Key="BackgroundColor">#FFFFFF</Color>
    <Color x:Key="TextColor">#000000</Color>
    <Color x:Key="AccentColor">#0078D4</Color>
</ResourceDictionary>

<!-- DarkTheme.xaml -->
<ResourceDictionary>
    <Color x:Key="BackgroundColor">#1E1E1E</Color>
    <Color x:Key="TextColor">#FFFFFF</Color>
    <Color x:Key="AccentColor">#0078D4</Color>
</ResourceDictionary>
```
