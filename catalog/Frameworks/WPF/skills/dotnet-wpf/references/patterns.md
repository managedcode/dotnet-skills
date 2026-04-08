# WPF Patterns Reference

## MVVM Patterns

### Basic MVVM Structure

The Model-View-ViewModel pattern separates concerns:

- **Model**: Business logic and data
- **View**: XAML UI, no business logic
- **ViewModel**: Presentation logic, exposes data and commands to the View

```
View (XAML) ←→ ViewModel (C#) ←→ Model (C#)
     ↑              ↑
  DataBinding    Services/Repositories
```

### ViewModel Base Class with MVVM Toolkit

```csharp
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    protected async Task ExecuteBusyActionAsync(Func<Task> action)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### Navigation ViewModel Pattern

```csharp
public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    void NavigateTo<TViewModel>(object parameter) where TViewModel : ObservableObject;
    void GoBack();
    bool CanGoBack { get; }
}

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [RelayCommand]
    private void NavigateToCustomers()
    {
        _navigation.NavigateTo<CustomersViewModel>();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigation.NavigateTo<SettingsViewModel>();
    }
}
```

### Messenger Pattern for Decoupled Communication

```csharp
// Message definition
public sealed record CustomerSelectedMessage(Customer Customer);

// Sender
public partial class CustomerListViewModel : ObservableObject
{
    [RelayCommand]
    private void SelectCustomer(Customer customer)
    {
        WeakReferenceMessenger.Default.Send(new CustomerSelectedMessage(customer));
    }
}

// Receiver
public partial class CustomerDetailViewModel : ObservableObject, IRecipient<CustomerSelectedMessage>
{
    public CustomerDetailViewModel()
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(CustomerSelectedMessage message)
    {
        CurrentCustomer = message.Customer;
    }

    [ObservableProperty]
    private Customer? _currentCustomer;
}
```

### Dialog Service Pattern

```csharp
public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task<string?> ShowInputAsync(string title, string prompt);
    Task<T?> ShowDialogAsync<T>(object viewModel) where T : class;
}

public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return await Task.FromResult(result == MessageBoxResult.Yes);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        await Task.CompletedTask;
    }

    public async Task<string?> ShowInputAsync(string title, string prompt)
    {
        var dialog = new InputDialog(title, prompt);
        return dialog.ShowDialog() == true
            ? await Task.FromResult(dialog.InputText)
            : null;
    }

    public async Task<T?> ShowDialogAsync<T>(object viewModel) where T : class
    {
        // Implementation for custom dialogs with ViewModels
        throw new NotImplementedException();
    }
}
```

## Binding Patterns

### Property Change Notification

```csharp
// Using MVVM Toolkit source generators
public partial class PersonViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string _lastName = string.Empty;

    public string FullName => $"{FirstName} {LastName}";
}
```

### Collection Binding with Filtering

```csharp
public partial class FilterableListViewModel : ObservableObject
{
    private readonly ObservableCollection<Item> _allItems = [];

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ICollectionView ItemsView { get; }

