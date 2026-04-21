# MAUI Patterns

## Shell Navigation

### Hierarchical Navigation

```csharp
// AppShell.xaml
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:views="clr-namespace:MyApp.Views"
       x:Class="MyApp.AppShell">

    <FlyoutItem Title="Home" Icon="home.png">
        <ShellContent ContentTemplate="{DataTemplate views:HomePage}" />
    </FlyoutItem>

    <FlyoutItem Title="Products" Icon="products.png">
        <Tab Title="All">
            <ShellContent ContentTemplate="{DataTemplate views:ProductsPage}" />
        </Tab>
        <Tab Title="Favorites">
            <ShellContent ContentTemplate="{DataTemplate views:FavoritesPage}" />
        </Tab>
    </FlyoutItem>

    <FlyoutItem Title="Settings" Icon="settings.png">
        <ShellContent ContentTemplate="{DataTemplate views:SettingsPage}" />
    </FlyoutItem>
</Shell>
```

### Route Registration and Navigation

```csharp
// Register routes in AppShell.xaml.cs
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register detail pages not in visual hierarchy
        Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));
        Routing.RegisterRoute(nameof(OrderDetailPage), typeof(OrderDetailPage));
        Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));
    }
}
```

### Navigation with Complex Parameters

```csharp
// Pass complex objects using Dictionary
public async Task NavigateToProductDetail(Product product)
{
    var parameters = new Dictionary<string, object>
    {
        { "Product", product },
        { "Source", "ProductList" }
    };
    await Shell.Current.GoToAsync(nameof(ProductDetailPage), parameters);
}

// Receive complex parameters
[QueryProperty(nameof(Product), "Product")]
[QueryProperty(nameof(Source), "Source")]
public partial class ProductDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private Product _product;

    [ObservableProperty]
    private string _source;

    partial void OnProductChanged(Product value)
    {
        // Initialize view with product data
        LoadProductDetails(value);
    }
}
```

### Navigation with URI-Style Routes

```csharp
// Absolute navigation (replaces stack)
await Shell.Current.GoToAsync("//home/products");

// Relative navigation (pushes onto stack)
await Shell.Current.GoToAsync("productDetail");

// Navigate back
await Shell.Current.GoToAsync("..");

// Navigate back multiple levels
await Shell.Current.GoToAsync("../..");

// Navigate back to root
await Shell.Current.GoToAsync("//");
```

### Back Button Handling

```csharp
public partial class CheckoutPage : ContentPage
{
    protected override bool OnBackButtonPressed()
    {
        // Show confirmation dialog
        Dispatcher.Dispatch(async () =>
        {
            bool answer = await DisplayAlert(
                "Leave Checkout?",
                "Your cart will be saved.",
                "Leave", "Stay");

            if (answer)
            {
                await Shell.Current.GoToAsync("..");
            }
        });

        return true; // Prevent default back navigation
    }
}
```

## Platform-Specific Code Patterns

### Handler Customization

```csharp
// Customize Entry handler for all platforms
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            "CustomEntry", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
            });

        return builder.Build();
    }
}
```

### Platform-Specific Services with DI

```csharp
// Shared interface
public interface INotificationService
{
    Task<bool> RequestPermissionAsync();
    Task ShowLocalNotificationAsync(string title, string message);
}

// Platform implementation registration
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

#if ANDROID
        builder.Services.AddSingleton<INotificationService, AndroidNotificationService>();
#elif IOS
        builder.Services.AddSingleton<INotificationService, iOSNotificationService>();
#elif WINDOWS
        builder.Services.AddSingleton<INotificationService, WindowsNotificationService>();
#endif

        return builder.Build();
    }
}
```

### Multi-Targeting with Partial Classes

```csharp
// Services/BiometricService.cs (shared definition)
public partial class BiometricService : IBiometricService
{
    public partial Task<bool> AuthenticateAsync(string reason);
    public partial bool IsAvailable { get; }
}

// Platforms/Android/BiometricService.cs
public partial class BiometricService
{
    public partial bool IsAvailable =>
        BiometricManager.From(Platform.CurrentActivity)
            .CanAuthenticate(BiometricManager.Authenticators.BiometricStrong)
            == BiometricManager.BiometricSuccess;

    public partial async Task<bool> AuthenticateAsync(string reason)
    {
        var executor = ContextCompat.GetMainExecutor(Platform.CurrentActivity);
        var callback = new BiometricCallback();

        var promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Authenticate")
            .SetSubtitle(reason)
            .SetNegativeButtonText("Cancel")
            .Build();

        var biometricPrompt = new BiometricPrompt(
            Platform.CurrentActivity as FragmentActivity,
            executor,
            callback);

        biometricPrompt.Authenticate(promptInfo);
        return await callback.Task;
    }
}

// Platforms/iOS/BiometricService.cs
public partial class BiometricService
{
    private readonly LAContext _context = new();

    public partial bool IsAvailable =>
        _context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);

    public partial async Task<bool> AuthenticateAsync(string reason)
    {
        var (success, _) = await _context.EvaluatePolicyAsync(
            LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
            reason);
        return success;
    }
}
```

