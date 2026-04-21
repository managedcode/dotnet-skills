---
name: mvvm
description: "Implement the Model-View-ViewModel pattern in .NET applications with proper separation of concerns, data binding, commands, and testable ViewModels using MVVM Toolkit."
compatibility: "Applies to WPF, MAUI, WinUI, Uno Platform, Avalonia, and Blazor projects."
---

# MVVM Pattern for .NET

## Trigger On

- implementing UI separation with Model-View-ViewModel
- using MVVM Toolkit (CommunityToolkit.Mvvm) for ViewModels
- designing testable UI architecture
- handling commands, property changes, and messaging
- choosing between MVVM frameworks

## Documentation

- [MVVM Toolkit Overview](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [ObservableObject](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/observableobject)
- [RelayCommand](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/relaycommand)
- [Messenger](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger)
- [Source Generators](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview)

## References

See detailed examples in the `references/` folder:
- [`patterns.md`](references/patterns.md) — ViewModel, command, navigation, and state patterns
- [`anti-patterns.md`](references/anti-patterns.md) — Common mistakes and how to fix them

## Core Concepts

| Component | Responsibility | Example |
|-----------|---------------|---------|
| **Model** | Business logic and data | `Product`, `Order`, `User` |
| **View** | UI presentation (XAML/Razor) | `ProductPage.xaml` |
| **ViewModel** | UI logic and state | `ProductViewModel` |

## Workflow

1. **Keep Views dumb** — no business logic in code-behind
2. **Use data binding** — connect View to ViewModel properties
3. **Commands for actions** — handle user interactions via ICommand
4. **Inject dependencies** — services go into ViewModel constructors
5. **Test ViewModels** — they should be unit testable without UI

## MVVM Toolkit Setup

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

## ViewModel Patterns

### Basic ViewModel with Source Generators
```csharp
public partial class ProductViewModel(IProductService productService) : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValid;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await productService.SaveAsync(new Product { Name = Name, Price = Price });
    }

    private bool CanSave() => IsValid && !string.IsNullOrEmpty(Name);
}
```

### Property Changed Notifications
```csharp
public partial class OrderViewModel : ObservableObject
{
    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private decimal _unitPrice;

    // Computed property - manually notify
    public decimal Total => Quantity * UnitPrice;

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Total));
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(Total));
    }
}
```

### Collection ViewModel
```csharp
public partial class ProductListViewModel(IProductService productService) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ProductViewModel> _products = [];

    [ObservableProperty]
    private ProductViewModel? _selectedProduct;

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        try
        {
            var items = await productService.GetAllAsync();
            Products = new ObservableCollection<ProductViewModel>(
                items.Select(p => new ProductViewModel(productService)
                {
                    Name = p.Name,
                    Price = p.Price
                }));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void DeleteProduct(ProductViewModel product)
    {
        Products.Remove(product);
    }
}
```

## Commands

### Async Commands with Cancellation
```csharp
public partial class SearchViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SearchAsync(CancellationToken token)
    {
        await Task.Delay(500, token); // Debounce
        // Search logic with cancellation support
    }
}
```

### Command with Parameter
```csharp
public partial class NavigationViewModel : ObservableObject
{
    [RelayCommand]
    private void NavigateTo(string page)
    {
        // Navigate to page
    }

    [RelayCommand]
    private async Task OpenItemAsync(int itemId)
    {
        // Load and open item
    }
}
```

## Messenger Pattern

### Sending Messages
```csharp
// Define message
public record ProductSelectedMessage(Product Product);

// Send from one ViewModel
WeakReferenceMessenger.Default.Send(new ProductSelectedMessage(selectedProduct));
```

### Receiving Messages
```csharp
public partial class ProductDetailViewModel : ObservableRecipient
{
    public ProductDetailViewModel()
    {
        IsActive = true; // Enable message reception
    }

    protected override void OnActivated()
    {
        Messenger.Register<ProductDetailViewModel, ProductSelectedMessage>(
            this, (r, m) => r.LoadProduct(m.Product));
    }

    private void LoadProduct(Product product)
    {
        // Update UI with product details
    }
}
```

## Validation

### Using ObservableValidator
```csharp
public partial class RegistrationViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    private string _password = string.Empty;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        // Registration logic
    }

    private bool CanRegister() => !HasErrors;
}
```

## Dependency Injection

### Registration
```csharp
// Services
services.AddSingleton<IProductService, ProductService>();
services.AddSingleton<INavigationService, NavigationService>();

// ViewModels
services.AddTransient<ProductListViewModel>();
services.AddTransient<ProductDetailViewModel>();

// Views (for View-first navigation)
services.AddTransient<ProductListPage>();
services.AddTransient<ProductDetailPage>();
```

### ViewModel Locator Pattern
```csharp
public class ViewModelLocator
{
    private static IServiceProvider _provider = null!;

    public static void Initialize(IServiceProvider provider) => _provider = provider;

    public ProductListViewModel ProductList => _provider.GetRequiredService<ProductListViewModel>();
    public ProductDetailViewModel ProductDetail => _provider.GetRequiredService<ProductDetailViewModel>();
}
```

## View Binding

### XAML Binding
```xml
<Page x:Class="MyApp.Views.ProductListPage"
      xmlns:vm="using:MyApp.ViewModels"
      x:DataType="vm:ProductListViewModel">

    <Grid>
        <ProgressRing IsActive="{x:Bind ViewModel.IsLoading, Mode=OneWay}"
                      Visibility="{x:Bind ViewModel.IsLoading, Mode=OneWay}" />

        <ListView ItemsSource="{x:Bind ViewModel.Products, Mode=OneWay}"
                  SelectedItem="{x:Bind ViewModel.SelectedProduct, Mode=TwoWay}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:ProductViewModel">
                    <StackPanel>
                        <TextBlock Text="{x:Bind Name, Mode=OneWay}" />
                        <TextBlock Text="{x:Bind Price, Mode=OneWay}" />
                    </StackPanel>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <Button Content="Load"
                Command="{x:Bind ViewModel.LoadProductsCommand}" />
    </Grid>
</Page>
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Logic in code-behind | Not testable | Move to ViewModel |
| ViewModel knows View | Tight coupling | Use interfaces/messaging |
| Manual INotifyPropertyChanged | Verbose, error-prone | Use source generators |
| God ViewModel | Unmaintainable | Split responsibilities |
| Direct service calls in View | Violates separation | Go through ViewModel |
| Exposing Model directly | Leaks implementation | Create ViewModel properties |

## Testing ViewModels

```csharp
public class ProductViewModelTests
{
    [Fact]
    public async Task LoadProducts_PopulatesCollection()
    {
        // Arrange
        var mockService = new Mock<IProductService>();
        mockService.Setup(s => s.GetAllAsync())
            .ReturnsAsync([new Product { Name = "Test", Price = 10 }]);

        var viewModel = new ProductListViewModel(mockService.Object);

        // Act
        await viewModel.LoadProductsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(viewModel.Products);
        Assert.Equal("Test", viewModel.Products[0].Name);
    }

    [Fact]
    public void SaveCommand_CannotExecute_WhenInvalid()
    {
        var viewModel = new ProductViewModel(Mock.Of<IProductService>())
        {
            Name = "",
            IsValid = false
        };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }
}
```

## Framework Comparison

| Feature | MVVM Toolkit | Prism | MVVMLight |
|---------|--------------|-------|-----------|
| Source generators | Yes | No | No |
| Maintenance | Active | Active | Deprecated |
| DI built-in | No | Yes | No |
| Navigation | No | Yes | No |
| Weight | Light | Heavy | Light |

## Deliver

- ViewModels that are fully unit testable
- Clean separation between UI and business logic
- Proper use of commands and data binding
- Messaging for loose coupling between components

## Validate

- No business logic in code-behind files
- ViewModels don't reference View types
- Commands are used for all user actions
- Properties use ObservableProperty or equivalent
- Dependencies are injected, not created
- Unit tests cover ViewModel logic
