using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using SuperDock.Search;

namespace SuperDock.SearchModels;

public sealed class SearchCategoryViewModel : ObservableObject
{
    private int _count;
    private bool _isSelected;

    public SearchCategoryViewModel(SearchCategory category, string label)
    {
        Category = category;
        Label = label;
        GlyphData = SearchGlyphs.For(category);
    }

    public SearchCategory Category { get; }

    public string Label { get; }

    public string GlyphData { get; }

    public int Count
    {
        get => _count;
        private set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(CountText));
                OnPropertyChanged(nameof(HasCount));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasCount => Count > 0;

    public string CountText => Count > 0 ? Count.ToString() : string.Empty;

    public void Update(int count) => Count = count;
}

public sealed class SearchViewModel : ObservableObject
{
    CancellationTokenSource? _queryCancellation;
    string _lastQuery="",_sortMode="相关度";
    public string SortMode {get=>_sortMode;set{if(SetProperty(ref _sortMode,value))Search(_lastQuery);}}
    private const int MaxResults = 500;
    private const int QueryLimit = 900;
    private const int MaxHistory = 24;

    private readonly EverythingSearchService _service = new();
    private readonly FilePreviewService _previewService = new();
    private readonly SearchPreferencesStore _preferencesStore = new();
    private readonly SearchPreferences _preferences;
    private readonly List<SearchItemViewModel> _all = [];
    private readonly System.Windows.Threading.DispatcherTimer _copyHintTimer;
    private readonly System.Windows.Threading.DispatcherTimer _historyTimer;
    private CancellationTokenSource? _previewCancellation;
    private int _searchToken;
    private string _pendingHistoryTerm = string.Empty;
    private string _statusText = "输入关键字开始搜索\n支持 ext:pdf、dm:today、\\路径\\ 等 Everything 语法";
    private string _detailText = string.Empty;
    private string _copyHint = string.Empty;
    private string _selectionSummary = string.Empty;
    private bool _isCopyHintVisible;
    private bool _isServiceUnavailable;
    private bool _isEmpty = true;
    private bool _isSearching;
    private bool _previewEnabled;
    private bool _isQueryEmpty = true;
    private SearchItemViewModel? _selectedItem;
    private SearchCategory _selectedCategory = SearchCategory.All;