### OnPlatform and OnIdiom in XAML

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- Platform-specific values -->
    <ContentPage.Padding>
        <OnPlatform x:TypeArguments="Thickness">
            <On Platform="iOS" Value="0,20,0,0" />
            <On Platform="Android" Value="0" />
            <On Platform="WinUI" Value="10" />
        </OnPlatform>
    </ContentPage.Padding>

    <!-- Device idiom-specific values -->
    <Label Text="Welcome">
        <Label.FontSize>
            <OnIdiom x:TypeArguments="x:Double">
                <OnIdiom.Phone>16</OnIdiom.Phone>
                <OnIdiom.Tablet>24</OnIdiom.Tablet>
                <OnIdiom.Desktop>20</OnIdiom.Desktop>
            </OnIdiom>
        </Label.FontSize>
    </Label>

    <!-- Combined platform and idiom -->
    <Grid>
        <Grid.ColumnDefinitions>
            <OnIdiom x:TypeArguments="ColumnDefinitionCollection">
                <OnIdiom.Phone>
                    <ColumnDefinition Width="*" />
                </OnIdiom.Phone>
                <OnIdiom.Tablet>
                    <ColumnDefinition Width="300" />
                    <ColumnDefinition Width="*" />
                </OnIdiom.Tablet>
            </OnIdiom>
        </Grid.ColumnDefinitions>
    </Grid>
</ContentPage>
```

## Messaging Patterns

### WeakReferenceMessenger

```csharp
// Define message types
public record ProductAddedMessage(Product Product);
public record CartUpdatedMessage(int ItemCount);
public record UserLoggedInMessage(User User);

// Subscribe in ViewModel constructor
public partial class CartViewModel : ObservableObject
{
    public CartViewModel()
    {
        WeakReferenceMessenger.Default.Register<ProductAddedMessage>(this, (r, m) =>
        {
            // Handle product added
            AddToCart(m.Product);
        });
    }

    // Clean up when ViewModel is disposed
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}

// Send message from another ViewModel
public partial class ProductDetailViewModel : ObservableObject
{
    [RelayCommand]
    private void AddToCart()
    {
        WeakReferenceMessenger.Default.Send(new ProductAddedMessage(CurrentProduct));
    }
}
```

### Request/Response Messages

```csharp
// Define request message
public class CartCountRequestMessage : RequestMessage<int> { }

// Register handler
WeakReferenceMessenger.Default.Register<CartViewModel, CartCountRequestMessage>(
    this, (r, m) => m.Reply(r.Items.Count));

// Send request and get response
var count = WeakReferenceMessenger.Default.Send<CartCountRequestMessage>();
if (count.HasReceivedResponse)
{
    UpdateBadge(count.Response);
}
```

## Lifecycle Patterns

### Application Lifecycle

```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        // App started or returned from background (cold start)
        Analytics.TrackEvent("app_started");
    }

    protected override void OnSleep()
    {
        // App going to background
        SaveAppState();
    }

    protected override void OnResume()
    {
        // App returning from background (warm start)
        RefreshTokenIfNeeded();
    }
}
```

### Page Lifecycle

```csharp
public partial class ProductsPage : ContentPage
{
    private readonly ProductsViewModel _viewModel;

    public ProductsPage(ProductsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Load data when page appears
        _viewModel.LoadProductsCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Clean up resources
        _viewModel.CancelPendingOperations();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // Page is now the active page
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // Page is no longer active
    }
}
```

## Data Binding Patterns

### Value Converters

```csharp
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Green;
    public Color FalseColor { get; set; } = Colors.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? TrueColor : FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Usage in XAML
<ContentPage.Resources>
    <converters:BoolToColorConverter x:Key="BoolToColor"
                                     TrueColor="Green"
                                     FalseColor="Gray" />
