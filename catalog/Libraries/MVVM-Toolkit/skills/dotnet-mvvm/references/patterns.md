# MVVM Patterns Reference

## Property Patterns

### Simple Observable Property
```csharp
[ObservableProperty]
private string _name = string.Empty;
```

### Property with Change Notification
```csharp
[ObservableProperty]
private int _quantity;

partial void OnQuantityChanged(int oldValue, int newValue)
{
    // React to change
    OnPropertyChanged(nameof(Total));
}
```

### Property Affecting Commands
```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
[NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
private bool _isValid;
```

### Property with Validation
```csharp
[ObservableProperty]
[NotifyDataErrorInfo]
[Required(ErrorMessage = "Name is required")]
[MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
private string _name = string.Empty;
```

## Command Patterns

### Simple Async Command
```csharp
[RelayCommand]
private async Task LoadDataAsync()
{
    var data = await _service.GetDataAsync();
    Items = new ObservableCollection<Item>(data);
}
```

### Command with CanExecute
```csharp
[RelayCommand(CanExecute = nameof(CanSave))]
private async Task SaveAsync()
{
    await _service.SaveAsync(CurrentItem);
}

private bool CanSave() => IsValid && !IsBusy;
```

### Command with Parameter
```csharp
[RelayCommand]
private void SelectItem(Item item)
{
    SelectedItem = item;
}

[RelayCommand]
private async Task DeleteItemAsync(int itemId)
{
    await _service.DeleteAsync(itemId);
    Items.Remove(Items.First(i => i.Id == itemId));
}
```

### Command with Cancellation
```csharp
[RelayCommand(IncludeCancelCommand = true)]
private async Task SearchAsync(string query, CancellationToken token)
{
    await Task.Delay(300, token); // Debounce
    var results = await _searchService.SearchAsync(query, token);
    SearchResults = new ObservableCollection<SearchResult>(results);
}
```

## Collection Patterns

### Master-Detail
```csharp
public partial class MasterDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Item> _items = [];

    [ObservableProperty]
    private Item? _selectedItem;

    partial void OnSelectedItemChanged(Item? value)
    {
        if (value is not null)
        {
            LoadDetails(value.Id);
        }
    }

    private async void LoadDetails(int id)
    {
        DetailItem = await _service.GetDetailsAsync(id);
    }
}
```

### Filtered Collection
```csharp
public partial class FilteredListViewModel : ObservableObject
{
    private readonly List<Item> _allItems = [];

    [ObservableProperty]
    private ObservableCollection<Item> _filteredItems = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        FilterItems();
    }

    private void FilterItems()
    {
        var filtered = string.IsNullOrEmpty(SearchText)
            ? _allItems
            : _allItems.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        FilteredItems = new ObservableCollection<Item>(filtered);
    }
}
```

## Navigation Patterns

### ViewModel-First Navigation
```csharp
public interface INavigationService
{
    Task NavigateToAsync<TViewModel>() where TViewModel : ObservableObject;
    Task NavigateToAsync<TViewModel>(object parameter) where TViewModel : ObservableObject;
    Task GoBackAsync();
}

public partial class MainViewModel(INavigationService navigation) : ObservableObject
{
    [RelayCommand]
    private async Task OpenDetailsAsync(Item item)
    {
        await navigation.NavigateToAsync<ItemDetailViewModel>(item.Id);
    }
}
```

### View-First Navigation
```csharp
// Shell navigation (MAUI)
[RelayCommand]
private async Task NavigateToSettings()
{
    await Shell.Current.GoToAsync("settings");
}

// Frame navigation (WPF/WinUI)
[RelayCommand]
private void NavigateToSettings()
{
    _frame.Navigate(typeof(SettingsPage));
}
```

## State Management Patterns

### Loading State
```csharp
public partial class DataViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    [NotifyPropertyChangedFor(nameof(ShowLoading))]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    private ViewState _state = ViewState.Idle;

    [ObservableProperty]
    private string? _errorMessage;

    public bool ShowLoading => State == ViewState.Loading;
    public bool ShowContent => State == ViewState.Success;
    public bool ShowError => State == ViewState.Error;

    [RelayCommand]
    private async Task LoadAsync()
    {
        State = ViewState.Loading;
        try
        {
            var data = await _service.GetDataAsync();
            Items = new ObservableCollection<Item>(data);
            State = ViewState.Success;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = ViewState.Error;
        }
    }
}

public enum ViewState { Idle, Loading, Success, Error }
```

### Undo/Redo Pattern
```csharp
public partial class EditViewModel : ObservableObject
{
    private readonly Stack<Action> _undoStack = new();
    private readonly Stack<Action> _redoStack = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private bool _canRedo;

    public void ExecuteCommand(Action doAction, Action undoAction)
    {
        doAction();
        _undoStack.Push(undoAction);
        _redoStack.Clear();
        UpdateCanUndoRedo();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var action = _undoStack.Pop();
        action();
        _redoStack.Push(action);
        UpdateCanUndoRedo();
    }
}
```

## Dialog Patterns

### Confirmation Dialog
```csharp
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PromptAsync(string title, string message);
    Task AlertAsync(string title, string message);
}

[RelayCommand]
private async Task DeleteItemAsync(Item item)
{
    var confirmed = await _dialogService.ConfirmAsync(
        "Delete Item",
        $"Are you sure you want to delete '{item.Name}'?");

    if (confirmed)
    {
        await _service.DeleteAsync(item.Id);
        Items.Remove(item);
    }
}
```

## Dependency Injection Patterns

### Constructor Injection (Preferred)
```csharp
public partial class ProductViewModel(
    IProductService productService,
    INavigationService navigation,
    IDialogService dialogs) : ObservableObject
{
    // Use injected services directly
}
```

### Factory Pattern for ViewModels
```csharp
public interface IViewModelFactory
{
    TViewModel Create<TViewModel>() where TViewModel : ObservableObject;
}

public class ViewModelFactory(IServiceProvider provider) : IViewModelFactory
{
    public TViewModel Create<TViewModel>() where TViewModel : ObservableObject
    {
        return provider.GetRequiredService<TViewModel>();
    }
}
```