    public FilterableListViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(_allItems);
        ItemsView.Filter = FilterItems;
    }

    partial void OnFilterTextChanged(string value)
    {
        ItemsView.Refresh();
    }

    private bool FilterItems(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        if (obj is Item item)
        {
            return item.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
```

### Master-Detail Binding

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="300"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Master List -->
    <ListBox Grid.Column="0"
             ItemsSource="{Binding Items}"
             SelectedItem="{Binding SelectedItem}"
             DisplayMemberPath="Name"/>

    <!-- Detail View -->
    <ContentControl Grid.Column="1"
                    Content="{Binding SelectedItem}">
        <ContentControl.ContentTemplate>
            <DataTemplate>
                <StackPanel Margin="10">
                    <TextBlock Text="{Binding Name}" FontSize="24"/>
                    <TextBlock Text="{Binding Description}" TextWrapping="Wrap"/>
                </StackPanel>
            </DataTemplate>
        </ContentControl.ContentTemplate>
    </ContentControl>
</Grid>
```

### Binding to Nested Properties

```xml
<!-- Direct nested binding -->
<TextBlock Text="{Binding Customer.Address.City}"/>

<!-- With fallback for null -->
<TextBlock Text="{Binding Customer.Address.City, FallbackValue='N/A', TargetNullValue='Not set'}"/>

<!-- With string format -->
<TextBlock Text="{Binding Order.Total, StringFormat='{}{0:C}'}"/>

<!-- With multi-binding -->
<TextBlock>
    <TextBlock.Text>
        <MultiBinding StringFormat="{}{0}, {1}">
            <Binding Path="Customer.LastName"/>
            <Binding Path="Customer.FirstName"/>
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>
```

### DataTemplate Selector Pattern

```csharp
public class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextMessageTemplate { get; set; }
    public DataTemplate? ImageMessageTemplate { get; set; }
    public DataTemplate? SystemMessageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            TextMessage => TextMessageTemplate,
            ImageMessage => ImageMessageTemplate,
            SystemMessage => SystemMessageTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
```

```xml
<Window.Resources>
    <DataTemplate x:Key="TextMessageTemplate">
        <TextBlock Text="{Binding Content}"/>
    </DataTemplate>
    <DataTemplate x:Key="ImageMessageTemplate">
        <Image Source="{Binding ImageUrl}"/>
    </DataTemplate>
    <DataTemplate x:Key="SystemMessageTemplate">
        <TextBlock Text="{Binding Content}" FontStyle="Italic"/>
    </DataTemplate>

    <local:MessageTemplateSelector x:Key="MessageSelector"
        TextMessageTemplate="{StaticResource TextMessageTemplate}"
        ImageMessageTemplate="{StaticResource ImageMessageTemplate}"
        SystemMessageTemplate="{StaticResource SystemMessageTemplate}"/>
</Window.Resources>

<ItemsControl ItemsSource="{Binding Messages}"
              ItemTemplateSelector="{StaticResource MessageSelector}"/>
```

## Command Patterns

### Async Commands with MVVM Toolkit

```csharp
public partial class DataViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private Data? _data;

    public DataViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken token)
    {
        IsLoading = true;
        try
        {
            Data = await _dataService.LoadAsync(token);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoad() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Data is null) return;
        IsLoading = true;
        try
        {
            await _dataService.SaveAsync(Data);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSave() => !IsLoading && Data is not null;
}
```

### Parameterized Commands

```csharp
public partial class ItemsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Item> _items = [];

    [RelayCommand]
    private void DeleteItem(Item item)
    {
        Items.Remove(item);
    }

    [RelayCommand]
    private async Task EditItemAsync(Item item)
    {
        var editedItem = await _dialogService.ShowEditDialogAsync(item);
        if (editedItem is not null)
        {
            var index = Items.IndexOf(item);
            Items[index] = editedItem;
        }
    }
}
```

```xml
<ListBox ItemsSource="{Binding Items}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Name}"/>
                <Button Content="Edit"
                        Command="{Binding DataContext.EditItemCommand,
                                  RelativeSource={RelativeSource AncestorType=ListBox}}"
                        CommandParameter="{Binding}"/>
                <Button Content="Delete"
                        Command="{Binding DataContext.DeleteItemCommand,
                                  RelativeSource={RelativeSource AncestorType=ListBox}}"
                        CommandParameter="{Binding}"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

### Composite Commands

```csharp
public interface ICompositeCommandManager
{
    ICommand SaveAllCommand { get; }
    void RegisterSaveCommand(ICommand command);
    void UnregisterSaveCommand(ICommand command);
}

public class CompositeCommandManager : ICompositeCommandManager
{
    private readonly List<ICommand> _saveCommands = [];
    private readonly RelayCommand _saveAllCommand;

    public ICommand SaveAllCommand => _saveAllCommand;

    public CompositeCommandManager()
    {
        _saveAllCommand = new RelayCommand(
            () => ExecuteAll(_saveCommands),
            () => _saveCommands.All(c => c.CanExecute(null)));
    }

    public void RegisterSaveCommand(ICommand command)
    {
        _saveCommands.Add(command);
        command.CanExecuteChanged += OnCommandCanExecuteChanged;
    }

    public void UnregisterSaveCommand(ICommand command)
    {
        _saveCommands.Remove(command);
        command.CanExecuteChanged -= OnCommandCanExecuteChanged;
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        _saveAllCommand.NotifyCanExecuteChanged();
    }

    private static void ExecuteAll(IEnumerable<ICommand> commands)
    {
        foreach (var command in commands.Where(c => c.CanExecute(null)))
        {
            command.Execute(null);
        }
    }
}
```

## View Locator Pattern

```csharp
public interface IViewLocator
{
    FrameworkElement? ResolveView(object viewModel);
}

public class ViewLocator : IViewLocator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, Type> _viewModelToViewMap = new();

    public ViewLocator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        RegisterMappings();
    }

    private void RegisterMappings()
    {
        _viewModelToViewMap[typeof(CustomersViewModel)] = typeof(CustomersView);
        _viewModelToViewMap[typeof(OrdersViewModel)] = typeof(OrdersView);
        _viewModelToViewMap[typeof(SettingsViewModel)] = typeof(SettingsView);
    }

    public FrameworkElement? ResolveView(object viewModel)
    {
        var viewModelType = viewModel.GetType();

        if (_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            var view = (FrameworkElement)_serviceProvider.GetRequiredService(viewType);
            view.DataContext = viewModel;
            return view;
        }

        return null;
    }
}
```

## Attached Behavior Pattern

```csharp
public static class TextBoxBehaviors
{
    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus",
            typeof(bool),
            typeof(TextBoxBehaviors),
            new PropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(DependencyObject obj)
        => (bool)obj.GetValue(SelectAllOnFocusProperty);