    public SearchViewModel()
    {
        _preferences = _preferencesStore.Load();
        _previewEnabled = _preferences.PreviewEnabled;
        foreach (var term in _preferences.History)
        {
            History.Add(term);
        }

        _copyHintTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.6)
        };
        _copyHintTimer.Tick += (_, _) =>
        {
            _copyHintTimer.Stop();
            IsCopyHintVisible = false;
        };

        // 输入停止 900ms 后才写入历史，避免把「合 / 合同」这类中间态全记下来
        _historyTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _historyTimer.Tick += (_, _) =>
        {
            _historyTimer.Stop();
            CommitPendingHistory();
        };

        SelectCategoryCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SearchCategoryViewModel category)
                {
                    ApplyCategory(category.Category);
                }
            },
            parameter => parameter is SearchCategoryViewModel);

        Categories =
        [
            new SearchCategoryViewModel(SearchCategory.All, "全部"),
            new SearchCategoryViewModel(SearchCategory.Program, "程序"),
            new SearchCategoryViewModel(SearchCategory.Document, "文档"),
            new SearchCategoryViewModel(SearchCategory.Spreadsheet, "表格"),
            new SearchCategoryViewModel(SearchCategory.Presentation, "演示"),
            new SearchCategoryViewModel(SearchCategory.Image, "图片"),
            new SearchCategoryViewModel(SearchCategory.Media, "音视频"),
            new SearchCategoryViewModel(SearchCategory.Archive, "压缩包"),
            new SearchCategoryViewModel(SearchCategory.Folder, "文件夹"),
            new SearchCategoryViewModel(SearchCategory.Other, "其他")
        ];

        Categories[0].IsSelected = true;
    }

    public IReadOnlyList<SearchCategoryViewModel> Categories { get; }

    public RelayCommand SelectCategoryCommand { get; }

    public ObservableCollection<SearchItemViewModel> Results { get; } = [];

    /// <summary>最近搜索词，最新在前。</summary>
    public ObservableCollection<string> History { get; } = [];

    public FilePreviewViewModel Preview { get; } = new();

    public bool HasHistory => History.Count > 0;

    public bool IsHistoryVisible => _isQueryEmpty && History.Count > 0;

    public bool PreviewEnabled
    {
        get => _previewEnabled;
        set
        {
            if (!SetProperty(ref _previewEnabled, value))
            {
                return;
            }

            _preferences.PreviewEnabled = value;
            SavePreferences();
            if (value)
            {
                SetPreviewTarget(_selectedItem);
            }
            else
            {
                CancelPreview();
                Preview.Clear();
            }
        }
    }

    public SearchCategoryViewModel? SelectedCategory
    {
        get => Categories.FirstOrDefault(category => category.Category == _selectedCategory);
        set
        {
            if (value is not null && value.Category != _selectedCategory)
            {
                ApplyCategory(value.Category);
            }
        }
    }

    public SearchItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                SetPreviewTarget(value);
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    public bool IsServiceUnavailable
    {
        get => _isServiceUnavailable;
        private set => SetProperty(ref _isServiceUnavailable, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set
        {
            if (SetProperty(ref _selectionSummary, value))
            {
                OnPropertyChanged(nameof(HasSelectionSummary));
            }
        }
    }

    public bool HasSelectionSummary => !string.IsNullOrEmpty(_selectionSummary);

    /// <summary>更新底部“已选 N 项”提示（只有多选时才显示）。</summary>
    public void UpdateSelection(int count)
    {
        SelectionSummary = count > 1 ? $"已选 {count} 项" : string.Empty;
    }

    public string CopyHint
    {
        get => _copyHint;
        private set => SetProperty(ref _copyHint, value);
    }

    public bool IsCopyHintVisible
    {
        get => _isCopyHintVisible;
        private set => SetProperty(ref _isCopyHintVisible, value);
    }

    /// <summary>在窗口右下角弹一个短暂的“已复制”提示。</summary>
    public void ShowCopyHint(string message)
    {
        CopyHint = message;
        IsCopyHintVisible = true;
        _copyHintTimer.Stop();
        _copyHintTimer.Start();
    }

    public void AddHistory(string term)
    {
        var trimmed = (term ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var existing = History.FirstOrDefault(item =>
            string.Equals(item, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            History.Remove(existing);
        }

        History.Insert(0, trimmed);
        while (History.Count > MaxHistory)
        {
            History.RemoveAt(History.Count - 1);
        }

        SavePreferences();
        NotifyHistoryChanged();
    }

    public void RemoveHistory(string term)
    {
        var existing = History.FirstOrDefault(item =>
            string.Equals(item, term, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        History.Remove(existing);
        SavePreferences();
        NotifyHistoryChanged();
    }

    public void ClearHistory()
    {
        History.Clear();
        SavePreferences();
        NotifyHistoryChanged();
    }

    /// <summary>窗口关闭前把还没稳定下来的最后一次查询落盘。</summary>
    public void FlushHistory()
    {
        _historyTimer.Stop();
        CommitPendingHistory();
    }

    public async void Search(string query)
    {
        var term = (query ?? string.Empty).Trim();_lastQuery=term;var sort=SortMode;
        _queryCancellation?.Cancel();_queryCancellation?.Dispose();_queryCancellation=new CancellationTokenSource();var cancellationToken=_queryCancellation.Token;
        var token = ++_searchToken;

        if (term.Length == 0)
        {
            _historyTimer.Stop();
            _pendingHistoryTerm = string.Empty;
            _all.Clear();
            Results.Clear();
            SelectedItem = null;
            IsEmpty = true;
            IsSearching = false;
            StatusText = "输入关键字开始搜索\n支持 ext:pdf、dm:today、\\路径\\ 等 Everything 语法";
            DetailText = string.Empty;
            _isQueryEmpty = true;
            CancelPreview();
            Preview.Clear();
            RefreshCategoryCounts();
            NotifyHistoryChanged();
            return;
        }

        _isQueryEmpty = false;
        _pendingHistoryTerm = term;
        _historyTimer.Stop();
        _historyTimer.Start();
        NotifyHistoryChanged();

        IsSearching = true;
        IsServiceUnavailable = false;
        StatusText = "正在准备文件索引…";
        try
        {
            // 自动恢复：已有实例直接用，没有就后台拉起（优先用户已装，其次内置便携版）
            StatusText="正在搜索…";
            var watch = Stopwatch.StartNew();
            var response=await SuperDock.FileSearchBackend.Query(term,"全部",QueryLimit,sort,cancellationToken);
            var result=(Hits:response.Hits,Message:response.Message,Unavailable:!response.Success);
            watch.Stop();

            if (token != _searchToken)
            {
                return;
            }

            _all.Clear();
            if (result.Unavailable)
            {
                Results.Clear();
                SelectedItem = null;
                IsServiceUnavailable = true;
                IsEmpty = true;
                StatusText = result.Message;
                DetailText = string.Empty;
                RefreshCategoryCounts();
                return;
            }

            IsServiceUnavailable = false;
            foreach (var hit in (sort=="相关度"?Rank(result.Hits,term):sort=="大小升序"?result.Hits.OrderBy(h=>h.IsFolder).ThenBy(h=>h.Size).ThenBy(h=>h.Name,StringComparer.CurrentCultureIgnoreCase):result.Hits.OrderBy(h=>h.IsFolder).ThenByDescending(h=>h.Size).ThenBy(h=>h.Name,StringComparer.CurrentCultureIgnoreCase)).Take(MaxResults))
            {
                _all.Add(new SearchItemViewModel(hit));
            }

            ApplyCategory(_selectedCategory);
            DetailText = _all.Count == 0 ? string.Empty : $"{_all.Count} 项 · {watch.ElapsedMilliseconds} ms";
            StatusText = _all.Count == 0
                ? (string.IsNullOrEmpty(result.Message) ? $"没有找到「{term}」相关的内容" : result.Message)
                : string.Empty;
        }
        catch (Exception exception)
        {
            if (token == _searchToken)
            {
                StatusText = exception.Message;
            }
        }
        finally
        {
            if (token == _searchToken)
            {
                IsSearching = false;
            }
        }
    }

    public void ApplyCategory(SearchCategory category)
    {
        _selectedCategory = category;
        foreach (var item in Categories)
        {
            item.IsSelected = item.Category == category;
        }

        Results.Clear();

        foreach (var item in _all)
        {
            if (category == SearchCategory.All || item.Category == category)
            {
                Results.Add(item);
            }
        }

        SelectedItem = Results.FirstOrDefault();
        IsEmpty = Results.Count == 0;
        OnPropertyChanged(nameof(SelectedCategory));
        RefreshCategoryCounts();

        // 切换分类时同步底部计数（搜索完成时会被“N 项 · X ms”覆盖）
        if (_all.Count == 0)
        {
            DetailText = string.Empty;
        }
        else if (category == SearchCategory.All)
        {
            DetailText = $"{_all.Count} 项";
        }
        else
        {
            DetailText = $"{Results.Count} / {_all.Count} 项";
        }

        if (_all.Count > 0 && Results.Count == 0)
        {
            StatusText = "该分类下没有结果，按 Tab 切换分类";
        }
        else if (_all.Count > 0)
        {
            StatusText = string.Empty;
        }
    }

    public void Stop(){_queryCancellation?.Cancel();_queryCancellation?.Dispose();_queryCancellation=null;_searchToken++;_copyHintTimer.Stop();_historyTimer.Stop();CancelPreview();}
    public void CycleCategory(int direction)
    {
        var index = Categories.ToList().FindIndex(category => category.Category == _selectedCategory);
        if (index < 0)
        {
            index = 0;
        }

        var next = (index + direction + Categories.Count) % Categories.Count;
        ApplyCategory(Categories[next].Category);
    }

    public static string? ResolveEverythingExecutable() => EverythingSearchService.ResolveEverythingExecutable();

    private async void SetPreviewTarget(SearchItemViewModel? item)
    {
        CancelPreview();
        if (item is null || !_previewEnabled)
        {
            Preview.Clear();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        var path = item.FullPath;
        var name = item.Name;
        Preview.Clear();

        try
        {
            var result = await Task.Run(()=>_previewService.BuildAsync(path,cancellation.Token),cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Preview.Apply(name, result);
        }
        catch (OperationCanceledException)
        {
            // 快速切换选中项，丢弃过期结果
        }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                Preview.Apply(name, new FilePreviewResult(
                    FilePreviewKind.Info, string.Empty, string.Empty, null, exception.Message));
            }
        }
    }

    private void CancelPreview()
    {
        if (_previewCancellation is not { } existing)
        {
            return;
        }

        _previewCancellation = null;
        existing.Cancel();
        existing.Dispose();
    }

    private void CommitPendingHistory()
    {
        if (string.IsNullOrWhiteSpace(_pendingHistoryTerm))
        {
            return;
        }

        AddHistory(_pendingHistoryTerm);
        _pendingHistoryTerm = string.Empty;
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(IsHistoryVisible));
    }

    private void SavePreferences()
    {
        _preferences.History = History.ToList();
        _preferencesStore.Save(_preferences);
    }

    private void RefreshCategoryCounts()
    {
        foreach (var category in Categories)
        {
            var count = category.Category == SearchCategory.All
                ? _all.Count
                : _all.Count(item => item.Category == category.Category);
            category.Update(count);
        }
    }

    private static IEnumerable<EverythingHit> Rank(IReadOnlyList<EverythingHit> hits, string term)
    {
        return hits
            .Where(hit => !IsNoise(hit.FullPath))
            .OrderByDescending(hit => Score(hit, term))
            .ThenBy(hit => hit.Name.Length)
            .ThenBy(hit => hit.FullPath.Length);
    }

    /// <summary>回收站、系统卷信息、临时目录这类结果对搜索没意义，直接过滤掉。</summary>
    private static bool IsNoise(string fullPath)
    {
        return fullPath.Contains(@"\$RECYCLE.BIN\", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(@"\System Volume Information\", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(@"\AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase);
    }

    private static int Score(EverythingHit hit, string term)
    {
        var score = 0;
        if (string.Equals(hit.Name, term, StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }
        else if (hit.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            score += 400;
        }
        else if (hit.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            score += 300;
        }
        else
        {
            score += 100;
        }

        if (hit.Category == SearchCategory.Program)
        {
            score += 40;
        }

        // 同分时文件优先于文件夹：搜东西通常是要打开文件
        if (!hit.IsFolder)
        {
            score += 5;
        }

        if (hit.FullPath.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase)
            || hit.FullPath.Contains(@"\Start Menu\", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        return score;
    }
}
