---
name: wpf
description: "Build and modernize WPF applications on .NET with correct XAML, data binding, commands, threading, styling, and Windows desktop migration decisions."
compatibility: "Requires a WPF project on .NET or .NET Framework."
---

# WPF

## Trigger On

- working on WPF UI, MVVM, binding, commands, or desktop modernization
- migrating WPF from .NET Framework to .NET
- integrating newer Windows capabilities into a WPF app
- implementing data binding, styles, templates, or control customization

## Documentation

- [WPF Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Data Binding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/)
- [MVVM Toolkit Introduction](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [Styles and Templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
- [Migration Guide](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/migration/)

### References

- [patterns.md](references/patterns.md) - MVVM patterns, binding patterns, command patterns, and reusable architectural approaches
- [anti-patterns.md](references/anti-patterns.md) - Common WPF mistakes and how to avoid them

## Workflow

1. **Confirm Windows-only scope** — WPF is Windows-only even when the wider .NET stack is cross-platform
2. **Apply MVVM pattern** — keep views dumb, logic in ViewModels, use commands
3. **Manage data binding explicitly** — choose correct binding modes, validate at runtime
4. **Use styles and templates deliberately** — keep UI composable, avoid page-specific hacks
5. **Handle threading correctly** — use Dispatcher for UI updates, async/await for long operations
6. **Validate both designer and runtime** — XAML composition failures often surface only at runtime

## Project Structure

```
MyWpfApp/
├── MyWpfApp/
│   ├── App.xaml                # Application entry
│   ├── MainWindow.xaml         # Main window
│   ├── Views/                  # XAML views/windows
│   ├── ViewModels/             # MVVM ViewModels
│   ├── Models/                 # Domain models
│   ├── Services/               # Business logic
│   ├── Converters/             # Value converters
│   ├── Resources/              # Styles, templates, dictionaries
│   └── Controls/               # Custom controls
└── MyWpfApp.Tests/
```

## MVVM Pattern

### ViewModel with MVVM Toolkit
```csharp
public partial class CustomersViewModel : ObservableObject
{
    private readonly ICustomerService _customerService;

    [ObservableProperty]
    private ObservableCollection<Customer> _customers = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;

    public CustomersViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _customerService.GetAllAsync();
            Customers = new ObservableCollection<Customer>(items);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRefresh() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedCustomer is null) return;
        await _customerService.SaveAsync(SelectedCustomer);
    }

    private bool CanSave() => SelectedCustomer is not null;
}
```

### View Binding
```xml
<Window x:Class="MyWpfApp.Views.CustomersView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:MyWpfApp.ViewModels"
        d:DataContext="{d:DesignInstance Type=vm:CustomersViewModel}">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <ToolBar Grid.Row="0">
            <Button Content="Refresh"
                    Command="{Binding RefreshCommand}"/>
            <Button Content="Save"
                    Command="{Binding SaveCommand}"/>
        </ToolBar>

        <DataGrid Grid.Row="1"
                  ItemsSource="{Binding Customers}"
                  SelectedItem="{Binding SelectedCustomer}"
                  AutoGenerateColumns="False">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Name"
                                    Binding="{Binding Name}"/>
                <DataGridTextColumn Header="Email"
                                    Binding="{Binding Email}"/>
            </DataGrid.Columns>
        </DataGrid>
    </Grid>
</Window>
```

## Dependency Injection

```csharp
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Services
                services.AddSingleton<ICustomerService, CustomerService>();
                services.AddSingleton<INavigationService, NavigationService>();

                // ViewModels
                services.AddTransient<CustomersViewModel>();
                services.AddTransient<CustomerDetailViewModel>();

                // Views
                services.AddTransient<MainWindow>();
                services.AddTransient<CustomersView>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
```

## Data Binding Modes

```xml
<!-- OneTime: Read once at initialization -->
<TextBlock Text="{Binding CreatedDate, Mode=OneTime}"/>

<!-- OneWay: Source to target only (default for most properties) -->
<TextBlock Text="{Binding Name, Mode=OneWay}"/>

<!-- TwoWay: Bidirectional synchronization -->
<TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

<!-- OneWayToSource: Target to source only -->
<TextBox Text="{Binding SearchFilter, Mode=OneWayToSource}"/>
```

## Value Converters

```csharp
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

// Multi-value converter
public class MultiplyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double a && values[1] is double b)
        {
            return a * b;
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

## Styles and Templates

### Resource Dictionary
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Implicit style for all Buttons -->
    <Style TargetType="Button">
        <Setter Property="Padding" Value="10,5"/>
        <Setter Property="Margin" Value="5"/>
        <Setter Property="Background" Value="#0078D4"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}"
                            CornerRadius="4"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center"
                                          VerticalAlignment="Center"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Named style -->
    <Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="#D32F2F"/>
    </Style>
</ResourceDictionary>
```

## Threading and Dispatcher

```csharp
// Update UI from background thread
await Task.Run(async () =>
{
    var data = await LoadDataAsync();

    // Must use Dispatcher to update UI
    Application.Current.Dispatcher.Invoke(() =>
    {
        Items.Clear();
        foreach (var item in data)
        {
            Items.Add(item);
        }
    });
});

// Better: Use async/await properly
private async Task LoadDataAsync()
{
    IsLoading = true;
    try
    {
        // This runs on background thread
        var data = await _service.GetDataAsync();

        // This automatically marshals to UI thread
        Items = new ObservableCollection<Item>(data);
    }
    finally
    {
        IsLoading = false;
    }
}
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| Logic in code-behind | Hard to test, tight coupling | Use MVVM with ViewModels |
| Synchronous blocking calls | UI freezes | Use async/await |
| Manual INotifyPropertyChanged | Boilerplate, error-prone | Use MVVM Toolkit attributes |
| Hardcoded colors/sizes | Inconsistent, hard to theme | Use resource dictionaries |
| Direct Dispatcher.Invoke everywhere | Complex, error-prone | Prefer async/await marshaling |
| God ViewModel | Unmaintainable | Split into focused ViewModels |
| Skipping binding validation | Runtime errors hidden | Use ValidatesOnDataErrors |
| Event handlers for everything | Memory leaks, coupling | Use commands and bindings |

## Best Practices

1. **Use compiled bindings in .NET 5+:**
   - Enable `x:CompileBindings="True"` for performance

2. **Implement INotifyDataErrorInfo for validation:**
   ```csharp
   [ObservableProperty]
   [NotifyDataErrorInfo]
   [Required(ErrorMessage = "Name is required")]
   [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
   private string _name = string.Empty;
   ```

3. **Use weak event patterns for long-lived subscriptions:**
   ```csharp
   WeakEventManager<Source, EventArgs>.AddHandler(source, "EventName", Handler);
   ```

4. **Virtualize large collections:**
   ```xml
   <ListBox VirtualizingPanel.IsVirtualizing="True"
            VirtualizingPanel.VirtualizationMode="Recycling"
            ItemsSource="{Binding LargeCollection}"/>
   ```

5. **Freeze Freezables when possible:**
   ```csharp
   var brush = new SolidColorBrush(Colors.Blue);
   brush.Freeze(); // Thread-safe, better performance
   ```

6. **Use design-time data:**
   ```xml
   <Window d:DataContext="{d:DesignInstance Type=vm:MainViewModel, IsDesignTimeCreatable=True}">
   ```

## Testing

```csharp
[Fact]
public async Task RefreshCommand_LoadsCustomers()
{
    var mockService = new Mock<ICustomerService>();
    mockService.Setup(s => s.GetAllAsync())
        .ReturnsAsync(new[] { new Customer { Name = "Test" } });

    var viewModel = new CustomersViewModel(mockService.Object);

    await viewModel.RefreshCommand.ExecuteAsync(null);

    Assert.Single(viewModel.Customers);
    Assert.Equal("Test", viewModel.Customers[0].Name);
}

[Fact]
public void SaveCommand_CannotExecute_WhenNoSelection()
{
    var mockService = new Mock<ICustomerService>();
    var viewModel = new CustomersViewModel(mockService.Object);

    viewModel.SelectedCustomer = null;

    Assert.False(viewModel.SaveCommand.CanExecute(null));
}
```

## Deliver

- cleaner WPF views and view-model boundaries
- safer binding and threading behavior
- migration guidance grounded in actual Windows constraints
- MVVM pattern with testable ViewModels

## Validate

- binding and command flows are explicit
- code-behind is not carrying hidden business logic
- Windows-only assumptions are acknowledged
- threading and dispatcher usage is correct
- styles and resources are properly organized
