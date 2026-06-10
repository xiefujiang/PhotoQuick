using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using PhotoQuick.Application;
using PhotoQuick.Domain;
using PhotoQuick.Infrastructure;
using PhotoQuick.Views;
using MessageBox = System.Windows.MessageBox;

namespace PhotoQuick.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IFolderScanner _scanner;
    private readonly BrowserSession _session;
    private readonly IFileOperationService _fileOperations;
    private readonly IAppSettingsStore _settingsStore;
    private AppSettings _settings = AppSettings.Default;
    private CancellationTokenSource? _scanCts;
    private bool _updatingSelection;

    private string _currentFolder = "请选择图片文件夹";
    private string _statusText = "就绪";
    private BitmapSource? _currentImage;
    private ImageItem? _currentItem;
    private bool _isLoading;
    private bool _recursiveScan;
    private SortMode _selectedSortMode;
    private bool _showSidePanel = true;
    private string? _selectedMoveFolder;

    public MainViewModel()
    {
        _scanner = new FolderScanner();
        var cache = new ImageMemoryCache();
        _session = new BrowserSession(new WicImageDecoder(), cache);
        _fileOperations = new FileOperationService();
        _settingsStore = new JsonAppSettingsStore();

        OpenFolderCommand = new AsyncRelayCommand(_ => OpenFolderAsync());
        PreviousCommand = new AsyncRelayCommand(_ => NavigateByAsync(-1), _ => Items.Count > 0);
        NextCommand = new AsyncRelayCommand(_ => NavigateByAsync(1), _ => Items.Count > 0);
        RenameCommand = new AsyncRelayCommand(_ => RenameCurrentAsync(), _ => CurrentItem is not null);
        MoveCommand = new AsyncRelayCommand(_ => MoveCurrentAsync(), _ => CurrentItem is not null && !string.IsNullOrWhiteSpace(SelectedMoveFolder));
        DeleteCommand = new AsyncRelayCommand(_ => DeleteCurrentAsync(), _ => CurrentItem is not null);
        AddPresetFolderCommand = new AsyncRelayCommand(_ => AddPresetFolderAsync());
        RemovePresetFolderCommand = new AsyncRelayCommand(_ => RemovePresetFolderAsync(), _ => !string.IsNullOrWhiteSpace(SelectedMoveFolder));
        ToggleSidePanelCommand = new RelayCommand(_ => ShowSidePanel = !ShowSidePanel);

        _ = LoadSettingsAsync();
    }

    public ObservableCollection<ImageItem> Items { get; } = [];
    public ObservableCollection<string> PresetMoveFolders { get; } = [];
    public IReadOnlyList<SortMode> SortModes { get; } =
    [
        SortMode.FileNameAsc,
        SortMode.FileNameDesc,
        SortMode.LastWriteTimeAsc,
        SortMode.LastWriteTimeDesc
    ];

    public AsyncRelayCommand OpenFolderCommand { get; }
    public AsyncRelayCommand PreviousCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }
    public AsyncRelayCommand MoveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand AddPresetFolderCommand { get; }
    public AsyncRelayCommand RemovePresetFolderCommand { get; }
    public RelayCommand ToggleSidePanelCommand { get; }

    public string CurrentFolder
    {
        get => _currentFolder;
        private set => SetProperty(ref _currentFolder, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string IndexText => Items.Count == 0 || _session.CurrentIndex < 0
        ? "0 / 0"
        : $"{_session.CurrentIndex + 1} / {Items.Count}";

    public BitmapSource? CurrentImage
    {
        get => _currentImage;
        private set => SetProperty(ref _currentImage, value);
    }

    public ImageItem? CurrentItem
    {
        get => _currentItem;
        set
        {
            if (!SetProperty(ref _currentItem, value) || _updatingSelection || value is null)
            {
                return;
            }

            _ = NavigateToItemAsync(value);
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool RecursiveScan
    {
        get => _recursiveScan;
        set
        {
            if (SetProperty(ref _recursiveScan, value))
            {
                _settings.RecursiveScan = value;
                _ = SaveSettingsAsync();
            }
        }
    }

    public SortMode SelectedSortMode
    {
        get => _selectedSortMode;
        set
        {
            if (!SetProperty(ref _selectedSortMode, value))
            {
                return;
            }

            _settings.SortMode = value;
            SortCurrentItems();
            _ = SaveSettingsAsync();
        }
    }

    public bool ShowSidePanel
    {
        get => _showSidePanel;
        set
        {
            if (SetProperty(ref _showSidePanel, value))
            {
                _settings.ShowSidePanel = value;
                _ = SaveSettingsAsync();
            }
        }
    }

    public string? SelectedMoveFolder
    {
        get => _selectedMoveFolder;
        set
        {
            if (SetProperty(ref _selectedMoveFolder, value))
            {
                MoveCommand.RaiseCanExecuteChanged();
                RemovePresetFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task OpenFolderAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择要浏览的图片文件夹",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            await LoadFolderAsync(dialog.SelectedPath);
        }
    }

    public Task NavigateByAsync(int delta) => NavigateToIndexAsync(_session.CurrentIndex + delta);

    private async Task LoadFolderAsync(string folder)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        Items.Clear();
        CurrentImage = null;
        CurrentItem = null;
        CurrentFolder = folder;
        StatusText = "正在扫描...";
        IsLoading = true;
        RaiseNavigationStateChanged();

        var discovered = new List<ImageItem>();

        try
        {
            await foreach (var progress in _scanner.ScanAsync(folder, RecursiveScan, token))
            {
                if (progress.Item is not null)
                {
                    discovered.Add(progress.Item);
                    StatusText = $"已发现 {progress.Count} 张图片";
                }
            }

            foreach (var item in Sort(discovered))
            {
                Items.Add(item);
            }

            _session.SetItems(Items.ToList());
            StatusText = Items.Count == 0 ? "未找到支持的图片文件" : $"加载完成：{Items.Count} 张图片";
            await NavigateToIndexAsync(0);
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
        }
        finally
        {
            IsLoading = false;
            RaiseNavigationStateChanged();
        }
    }

    private Task NavigateToItemAsync(ImageItem item)
    {
        var index = Items.IndexOf(item);
        return index >= 0 ? NavigateToIndexAsync(index) : Task.CompletedTask;
    }

    private async Task NavigateToIndexAsync(int index)
    {
        if (Items.Count == 0)
        {
            CurrentImage = null;
            CurrentItem = null;
            OnPropertyChanged(nameof(IndexText));
            return;
        }

        var image = await _session.NavigateAsync(index);
        CurrentImage = image?.Bitmap;

        _updatingSelection = true;
        CurrentItem = Items[_session.CurrentIndex];
        _updatingSelection = false;

        StatusText = image?.IsPlaceholder == true
            ? "当前文件无法直接解码，可能需要安装相机 RAW 编解码器或接入 LibRaw。"
            : "就绪";

        OnPropertyChanged(nameof(IndexText));
        RaiseNavigationStateChanged();
    }

    private async Task RenameCurrentAsync()
    {
        if (CurrentItem is null)
        {
            return;
        }

        var name = TextInputDialog.Prompt("重命名", "输入新文件名（不含扩展名）", CurrentItem.Name);
        if (name is null)
        {
            return;
        }

        var result = await _fileOperations.RenameAsync(CurrentItem, name, CancellationToken.None);
        if (!result.Success || result.NewPath is null)
        {
            MessageBox.Show(result.Error ?? "重命名失败。", "PhotoQuick", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ReplaceCurrentItem(CurrentItem.WithPath(result.NewPath));
    }

    private async Task MoveCurrentAsync()
    {
        if (CurrentItem is null || string.IsNullOrWhiteSpace(SelectedMoveFolder))
        {
            return;
        }

        var result = await _fileOperations.MoveAsync(CurrentItem, SelectedMoveFolder, CancellationToken.None);
        if (!result.Success)
        {
            MessageBox.Show(result.Error ?? "移动失败。", "PhotoQuick", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RemoveCurrentItemAfterOperation();
    }

    private async Task DeleteCurrentAsync()
    {
        if (CurrentItem is null)
        {
            return;
        }

        var result = await _fileOperations.MoveToRecycleBinAsync(CurrentItem, CancellationToken.None);
        if (!result.Success)
        {
            MessageBox.Show(result.Error ?? "删除失败。", "PhotoQuick", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RemoveCurrentItemAfterOperation();
    }

    private async Task AddPresetFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择一个或多个常用移动目标目录",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var addedFolders = new List<string>();
        foreach (var folder in dialog.FolderNames.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (PresetMoveFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            PresetMoveFolders.Add(folder);
            addedFolders.Add(folder);
        }

        if (addedFolders.Count > 0)
        {
            SelectedMoveFolder = addedFolders[^1];
            _settings.PresetMoveFolders = PresetMoveFolders.ToList();
            await SaveSettingsAsync();
        }
    }

    private async Task RemovePresetFolderAsync()
    {
        if (SelectedMoveFolder is null)
        {
            return;
        }

        PresetMoveFolders.Remove(SelectedMoveFolder);
        SelectedMoveFolder = PresetMoveFolders.FirstOrDefault();
        _settings.PresetMoveFolders = PresetMoveFolders.ToList();
        await SaveSettingsAsync();
    }

    private void ReplaceCurrentItem(ImageItem newItem)
    {
        var index = _session.CurrentIndex;
        if (index < 0 || index >= Items.Count)
        {
            return;
        }

        Items[index] = newItem;
        _session.SetItems(Items.ToList());
        _ = NavigateToIndexAsync(index);
    }

    private void RemoveCurrentItemAfterOperation()
    {
        var oldIndex = _session.CurrentIndex;
        if (oldIndex < 0 || oldIndex >= Items.Count)
        {
            return;
        }

        Items.RemoveAt(oldIndex);
        _session.SetItems(Items.ToList());
        _ = NavigateToIndexAsync(Math.Min(oldIndex, Items.Count - 1));
    }

    private void SortCurrentItems()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var currentPath = CurrentItem?.Path;
        var sorted = Sort(Items).ToList();
        Items.Clear();
        foreach (var item in sorted)
        {
            Items.Add(item);
        }

        _session.SetItems(Items.ToList());
        var nextIndex = currentPath is null
            ? 0
            : Math.Max(0, Items.ToList().FindIndex(x => string.Equals(x.Path, currentPath, StringComparison.OrdinalIgnoreCase)));
        _ = NavigateToIndexAsync(nextIndex);
    }

    private IEnumerable<ImageItem> Sort(IEnumerable<ImageItem> items) => SelectedSortMode switch
    {
        SortMode.FileNameDesc => items.OrderByDescending(x => x.FileName, StringComparer.CurrentCultureIgnoreCase),
        SortMode.LastWriteTimeAsc => items.OrderBy(x => x.LastWriteTime).ThenBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase),
        SortMode.LastWriteTimeDesc => items.OrderByDescending(x => x.LastWriteTime).ThenBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase),
        _ => items.OrderBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase)
    };

    private async Task LoadSettingsAsync()
    {
        var loaded = await _settingsStore.LoadAsync(CancellationToken.None);
        _settings = loaded;

        RecursiveScan = loaded.RecursiveScan;
        SelectedSortMode = loaded.SortMode;
        ShowSidePanel = loaded.ShowSidePanel;

        PresetMoveFolders.Clear();
        foreach (var folder in loaded.PresetMoveFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            PresetMoveFolders.Add(folder);
        }

        SelectedMoveFolder = PresetMoveFolders.FirstOrDefault();
    }

    private async Task SaveSettingsAsync()
    {
        _settings.RecursiveScan = RecursiveScan;
        _settings.SortMode = SelectedSortMode;
        _settings.ShowSidePanel = ShowSidePanel;
        _settings.PresetMoveFolders = PresetMoveFolders.ToList();
        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
    }

    private void RaiseNavigationStateChanged()
    {
        PreviousCommand.RaiseCanExecuteChanged();
        NextCommand.RaiseCanExecuteChanged();
        RenameCommand.RaiseCanExecuteChanged();
        MoveCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IndexText));
    }
}