    public static void SetSelectAllOnFocus(DependencyObject obj, bool value)
        => obj.SetValue(SelectAllOnFocusProperty, value);

    private static void OnSelectAllOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.GotFocus += TextBox_GotFocus;
            }
            else
            {
                textBox.GotFocus -= TextBox_GotFocus;
            }
        }
    }

    private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }
}
```

```xml
<TextBox local:TextBoxBehaviors.SelectAllOnFocus="True"/>
```

## Validation Pattern with INotifyDataErrorInfo

```csharp
public partial class CustomerFormViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required")]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 150, ErrorMessage = "Age must be between 0 and 150")]
    private int _age;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        await _customerService.SaveAsync(new Customer
        {
            Name = Name,
            Email = Email,
            Age = Age
        });
    }

    private bool CanSubmit() => !HasErrors;
}
```

```xml
<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged, ValidatesOnNotifyDataErrors=True}"/>
<ItemsControl ItemsSource="{Binding (Validation.Errors), RelativeSource={RelativeSource Self}}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding ErrorContent}" Foreground="Red"/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

## State Management Pattern

```csharp
public enum ViewState
{
    Loading,
    Loaded,
    Empty,
    Error
}

public partial class StatefulViewModel : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<Item> _items = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        State = ViewState.Loading;
        ErrorMessage = null;

        try
        {
            var data = await _service.GetItemsAsync();
            Items = new ObservableCollection<Item>(data);
            State = Items.Count > 0 ? ViewState.Loaded : ViewState.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = ViewState.Error;
        }
    }
}
```

```xml
<Grid>
    <!-- Loading State -->
    <ProgressBar IsIndeterminate="True"
                 Visibility="{Binding State, Converter={StaticResource StateToVisibility},
                              ConverterParameter=Loading}"/>

    <!-- Loaded State -->
    <ListBox ItemsSource="{Binding Items}"
             Visibility="{Binding State, Converter={StaticResource StateToVisibility},
                          ConverterParameter=Loaded}"/>

    <!-- Empty State -->
    <TextBlock Text="No items found"
               Visibility="{Binding State, Converter={StaticResource StateToVisibility},
                            ConverterParameter=Empty}"/>

    <!-- Error State -->
    <StackPanel Visibility="{Binding State, Converter={StaticResource StateToVisibility},
                             ConverterParameter=Error}">
        <TextBlock Text="{Binding ErrorMessage}" Foreground="Red"/>
        <Button Content="Retry" Command="{Binding LoadCommand}"/>
    </StackPanel>
</Grid>
```
