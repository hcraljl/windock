using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using SuperDock.Search;
using SuperDock.SearchModels;
using WpfButton = System.Windows.Controls.Button;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfThumb = System.Windows.Controls.Primitives.Thumb;

namespace SuperDock.SearchViews;

public partial class SearchWindow : Window
{
    private const uint FileOperationDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const double DragThreshold = 4;

    private static double _lastWidth = 720;
    private static double _lastHeight = 500;

    private readonly DispatcherTimer _debounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(170)
    };

    private bool _hasBeenActivated;
    private bool _isModalOpen;
    private DateTime _lastDragUtc = DateTime.MinValue;
    private System.Windows.Point? _dragOrigin;

    public Action<MainWindow.Entry>? PinToDock {get;set;}
    private void MenuPin_Click(object sender,RoutedEventArgs e){foreach(var item in GetSelectedItems())PinToDock?.Invoke(new MainWindow.Entry(item.Name,item.FullPath));ViewModel?.ShowCopyHint("已添加到 Dock");}
    bool iconsOnly;
    private void ToggleCategories_Click(object sender,RoutedEventArgs e){iconsOnly=!iconsOnly;CategoryColumn.Width=new GridLength(iconsOnly?54:184);foreach(var text in Descendants<TextBlock>(CategoryList)){if(text.Name is "CategoryLabel" or "CategoryCount")text.Visibility=iconsOnly?Visibility.Collapsed:Visibility.Visible;}foreach(var button in Descendants<Button>(CategoryList))button.ToolTip=button.DataContext is SearchCategoryViewModel category?category.Label:null;}
    static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}}    DocumentPreview? documentPreview;
    bool _minimizing;
    private void Minimize_Click(object sender,RoutedEventArgs e){_minimizing=true;WindowState=WindowState.Minimized;}
    private void Maximize_Click(object sender,RoutedEventArgs e){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;MaximizeButton.Content=WindowState==WindowState.Maximized?"\uE923":"\uE922";}
    private void CloseSearch_Click(object sender,RoutedEventArgs e)=>Close();
    private void Sort_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:string sort}){foreach(var button in SortButtons.Children.OfType<Button>())button.Background=Equals(button.Tag,sort)?(Brush)FindResource("AccentSoftBrush"):Brushes.Transparent;if(ViewModel is {} vm)vm.SortMode=sort;}}
    public string ShortcutText {get;set;}="Alt + 空格";
    public string SearchTheme {get;set;}="Light";
    public SearchWindow()
    {
        InitializeComponent();
        DialogTheme.Attach(this);StateChanged+=(_,_)=>{MaximizeButton.Content=WindowState==WindowState.Maximized?"\uE923":"\uE922";MaximizeButton.ToolTip=WindowState==WindowState.Maximized?"还原":"最大化";};

        Width = Math.Max(MinWidth, _lastWidth);
        Height = Math.Max(MinHeight, _lastHeight);

        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            ViewModel?.Search(QueryBox.Text);
        };

        Closed += (_, _) =>
        {
            _debounceTimer.Stop();ViewModel?.Stop();
            if (WindowState == WindowState.Normal)
            {
                _lastWidth = Width;
                _lastHeight = Height;
            }
        };

        Activated += (_, _) => {_hasBeenActivated=true;_minimizing=false;};
    }

    private SearchViewModel? ViewModel => DataContext as SearchViewModel;

    /// <summary>窗口正在关闭（关闭过程中再调用 Show/Close 会抛异常）。</summary>
    public bool IsClosing { get; private set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        IsClosing = true;
        ViewModel?.FlushHistory();
        base.OnClosing(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SearchHotkeyHint.Text=ShortcutText;Activate();
        QueryBox.Focus();
        Keyboard.Focus(QueryBox);

        if (ViewModel is { } viewModel)
        {
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SearchViewModel.PreviewEnabled))
                {
                    ApplyPreviewLayout();
        if(documentPreview==null){documentPreview=new DocumentPreview(_=>{},ViewModel);DocumentPreviewHost.Content=documentPreview;LegacyPreviewImage.Visibility=Visibility.Collapsed;LegacyPreviewText.Visibility=Visibility.Collapsed;if(ViewModel is {} vm)vm.PropertyChanged+=(_,args)=>{if(args.PropertyName=="SelectedItem")documentPreview.Select(vm.SelectedItem?.FullPath);};}
                }
            };
        }

        ApplyPreviewLayout();
        if(documentPreview==null){documentPreview=new DocumentPreview(_=>{},ViewModel);DocumentPreviewHost.Content=documentPreview;LegacyPreviewImage.Visibility=Visibility.Collapsed;LegacyPreviewText.Visibility=Visibility.Collapsed;if(ViewModel is {} vm)vm.PropertyChanged+=(_,args)=>{if(args.PropertyName=="SelectedItem")documentPreview.Select(vm.SelectedItem?.FullPath);};}

        // 打开搜索窗口时预热索引服务，首次输入不必再等待启动
        _ = Task.Run(()=>EverythingRuntime.EnsureAsync());
    }

    /// <summary>预览开关变化时切换列宽；关闭时结果列表占满整个右侧区域。</summary>
    private void ApplyPreviewLayout()
    {
        var enabled = ViewModel?.PreviewEnabled ?? false;
        PreviewColumn.MinWidth=enabled?340:0;MinWidth=enabled?820:560;PreviewColumn.Width=enabled?new GridLength(.85,GridUnitType.Star):new GridLength(0);
        PreviewSplitterColumn.Width = new GridLength(enabled ? 7 : 0);
        PreviewSplitter.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        PreviewPane.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (enabled && Width < 940)
        {
            var delta = 960 - Width;
            if (!double.IsNaN(Left))
            {
                Left -= delta / 2;
            }

            Width = 960;
        }
    }

    private void HistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string term })
        {
            return;
        }

        QueryBox.Text = term;
        // 设置文本会触发 TextChanged 并重启防抖计时器，这里直接取消重复搜索
        _debounceTimer.Stop();
        QueryBox.CaretIndex = term.Length;
        ViewModel?.Search(term);
        QueryBox.Focus();
        Keyboard.Focus(QueryBox);
    }

    private void DeleteHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string term })
        {
            ViewModel?.RemoveHistory(term);
        }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearHistory();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // 打开文件/切到其他窗口时自动收起；
        // 弹窗（重命名/确认框）打开期间、以及拖拽（移动/缩放）刚结束时不关，否则一拖就消失
        if (!_hasBeenActivated || _isModalOpen || IsClosing || _minimizing || WindowState==WindowState.Minimized)
        {
            return;
        }

        if (Mouse.LeftButton == MouseButtonState.Pressed
            || (DateTime.UtcNow - _lastDragUtc).TotalMilliseconds < 700)
        {
            return;
        }

        try
        {
            Close();
        }
        catch
        {
            // 已经处于关闭流程中，忽略
        }
    }

    private void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if(ResultsToolbar!=null)ResultsToolbar.Visibility=string.IsNullOrWhiteSpace(QueryBox.Text)?Visibility.Collapsed:Visibility.Visible;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        // 点完分类把焦点还给搜索框，方便继续输入
        Dispatcher.BeginInvoke(() =>
        {
            QueryBox.Focus();
            Keyboard.Focus(QueryBox);
        });
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Close();
                break;

            case Key.Enter:
                e.Handled = true;
                OpenSelected((Keyboard.Modifiers & ModifierKeys.Control) != 0);
                break;

            case Key.Down:
                e.Handled = true;
                MoveSelection(1);
                break;

            case Key.Up:
                e.Handled = true;
                MoveSelection(-1);
                break;

            case Key.Tab:
                e.Handled = true;
                ViewModel?.CycleCategory((Keyboard.Modifiers & ModifierKeys.Shift) == 0 ? 1 : -1);
                break;

            case Key.F2:
                e.Handled = true;
                RenameSelected();
                break;
        }
    }

    /// <summary>按下即选中：不依赖 ListBoxItem 自身的鼠标处理，避免任何上层的拖动/命中逻辑把点击吞掉。
    /// 注意 Ctrl/Shift 组合必须交给 ListBox 自己处理，否则会破坏多选；多选状态下点空白项同理。</summary>
    private void ResultsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<WpfListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not SearchItemViewModel searchItem)
        {
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None || ResultsList.SelectedItems.Count > 1)
        {
            return;
        }

        if (ViewModel is { } viewModel)
        {
            viewModel.SelectedItem = searchItem;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelected(false);
    }

    private void ResultsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var item = FindAncestor<WpfListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is null)
        {
            e.Handled = true;
            return;
        }

        // 右键点在选中项之外：收缩成只选这一项（Windows 标准行为）
        var clicked = item.DataContext as SearchItemViewModel;
        var selected = GetSelectedItems();
        if (clicked is not null && !selected.Contains(clicked))
        {
            ResultsList.SelectedItems.Clear();
            item.IsSelected = true;
            selected = [clicked];
        }

        var multi = selected.Count > 1;
        MenuMultiHeader.Header = $"已选 {selected.Count} 项";
        MenuMultiHeader.Visibility = multi ? Visibility.Visible : Visibility.Collapsed;
        MenuOpen.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuReveal.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuSingleSeparator.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuRename.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuCopySeparator.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuCopyName.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        MenuCopyPath.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ViewModel?.UpdateSelection(ResultsList.SelectedItems.Count);
    }

    private List<SearchItemViewModel> GetSelectedItems()
    {
        var items = ResultsList.SelectedItems?
            .OfType<SearchItemViewModel>()
            .ToList();
        if (items is { Count: > 0 })
        {
            return items;
        }

        return ViewModel?.SelectedItem is { } single ? [single] : [];
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveSource(e.OriginalSource as DependencyObject))
        {
            _dragOrigin = null;
            return;
        }

        // 这里只记下起点，不能直接 DragMove：
        // DragMove 会立刻接管鼠标，把列表项/分类的单击整个吞掉（表现为点了没反应）。
        _dragOrigin = e.GetPosition(this);
    }

    private void RootBorder_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragOrigin is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragOrigin = null;
            return;
        }

        var current = e.GetPosition(this);
        var movedEnough = Math.Abs(current.X - _dragOrigin.Value.X) >= DragThreshold
                          || Math.Abs(current.Y - _dragOrigin.Value.Y) >= DragThreshold;
        if (!movedEnough)
        {
            return;
        }

        _dragOrigin = null;
        try
        {
            DragMove();
        }
        catch
        {
            // 拖动过程中窗口被关闭会抛异常，忽略即可
        }
        finally
        {
            _lastDragUtc = DateTime.UtcNow;
        }
    }

    private void RootBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragOrigin = null;
    }

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
        _lastDragUtc = DateTime.UtcNow;
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e) => OpenSelected(false);

    private void MenuReveal_Click(object sender, RoutedEventArgs e) => OpenSelected(true);

    private void MenuCopyName_Click(object sender, RoutedEventArgs e)
    {
        var item = ViewModel?.SelectedItem;
        if (item is null)
        {
            return;
        }

        CopyToClipboard(item.Name, "已复制文件名");
    }

    private void MenuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var item = ViewModel?.SelectedItem;
        if (item is null)
        {
            return;
        }

        CopyToClipboard(item.FullPath, "已复制完整路径");
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private void MenuRecycle_Click(object sender, RoutedEventArgs e) => DeleteSelected(toRecycleBin: true);

    private void MenuDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected(toRecycleBin: false);

    private void MoveSelection(int delta)
    {
        var viewModel = ViewModel;
        if (viewModel is null || viewModel.Results.Count == 0)
        {
            return;
        }

        var current = viewModel.SelectedItem is null ? -1 : viewModel.Results.IndexOf(viewModel.SelectedItem);
        var next = Math.Clamp(current + delta, 0, viewModel.Results.Count - 1);
        viewModel.SelectedItem = viewModel.Results[next];
        ResultsList.ScrollIntoView(viewModel.SelectedItem);
    }

    private void OpenSelected(bool revealInExplorer)
    {
        var item = ViewModel?.SelectedItem;
        if (item is null)
        {
            return;
        }

        try
        {
            if (revealInExplorer)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"")
                {
                    UseShellExecute = true
                });
            }
            else
            {
                if(!ActivityStore.Launch(item.FullPath,item.Name)){ShowWarning("无法打开该文件，请检查默认应用或文件是否仍存在。");return;}
            }

            Close();
        }
        catch (Exception exception)
        {
            ShowWarning($"无法打开：{exception.Message}");
        }
    }

    private void RenameSelected()
    {
        var item = ViewModel?.SelectedItem;
        if (item is null)
        {
            return;
        }

        var dialog = new RenameDialog(item.Name, item.FullPath)
        {
            Owner = this
        };
        var confirmed = RunModal(dialog.ShowDialog);
        if (confirmed != true)
        {
            return;
        }

        var newName = dialog.NewName;
        if (string.Equals(newName, item.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(item.FullPath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            var target = Path.Combine(directory, newName);
            if (item.IsFolder)
            {
                Directory.Move(item.FullPath, target);
            }
            else
            {
                File.Move(item.FullPath, target);
            }

            RefreshSearch();
        }
        catch (Exception exception)
        {
            ShowWarning($"重命名失败：{exception.Message}");
        }
    }

    private void DeleteSelected(bool toRecycleBin)
    {
        var items = GetSelectedItems();
        if (items.Count == 0)
        {
            return;
        }

        var single = items.Count == 1;
        string message;
        if (single)
        {
            var item = items[0];
            var target = item.IsFolder ? "文件夹" : "文件";
            message = toRecycleBin
                ? $"确定把{target}「{item.Name}」移到回收站？"
                : $"彻底删除{target}「{item.Name}」？\n\n此操作不会进回收站，无法撤销。";
        }
        else
        {
            // 多选时列出前几个名字，让用户能核对删的是什么
            var names = string.Join("\n", items.Take(5).Select(item => $"· {item.Name}"));
            if (items.Count > 5)
            {
                names += $"\n… 等共 {items.Count} 项";
            }

            message = toRecycleBin
                ? $"确定把选中的 {items.Count} 项移到回收站？\n\n{names}"
                : $"彻底删除选中的 {items.Count} 项？\n\n{names}\n\n此操作不会进回收站，无法撤销。";
        }

        var confirmed = RunModal(() => MessageDialog.Confirm(
            this,
            toRecycleBin ? "移到回收站" : "彻底删除",
            message,
            toRecycleBin ? "移到回收站" : "彻底删除",
            "取消",
            toRecycleBin ? MessageDialogKind.Question : MessageDialogKind.Danger));

        if (!confirmed)
        {
            return;
        }

        try
        {
            if (toRecycleBin)
            {
                // 一次 SHFileOperation 传入全部路径，回收站里算同一个撤销步骤
                MoveToRecycleBin(items.Select(item => item.FullPath).ToList());
            }
            else
            {
                var failures = new List<string>();
                foreach (var item in items)
                {
                    try
                    {
                        if (item.IsFolder)
                        {
                            Directory.Delete(item.FullPath, recursive: true);
                        }
                        else
                        {
                            File.Delete(item.FullPath);
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"{item.Name}：{exception.Message}");
                    }
                }

                if (failures.Count > 0)
                {
                    ShowWarning($"有 {failures.Count} 项删除失败：\n{string.Join("\n", failures.Take(5))}");
                }
            }

            ResultsList.SelectedItems.Clear();
            RefreshSearch();
        }
        catch (Exception exception)
        {
            ShowWarning($"删除失败：{exception.Message}");
        }
    }

    private static void MoveToRecycleBin(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        // SHFileOperation 的 pFrom 以 \0 分隔多个路径，结尾再补一个 \0
        var from = string.Join("\0", paths) + "\0\0";
        var operation = new ShFileOpStruct
        {
            wFunc = FileOperationDelete,
            pFrom = from,
            fFlags = (ushort)(FofAllowUndo | FofNoConfirmation | FofNoErrorUi | FofSilent)
        };

        var result = SHFileOperation(ref operation);
        if (result != 0)
        {
            throw new IOException($"回收站操作失败（错误码 {result}）");
        }
    }

    private void CopyToClipboard(string text, string doneHint)
    {
        // 走 Win32 写入 + 自建重试：WPF 的 Clipboard.SetText 会在“已经写成功”之后
        // 因为 OleFlushClipboard 抢不到剪贴板而抛 CLIPBRD_E_CANT_OPEN，导致误报失败。
        if (ClipboardHelper.TrySetText(text))
        {
            ViewModel?.ShowCopyHint(doneHint);
            return;
        }

        ShowWarning("复制失败：剪贴板正被其他程序占用，请稍后再试。");
    }

    private void RefreshSearch()
    {
        ViewModel?.Search(QueryBox.Text);
    }

    private void ShowWarning(string message)
    {
        RunModal(() => MessageDialog.Info(this, "搜索", message));
    }

    /// <summary>弹窗期间禁止“失焦关闭”，否则弹窗会把搜索窗口一起关掉。</summary>
    private T RunModal<T>(Func<T> action)
    {
        _isModalOpen = true;
        try
        {
            return action();
        }
        finally
        {
            _isModalOpen = false;
        }
    }

    private void RunModal(Action action)
    {
        _isModalOpen = true;
        try
        {
            action();
        }
        finally
        {
            _isModalOpen = false;
        }
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        while (source is not null and not Window)
        {
            if (source is WpfTextBox or WpfListBox or WpfListBoxItem or WpfButton or WpfThumb or WpfScrollBar or MenuItem)
            {
                return true;
            }

            source = GetParent(source);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null and not Window)
        {
            if (source is T match)
            {
                return match;
            }

            source = GetParent(source);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject source)
    {
        if (source is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(source);
        }

        return LogicalTreeHelper.GetParent(source);
    }

    private async void StartEverything_Click(object sender, RoutedEventArgs e)
    {
        // 与自动恢复走同一条逻辑：连接现有实例，或拉起已装/内置的索引进程
        var availability = await EverythingRuntime.EnsureAsync();
        if (availability.Kind == EverythingReadyKind.Failed)
        {
            ShowWarning(availability.Message);
            return;
        }

        if (IsLoaded)
        {
            RefreshSearch();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);
}