</ContentPage.Resources>

<Label Text="{Binding Status}"
       TextColor="{Binding IsActive, Converter={StaticResource BoolToColor}}" />
```

### Multi-Binding

```csharp
public class FullNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is string firstName &&
            values[1] is string lastName)
        {
            return $"{firstName} {lastName}";
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Usage in XAML
<Label>
    <Label.Text>
        <MultiBinding Converter="{StaticResource FullNameConverter}">
            <Binding Path="FirstName" />
            <Binding Path="LastName" />
        </MultiBinding>
    </Label.Text>
</Label>
```

### Behaviors

```csharp
public class NumericValidationBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        entry.TextChanged += OnTextChanged;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(entry);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry)
        {
            bool isValid = double.TryParse(e.NewTextValue, out _);
            entry.TextColor = isValid ? Colors.Black : Colors.Red;
        }
    }
}

// Usage in XAML
<Entry Placeholder="Enter amount">
    <Entry.Behaviors>
        <behaviors:NumericValidationBehavior />
    </Entry.Behaviors>
</Entry>
```

## Resource and Theme Patterns

### Dynamic Resources and Themes

```csharp
// App.xaml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
            <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>

        <!-- Theme-aware colors -->
        <Color x:Key="PrimaryColor">
            <AppThemeBinding Light="#512BD4" Dark="#B39DDB" />
        </Color>
        <Color x:Key="BackgroundColor">
            <AppThemeBinding Light="White" Dark="#1E1E1E" />
        </Color>
    </ResourceDictionary>
</Application.Resources>

// Programmatic theme switching
public void SetTheme(AppTheme theme)
{
    Application.Current.UserAppTheme = theme; // Light, Dark, or Unspecified
}
```

### Style Inheritance

```xml
<Style x:Key="BaseButtonStyle" TargetType="Button">
    <Setter Property="FontSize" Value="16" />
    <Setter Property="Padding" Value="16,10" />
    <Setter Property="CornerRadius" Value="8" />
</Style>

<Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource BaseButtonStyle}">
    <Setter Property="BackgroundColor" Value="{DynamicResource PrimaryColor}" />
    <Setter Property="TextColor" Value="White" />
</Style>

<Style x:Key="SecondaryButtonStyle" TargetType="Button" BasedOn="{StaticResource BaseButtonStyle}">
    <Setter Property="BackgroundColor" Value="Transparent" />
    <Setter Property="TextColor" Value="{DynamicResource PrimaryColor}" />
    <Setter Property="BorderColor" Value="{DynamicResource PrimaryColor}" />
    <Setter Property="BorderWidth" Value="1" />
</Style>
```

## CollectionView Patterns

### Grouping

```csharp
// ViewModel
public ObservableCollection<ProductGroup> GroupedProducts { get; } = new();

public class ProductGroup : ObservableCollection<Product>
{
    public string Category { get; }
    public ProductGroup(string category, IEnumerable<Product> products) : base(products)
    {
        Category = category;
    }
}
```

```xml
<CollectionView ItemsSource="{Binding GroupedProducts}"
                IsGrouped="True">
    <CollectionView.GroupHeaderTemplate>
        <DataTemplate x:DataType="vm:ProductGroup">
            <Label Text="{Binding Category}"
                   FontAttributes="Bold"
                   FontSize="18"
                   BackgroundColor="LightGray"
                   Padding="10" />
        </DataTemplate>
    </CollectionView.GroupHeaderTemplate>
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="models:Product">
            <VerticalStackLayout Padding="10">
                <Label Text="{Binding Name}" />
            </VerticalStackLayout>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### Selection and Commands

```xml
<CollectionView ItemsSource="{Binding Products}"
                SelectionMode="Single"
                SelectedItem="{Binding SelectedProduct}"
                SelectionChangedCommand="{Binding ProductSelectedCommand}">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="models:Product">
            <SwipeView>
                <SwipeView.RightItems>
                    <SwipeItems>
                        <SwipeItem Text="Delete"
                                   BackgroundColor="Red"
                                   Command="{Binding Source={RelativeSource AncestorType={x:Type vm:ProductsViewModel}}, Path=DeleteProductCommand}"
                                   CommandParameter="{Binding}" />
                    </SwipeItems>
                </SwipeView.RightItems>
                <Grid Padding="10">
                    <Label Text="{Binding Name}" />
                </Grid>
            </SwipeView>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```
