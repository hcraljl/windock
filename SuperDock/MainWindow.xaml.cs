using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Windows.Input;
namespace SuperDock;
public partial class MainWindow : Window
{
    public record Entry(string Name, string Target);
    public record AppGroup(string Name, List<Entry> Entries){public string Icon {get;set;}="工作";public string CustomIcon {get;set;}="";}
    public class Settings
    {
        // 0/absent marks a pre-versioning file; Migrate() upgrades it in place and never discards user data.
        public int SchemaVersion { get; set; }
        // Unknown fields (from a newer or modified config) ride along untouched and are written back on save.
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? ExtraData { get; set; }
        public string Edge { get; set; } = "Bottom";
        public string MonitorId { get; set; } = "";
        public bool MessageRemindersEnabled {get;set;}
        // 消息预览时长（秒）；0 = 一直显示，直到用户点击查看。关闭"一直显示"时恢复 Last 值。
        public int MessagePreviewSeconds {get;set;}=3;
        public int MessagePreviewLastSeconds {get;set;}=3;
        public double MessageReminderOpacity {get;set;}=.65;
        public bool MessagePreviewEnabled {get;set;}
        public Dictionary<string,bool> MessageDetailOverrides {get;set;}=[];
        public List<string> MessageReminderClients {get;set;}=["QQ","微信","企业微信","钉钉","飞书"];
        public bool ShowOnAllMonitors {get;set;}=true;
        public List<string> MonitorIds {get;set;}=[];
        public bool BackgroundBlur { get; set; } = true;
        public bool AmbientShimmer { get; set; } = true;
        public bool Reflection { get; set; } // 仅用于迁移旧配置
        public double ReflectionRatio { get; set; }
        public bool ApplicationCenterFullscreen { get; set; }
        public string ThemeMode { get; set; } = "Light";
        public string AmbientEffect { get; set; } = "Sweep";
        public List<AppGroup> Groups { get; set; } = [];
        public bool HoverWindowPreview { get; set; } = true;
        public bool DockHotkeyEnabled {get;set;}=true;
        public string DockHotkey {get;set;}="Alt+D";
        public bool AutomaticUpdates {get;set;}=true;
        public bool AutoHide { get; set; }
        public bool SmartLayering {get;set;}=true;
        public bool DockDraggable {get;set;}
        public double DockOffset {get;set;}
        public double DockLift {get;set;}
        public bool ShowFileSearch {get;set;}=true;
        public bool FileSearchHotkeyEnabled {get;set;}=true;
        public string FileSearchHotkey {get;set;}="Alt+Space";
        public bool ShowExitButton { get; set; } = true;
        public bool ShowDirectionArrows { get; set; } = true;
        public List<string> DockOrder { get; set; } = [];
        public int WidgetIntervalSeconds { get; set; } = 15;
        // 桌面组件文字颜色（十六进制）；Migrate 统一归一，颜色切换通过共享画刷即时生效。
        public string WidgetColor { get; set; } = "#FFFFFF";
        public bool ClockWidget { get; set; }
        public bool StorageWidget { get; set; }
        public bool WeatherWidget { get; set; }
        public bool PerformanceWidget { get; set; }
        // 截图（默认 F1）与贴图（默认 F3）全局快捷键；与 Dock 显隐/文件搜索快捷键同为 RegisterHotKey 管理，冲突时给出提示。
        public bool ScreenshotHotkeyEnabled { get; set; } = true;
        public string ScreenshotHotkey { get; set; } = "F1";
        public bool PinHotkeyEnabled { get; set; } = true;
        public string PinHotkey { get; set; } = "F3";
        // Alt+F 翻译：框选屏幕文字后识别并中英互译（优先本机 Umi-OCR 接口）。
        public bool TranslateHotkeyEnabled { get; set; } = true;
        public string TranslateHotkey { get; set; } = "Alt+F";
        // 本机 Umi-OCR HTTP 接口地址（参考 Umi-OCR 的默认端口）。
        public string OcrApiUrl { get; set; } = "http://127.0.0.1:1224";
        // 今日待办：未来一周内的待办事项与提醒时间；到点弹窗提醒未完成事项。
        public List<TodoItem> Todos { get; set; } = [];
        public string WeatherCity { get; set; } = "";
        public double WeatherLatitude { get; set; }
        public double WeatherLongitude { get; set; }
        public double DockHeight { get; set; } = 68;
        public double GlassOpacity { get; set; } = .8;
        public double IconSize { get; set; } = 36;
        public double IconSpacing { get; set; } = 5;
        public double HoverScale { get; set; } = 1.9;
        public double HoverRange { get; set; } = 2.5;
        public double HoverSmoothing { get; set; } = .5;
        public bool HoverPauseOnDrag { get; set; }
        public double LaunchBounce { get; set; } = .5;
        public string LaunchEffectColor { get; set; } = "None";
        public string LaunchEffectColorHex { get; set; } = "#5AA8FF";
        public double LaunchEffectDuration { get; set; } = .5;
        public double CornerRadius { get; set; } = 18;
        public List<Entry> Entries { get; set; } = [new("资源管理器", "explorer.exe"), new("设置", "ms-settings:"), new("计算器", "calc.exe")];
    }
    public sealed class TodoItem
    {
        public string Text { get; set; } = "";
        public DateTime RemindAt { get; set; }
        public bool Done { get; set; }
        // 每条待办到达提醒时间只弹窗一次；“稍后提醒”会把 Notified 复位并顺延时间。
        public bool Notified { get; set; }
    }
    readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperDock", "settings.json");
    Settings settings = new();
    internal DockCoordinator? Coordinator {get;set;}
    internal (string Id,Rect Area)? AssignedMonitor {get;set;}
    internal bool HandlesGlobalShortcut {get;}
    internal bool IsDockClosed=>closed;
    internal Rect WorkArea=>SelectedWorkArea();
    internal Rect PhysicalWorkArea=>AssignedMonitor?.Area??DesktopIntegration.Monitors().FirstOrDefault(m=>m.Id==settings.MonitorId).Area;
    readonly StackPanel panel = new();
    readonly HashSet<Button> longPressed = new();
    readonly Border surface;
    readonly ScrollViewer dockScroll = new();
    readonly Border shimmer = new OverlayBorder(){IsHitTestVisible=false,Opacity=0};
    readonly Border reflection = new(){IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    readonly ImageBrush reflectionBrush = new(){Stretch=Stretch.Fill};
    TimeSpan reflectionStamp;
    bool reflectionPending;
    readonly DispatcherTimer shimmerTimer = new(){Interval=TimeSpan.FromSeconds(12)};
    readonly Dictionary<Border, string> runningIndicators = new();
    readonly DispatcherTimer runningTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool refreshingRunning;
    bool closed;
    bool entryRefreshPending;
    AppearanceWindow? appearanceWindow;
    AddApplicationsWindow? addApplicationsWindow;
    WindowPreview? preview;
    Entry? hoverEntry;
    Button? hoverButton;
    readonly Dictionary<Button,(ScaleTransform Scale,TranslateTransform Bounce)> iconTransforms = new();
    readonly Dictionary<Button,Border> launchGlows = new();
    readonly Dictionary<Button,double> magnifyValues = new();
    readonly Dictionary<Button,double> magnifySpeeds = new();
    Point? magnifyCursor;
    bool magnifying;
    TimeSpan magnifyStamp;
    bool dockCandidate;
    bool dockDragging;
    bool suppressClick;
    Point dockPress;
    double dockOrigin;
    double dockLiftOrigin;
    readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    readonly DispatcherTimer closeTimer = new() { Interval = TimeSpan.FromMilliseconds(320) };
    readonly DispatcherTimer hideTimer = new() { Interval = TimeSpan.FromMilliseconds(850) };
    readonly DispatcherTimer foregroundTimer = new() { Interval = TimeSpan.FromMilliseconds(75) };
    IntPtr lastExternalForeground;
    IntPtr clickForeground;
    Entry? draggingEntry;
    bool hidden;
    int openMenus;
    public MainWindow():this(null,true){}
    internal MainWindow(Settings? sharedSettings,bool primary)
    {
        HandlesGlobalShortcut=primary;
        InitializeComponent();
        Icon=Branding.Logo;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        ShowActivated = false;
        runningTimer.Tick += async (_, _) => await RefreshRunning(); runningTimer.Start();
        foregroundTimer.Tick += (_, _) => { UpdateSmartLayer();CheckEdgeReveal();CheckGroupHover();var hwnd = RunningWindows.ExternalForeground(); if (hwnd != IntPtr.Zero) lastExternalForeground = hwnd; };
        lastExternalForeground = RunningWindows.ExternalForeground(); foregroundTimer.Start();
        PreviewMouseLeftButtonDown += (_, e) => { var hwnd = RunningWindows.ExternalForeground(); clickForeground = hwnd != IntPtr.Zero ? hwnd : lastExternalForeground; StartDockDrag(e); };
        PreviewMouseMove += (_, e) => { if (dockCandidate || dockDragging) DragDock(e); };
        panel.SizeChanged += (_, _) => RefreshReflection();
        PreviewMouseLeftButtonUp += (_, _) => EndDockDrag();
        // Capture can drop as soon as the pointer leaves the layered window; DockDragFrame owns the end of the drag.
        SourceInitialized += (_, _) =>
        {
            RunningWindows.PreventActivation(new WindowInteropHelper(this).Handle);
            // Keep the application's foreground state intact until the click is handled.
            // MA_NOACTIVATE delivers the click without activating the Dock itself.
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(MouseActivationHook);HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(ShortcutHook);
            if(HandlesGlobalShortcut)MessageReminders.Attach(this,settings);
        };
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
        settings=sharedSettings??ReadSettings();
        Theme.Apply(settings.ThemeMode);
        Migrate(settings);
        WidgetPaint.Apply(settings.WidgetColor);
        surface = new Border { Background = new SolidColorBrush(Color.FromArgb(242, 32, 37, 49)), BorderBrush = new SolidColorBrush(Color.FromArgb(65,255,255,255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0), Child = panel };
        dockScroll.Content=panel;dockScroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden;dockScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Hidden;surface.Child=null;dockScroll.PreviewMouseWheel+=(_,e)=>{if(panel.Orientation==Orientation.Horizontal)dockScroll.ScrollToHorizontalOffset(dockScroll.HorizontalOffset-e.Delta);else dockScroll.ScrollToVerticalOffset(dockScroll.VerticalOffset-e.Delta);e.Handled=true;};var dockLayers=new Grid{ClipToBounds=false};dockLayers.Children.Add(surface);dockLayers.Children.Add(shimmer);dockLayers.Children.Add(dockScroll);dockLayers.Children.Add(reflection);Content = dockLayers;
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); ShowPreview(); };
        closeTimer.Tick += (_, _) => { if (preview?.IsMouseOver == true || hoverButton?.IsMouseOver == true) return; closeTimer.Stop(); preview?.Close(); preview = null; };
        panel.MouseMove += (_, e) => { if (!settings.HoverPauseOnDrag || (draggingEntry == null && draggingKey == null && !dockDragging && !dockCandidate)) Magnify(e.GetPosition(panel)); };
        panel.MouseLeave += (_, _) => Magnify(null);
        AllowDrop = true;
        Drop += DockDrop;
        DragOver += DockDragOver;
        MouseEnter += (_, _) => { hideTimer.Stop(); hidden = false; Position(); };
        MouseLeave += (_, _) => ScheduleHide();
        hideTimer.Tick += (_, _) => { hideTimer.Stop(); if (settings.AutoHide && settings.DockLift <= 0 && !IsMouseOver && openMenus == 0 && appearanceWindow==null && addApplicationsWindow==null && preview == null) { hidden = true; Position(); } };
        shimmerTimer.Tick+=(_,_)=>PlayShimmer();
        Loaded += (_, _) => { UpdateShortcut();Position(); DesktopIntegration.Blur(this,false); ScheduleHide();shimmerTimer.Start();PlayShimmer();if(HandlesGlobalShortcut){_=WarmApplicationCenter();todoTimer.Tick+=(_,_)=>CheckTodoReminder();todoTimer.Start();Dispatcher.BeginInvoke(new Action(CheckTodoReminder),DispatcherPriority.ApplicationIdle);} };
        DpiChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(Position));
        SizeChanged += (_, _) => Position();
        SystemParameters.StaticPropertyChanged += DisplayChanged;
        Closed += (_, _) => { closed = true;if(entryRefreshPending){entryRefreshPending=false;Save();}UnregisterHotKey(new WindowInteropHelper(this).Handle,0x5144);UnregisterHotKey(new WindowInteropHelper(this).Handle,0x5146);shimmerTimer.Stop();shimmer.BeginAnimation(OpacityProperty,null); runningTimer.Stop(); foregroundTimer.Stop(); hideTimer.Stop(); previewTimer.Stop(); closeTimer.Stop(); todoTimer.Stop(); preview?.Close(); CompositionTarget.Rendering -= MagnifyFrame;CompositionTarget.Rendering -= ReorderFrame;reorderItem=null;magnifying=false; magnifyValues.Clear(); magnifySpeeds.Clear(); SystemParameters.StaticPropertyChanged -= DisplayChanged; };
        Render();
    }
    static string SettingsPath()=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperDock","settings.json");
    static readonly JsonSerializerOptions LoadOptions=new(){ReadCommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true,PropertyNameCaseInsensitive=true};
    internal static Settings ReadSettings()
    {
        var file=SettingsPath();
        if(!File.Exists(file)) return new();
        var loaded=TryLoad(file);
        if(loaded!=null){SnapshotLastGood(file);return loaded;}
        // The live file is unreadable: keep it as evidence, then fall back to the last good snapshot.
        var evidence=BackupDamaged(file);
        ActivityStore.Write("配置读取",message:"settings.json 无法解析，已备份为 "+Path.GetFileName(evidence));
        var lastGood=Path.Combine(Path.GetDirectoryName(file)!,"settings.last-good.json");
        if(File.Exists(lastGood))
        {
            var restored=TryLoad(lastGood);
            if(restored!=null)
            {
                File.Copy(lastGood,file,true);
                ActivityStore.Write("配置读取",message:"已恢复上次正常配置，用户数据未丢失");
                SnapshotLastGood(file);
                return restored;
            }
        }
        // Final attempt: keep every field whose value still matches the expected type.
        var salvaged=Salvage(file);
        if(salvaged!=null){ActivityStore.Write("配置读取",message:"部分字段损坏，已保留其余用户数据");SnapshotLastGood(file);return salvaged;}
        ActivityStore.Write("配置读取",message:"无法读取任何配置，使用默认配置（损坏文件已备份）");
        return new();
    }
    static Settings? TryLoad(string file)
    {
        try{return JsonSerializer.Deserialize<Settings>(File.ReadAllText(file),LoadOptions);}catch(Exception ex){ActivityStore.Write("配置读取",message:"解析失败："+ex.Message,error:ex);return null;}
    }
    static string BackupDamaged(string file)
    {
        var target=file.Replace(".json",$".damaged-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        try{File.Copy(file,target,true);}catch(IOException){}
        return target;
    }
    static void SnapshotLastGood(string file)
    {
        try{File.Copy(file,Path.Combine(Path.GetDirectoryName(file)!,"settings.last-good.json"),true);}catch(IOException){}
    }
    // Type-checked salvage: valid fields survive, broken ones fall back to defaults instead of failing the whole load.
    static Settings? Salvage(string file)
    {
        try
        {
            using var document=JsonDocument.Parse(File.ReadAllText(file));
            var clean=new Dictionary<string,object?>();
            var type=typeof(Settings);
            bool Check(System.Reflection.PropertyInfo property,JsonElement value)
            {
                var t=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
                if(t==typeof(string))return value.ValueKind==JsonValueKind.String;
                if(t==typeof(bool))return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
                if(t==typeof(int)||t==typeof(double)||t==typeof(long))return value.ValueKind==JsonValueKind.Number;
                if(t==typeof(Dictionary<string,bool>))return value.ValueKind==JsonValueKind.Object;
                // Reference-type nullability is erased at runtime, so the erased form matches.
                if(property.PropertyType==typeof(Dictionary<string,System.Text.Json.JsonElement>))return value.ValueKind==JsonValueKind.Object;
                if(t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(List<>))return value.ValueKind==JsonValueKind.Array;
                return value.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
            }
            foreach(var property in document.RootElement.EnumerateObject())
            {
                var target=type.GetProperties().FirstOrDefault(p=>string.Equals(p.Name,property.Name,StringComparison.OrdinalIgnoreCase));
                if(target==null||!Check(target,property.Value))continue;
                clean[property.Name]=property.Value.Clone();
            }
            return JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(clean),LoadOptions);
        }
        catch(Exception ex){ActivityStore.Write("配置读取",message:"字段级恢复失败",error:ex);return null;}
    }
    // Single normalization point: structure repair, range clamps and schema stamping.
    // User values always win over defaults — invalid ones are repaired, valid ones are kept as-is.
    internal static void Migrate(Settings settings)
    {
        settings.Entries ??= []; settings.Groups ??= []; settings.DockOrder ??= []; settings.MonitorIds ??= [];
        settings.MessageReminderClients ??= []; settings.MessageDetailOverrides ??= [];
        if (settings.MessagePreviewSeconds is < 0 or > 5) settings.MessagePreviewSeconds = 3;
        if (settings.MessagePreviewLastSeconds is < 1 or > 5) settings.MessagePreviewLastSeconds = 3;
        if (!new[] { "Bottom", "Top", "Left", "Right" }.Contains(settings.Edge)) settings.Edge = "Bottom";
        if (settings.ThemeMode is not ("Light" or "Dark")) settings.ThemeMode = "Light";
        if (settings.Entries.RemoveAll(entry=>entry==null||string.IsNullOrWhiteSpace(entry.Target))>0) settings.DockOrder.RemoveAll(key=>settings.Entries.All(entry=>entry.Target!=key));
        settings.Groups.RemoveAll(group=>group==null||string.IsNullOrEmpty(group.Name));
        FixNames(settings.Entries);
        foreach (var group in settings.Groups) if (group.Entries != null) FixNames(group.Entries);
        settings.DockHeight = Valid(settings.DockHeight,48,120,68);
        settings.GlassOpacity = Valid(settings.GlassOpacity,0,1,.8);
        settings.IconSize = Valid(settings.IconSize,24,64,36);
        settings.IconSpacing = Valid(settings.IconSpacing,0,16,5);
        settings.HoverScale = Valid(settings.HoverScale,1,2.5,1.9);
        settings.CornerRadius = Valid(settings.CornerRadius,0,32,18);
        settings.WidgetIntervalSeconds = (int)Valid(settings.WidgetIntervalSeconds,1,3600,15);
        if (string.IsNullOrWhiteSpace(settings.WidgetColor) || !System.Text.RegularExpressions.Regex.IsMatch(settings.WidgetColor.Trim(), "^#[0-9A-Fa-f]{6}$")) settings.WidgetColor = "#FFFFFF";
        settings.WidgetColor = settings.WidgetColor.Trim().ToUpperInvariant();
        settings.ScreenshotHotkey = NormalizeFnKey(settings.ScreenshotHotkey, "F1");
        settings.PinHotkey = NormalizeFnKey(settings.PinHotkey, "F3");
        if (settings.ScreenshotHotkey == settings.PinHotkey) settings.PinHotkey = "F3"; // 双键同键时保留截图键，贴图回落默认
        settings.TranslateHotkey = (settings.TranslateHotkey ?? "").Trim() switch { "Ctrl+Shift+F" => "Ctrl+Shift+F", "Alt+F" => "Alt+F", _ => "Alt+F" };
        settings.OcrApiUrl = string.IsNullOrWhiteSpace(settings.OcrApiUrl) ? "http://127.0.0.1:1224" : settings.OcrApiUrl.Trim();
        settings.Todos ??= [];
        settings.Todos.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.Text));
        foreach (var todo in settings.Todos) if (todo.RemindAt.Kind == DateTimeKind.Unspecified) todo.RemindAt = DateTime.SpecifyKind(todo.RemindAt, DateTimeKind.Local);
        settings.DockOffset = double.IsFinite(settings.DockOffset) ? settings.DockOffset : 0;
        settings.DockLift = double.IsFinite(settings.DockLift) && settings.DockLift > 0 ? settings.DockLift : 0;
        settings.ReflectionRatio = double.IsFinite(settings.ReflectionRatio) ? Math.Clamp(settings.ReflectionRatio,0,1) : 0;
        if (settings.Reflection && settings.ReflectionRatio <= 0) settings.ReflectionRatio = .35; // legacy switch -> ratio
        settings.Reflection = false;
        settings.LaunchBounce = double.IsFinite(settings.LaunchBounce) ? Math.Clamp(settings.LaunchBounce,0,1) : .5;
        settings.HoverRange = double.IsFinite(settings.HoverRange) ? Math.Clamp(settings.HoverRange,1,7) : 2.5;
        settings.HoverSmoothing = double.IsFinite(settings.HoverSmoothing) ? Math.Clamp(settings.HoverSmoothing,0,1) : .5;
        settings.LaunchEffectDuration = double.IsFinite(settings.LaunchEffectDuration) ? Math.Clamp(settings.LaunchEffectDuration,0,1) : .5;
        settings.LaunchEffectColor = settings.LaunchEffectColor is "Custom" or "HotTrack" ? settings.LaunchEffectColor : "None";
        settings.LaunchEffectColorHex = string.IsNullOrWhiteSpace(settings.LaunchEffectColorHex) ? "#5AA8FF" : settings.LaunchEffectColorHex;
        settings.SchemaVersion = 2;
        static string NormalizeFnKey(string key,string fallback)
        {
            key=(key??"").Trim().ToUpperInvariant();
            return System.Text.RegularExpressions.Regex.IsMatch(key, "^F([1-9]|1[0-2])$") ? key : fallback;
        }
        static void FixNames(List<Entry> entries)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!string.IsNullOrWhiteSpace(entry.Name)) continue;
                var fallback = System.IO.Path.GetFileNameWithoutExtension(entry.Target);
                entries[index] = entry with { Name = string.IsNullOrWhiteSpace(fallback) ? entry.Target : fallback };
            }
        }
    }
    internal void RefreshDock()=>Render();
    internal void PlaceOverlay(Window overlay,bool fullscreen=false)
    {
        overlay.WindowStartupLocation=WindowStartupLocation.Manual;
        void Place(){
            var physical=PhysicalWorkArea;if(physical.IsEmpty||physical.Width==0)physical=DesktopIntegration.Monitors().FirstOrDefault().Area;if(physical.Width==0)return;
            var dpi=VisualTreeHelper.GetDpi(overlay);double width=physical.Width/dpi.DpiScaleX,height=physical.Height/dpi.DpiScaleY;
            overlay.MinWidth=Math.Min(overlay.MinWidth,Math.Max(1,width-40));overlay.MinHeight=Math.Min(overlay.MinHeight,Math.Max(1,height-40));
            overlay.MaxWidth=fullscreen?width:Math.Max(1,width-40);overlay.MaxHeight=fullscreen?height:Math.Max(1,height-40);
            if(fullscreen){overlay.Width=width;overlay.Height=height;}overlay.UpdateLayout();
            DesktopIntegration.MovePhysical(overlay,physical.Left+(physical.Width-overlay.ActualWidth*dpi.DpiScaleX)/2,physical.Top+(physical.Height-overlay.ActualHeight*dpi.DpiScaleY)/2);
        }
        overlay.Loaded+=(_,_)=>Place();overlay.DpiChanged+=(_,_)=>overlay.Dispatcher.BeginInvoke(new Action(()=>{if(overlay.IsVisible)Place();}));
    }
    async Task WarmApplicationCenter(){try{var apps=await ApplicationCatalog.GetAsync();if(!closed)await Task.WhenAll(apps.Take(36).Select(a=>ShellIcons.LoadAsync(a.Target)));}catch(Exception ex){ActivityStore.Write("应用中心预加载失败",message:ex.Message,error:ex);}}
    void DisplayChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Dispatcher.InvokeAsync(()=>{Position();if(HandlesGlobalShortcut)Coordinator?.SettingsChanged(this);});
    void CheckEdgeReveal()
    {
        if(manuallyHidden||!hidden||!settings.AutoHide||closed||!DesktopIntegration.Cursor(out var physical))return;
        var dpi=VisualTreeHelper.GetDpi(this);var point=new Point(physical.X/dpi.DpiScaleX,physical.Y/dpi.DpiScaleY);var area=SelectedWorkArea();
        bool vertical=settings.Edge is "Left" or "Right";
        bool along=vertical?point.Y>=Top&&point.Y<=Top+ActualHeight:point.X>=Left&&point.X<=Left+ActualWidth;
        bool edge=settings.Edge switch
        {
            "Left"=>point.X>=area.Left-128&&point.X<=area.Left+10,
            "Right"=>point.X>=area.Right-10&&point.X<=area.Right+128,
            "Top"=>point.Y>=area.Top-128&&point.Y<=area.Top+10,
            _=>point.Y>=area.Bottom-10&&point.Y<=area.Bottom+128
        };
        if(along&&edge){hideTimer.Stop();hidden=false;Position();ScheduleHide();}
    }
    void ScheduleHide() { if (settings.AutoHide && settings.DockLift <= 0) { hideTimer.Stop(); hideTimer.Start(); } }
    void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true);Coordinator?.SettingsChanged(this); }
        catch (Exception ex) { ActivityStore.Write("配置保存",message:"保存失败",error:ex);GlassDialog.Show("保存失败：" + ex.Message); }
    }
    void Position()
    {
        var r = SelectedWorkArea();
        bool vertical=settings.Edge is "Left" or "Right";dockScroll.MaxWidth=vertical?SurfaceExtent+surface.Margin.Left+surface.Margin.Right:Math.Max(100,r.Width-16);dockScroll.MaxHeight=vertical?Math.Max(100,r.Height-16):SurfaceExtent+surface.Margin.Top+surface.Margin.Bottom;
        var reserve=surface.Margin;
        dockScroll.Clip=(vertical?dockScroll.ScrollableHeight:dockScroll.ScrollableWidth)<=0?null:vertical?new RectangleGeometry(new Rect(0,reserve.Top,Math.Max(0,dockScroll.ActualWidth),Math.Max(0,dockScroll.ActualHeight-reserve.Top-reserve.Bottom))):new RectangleGeometry(new Rect(reserve.Left,0,Math.Max(0,dockScroll.ActualWidth-reserve.Left-reserve.Right),Math.Max(0,dockScroll.ActualHeight)));
        double left = settings.Edge == "Left" ? r.Left : settings.Edge == "Right" ? r.Right - ActualWidth : r.Left + (r.Width - ActualWidth) / 2;
        double top = settings.Edge == "Top" ? r.Top : settings.Edge == "Bottom" ? r.Bottom - ActualHeight : r.Top + (r.Height - ActualHeight) / 2;
        if (!hidden)
        {
            if (vertical) top += settings.DockOffset; else left += settings.DockOffset;
            // DockLift pulls the dock away from its edge, so it can sit anywhere on the desktop.
            double lift = settings.DockLift;
            if (settings.Edge == "Bottom") top -= lift; else if (settings.Edge == "Top") top += lift; else if (settings.Edge == "Left") left += lift; else left -= lift;
            left = Math.Clamp(left, r.Left, Math.Max(r.Left, r.Right - ActualWidth));
            top = Math.Clamp(top, r.Top, Math.Max(r.Top, r.Bottom - ActualHeight));
        }
        // Keep a six-DIP hit target inside the work area, even when the system taskbar is visible.
        if (hidden) { if (settings.Edge == "Left") left = r.Left - ActualWidth + 6 + surface.Margin.Right; else if (settings.Edge == "Right") left = r.Right - 6 - surface.Margin.Left; else if (settings.Edge == "Top") top = r.Top - ActualHeight + 6 + surface.Margin.Bottom; else top = r.Bottom - 6 - surface.Margin.Top; }
        var dpi=VisualTreeHelper.GetDpi(this);if(!DesktopIntegration.MovePhysical(this,left*dpi.DpiScaleX,top*dpi.DpiScaleY)){Left=left;Top=top;}
    }
    void Launch(string p) => ActivityStore.Launch(p);
    static bool OnButton(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
    void StartDockDrag(MouseButtonEventArgs e)
    {
        if (!settings.DockDraggable || openMenus > 0 || draggingEntry != null || appearanceWindow != null || addApplicationsWindow != null || preview != null) return;
        if (e.ChangedButton != MouseButton.Left) return;
        // Icons own the pointer: dragging one reorders entries (AttachItemDrag). Only the blank
        // glass area starts a dock drag, so the two gestures can no longer collide.
        if (OnButton(e.OriginalSource as DependencyObject)) return;
        if (!DesktopIntegration.Cursor(out var cursor)) return;
        dockCandidate = true; dockDragging = false;
        dockPress = cursor; dockOrigin = settings.DockOffset; dockLiftOrigin = settings.DockLift;
    }
    // The drag only starts once the pointer really moves, so a plain click on an icon still launches it.
    void DragDock(MouseEventArgs e)
    {
        if (!dockCandidate) return;
        // A stale candidate (release swallowed elsewhere) must never turn a plain hover into a drag.
        if (e.LeftButton != MouseButtonState.Pressed) { EndDockDrag(); return; }
        if (!DesktopIntegration.Cursor(out var cursor)) return;
        if (!dockDragging)
        {
            var dpi = VisualTreeHelper.GetDpi(this); bool vertical = settings.Edge is "Left" or "Right";
            if (Math.Abs((cursor.X-dockPress.X)/dpi.DpiScaleX) < 4 && Math.Abs((cursor.Y-dockPress.Y)/dpi.DpiScaleY) < 4) return;
            dockDragging = true;
            hidden = false; hideTimer.Stop(); previewTimer.Stop(); preview?.Close(); preview = null;
            CaptureMouse(); Cursor = Cursors.SizeAll; CompositionTarget.Rendering += DockDragFrame;
        }
        e.Handled = true;
        MoveDock(cursor);
    }
    // Polling keeps the drag alive even when the pointer leaves the layered window and the capture drops.
    void DockDragFrame(object? sender,EventArgs e)
    {
        if (!dockDragging) return;
        if (!DesktopIntegration.PrimaryButton) { EndDockDrag(); return; }
        if (DesktopIntegration.Cursor(out var cursor)) MoveDock(cursor);
    }
    void MoveDock(Point cursor)
    {
        var dpi = VisualTreeHelper.GetDpi(this); bool vertical = settings.Edge is "Left" or "Right";
        double dx = (cursor.X - dockPress.X) / dpi.DpiScaleX, dy = (cursor.Y - dockPress.Y) / dpi.DpiScaleY;
        var area = SelectedWorkArea();
        double alongLimit = vertical ? Math.Max(0,(area.Height-ActualHeight)/2) : Math.Max(0,(area.Width-ActualWidth)/2);
        double liftLimit = vertical ? Math.Max(0,area.Width-ActualWidth) : Math.Max(0,area.Height-ActualHeight);
        double along = dockOrigin + (vertical ? dy : dx);
        double lift = dockLiftOrigin + settings.Edge switch { "Bottom" => -dy, "Top" => dy, "Left" => dx, _ => -dx };
        settings.DockOffset = alongLimit > 0 ? Math.Clamp(along,-alongLimit,alongLimit) : along;
        settings.DockLift = liftLimit > 0 ? Math.Clamp(lift,0,liftLimit) : Math.Max(0,lift);
        Position();
    }
    void EndDockDrag()
    {
        if (!dockCandidate) return;
        bool dragged = dockDragging;
        dockCandidate = false; dockDragging = false;
        CompositionTarget.Rendering -= DockDragFrame;
        Cursor = null; if (IsMouseCaptured) ReleaseMouseCapture();
        if (!dragged) return;
        // Swallow the click that the release would otherwise deliver to the icon.
        suppressClick = true;
        Dispatcher.BeginInvoke(new Action(() => suppressClick = false), DispatcherPriority.Input);
        if (DesktopIntegration.Cursor(out var cursor))
        {
            var dpi = VisualTreeHelper.GetDpi(this); var point = new Point(cursor.X/dpi.DpiScaleX, cursor.Y/dpi.DpiScaleY); var area = SelectedWorkArea();
            string edge = settings.Edge;
            if (point.Y >= area.Top - 16 && point.Y <= area.Top + 48) edge = "Top";
            else if (point.Y >= area.Bottom - 48 && point.Y <= area.Bottom + 16) edge = "Bottom";
            else if (point.X >= area.Left - 16 && point.X <= area.Left + 48) edge = "Left";
            else if (point.X >= area.Right - 48 && point.X <= area.Right + 16) edge = "Right";
            if (edge != settings.Edge) { settings.DockOffset = 0; settings.DockLift = 0; SetEdge(edge); return; }
        }
        Save(); Position(); ScheduleHide();
    }
    Button Make(string title, Action action)
    {
        var scale = new ScaleTransform(1, 1);
        var bounce = new TranslateTransform(0, 0);
        var transform = new TransformGroup(); transform.Children.Add(scale); transform.Children.Add(bounce);
        var b = new Button { Content = title, ToolTip = title, Width = CellSize, Height = CellSize, Margin = settings.Edge is "Left" or "Right" ? new Thickness(0,settings.IconSpacing,0,settings.IconSpacing) : new Thickness(settings.IconSpacing,0,settings.IconSpacing,0), Padding = new Thickness(4), Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 22, RenderTransformOrigin = settings.Edge switch{"Bottom"=>new Point(.5,1),"Top"=>new Point(.5,0),"Left"=>new Point(0,.5),_=>new Point(1,.5)}, RenderTransform = transform, Focusable = false, Cursor = System.Windows.Input.Cursors.Hand };
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12)); border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter); template.VisualTree = border; b.Template = template;
        b.Click += (_, _) => { if (draggingEntry == null && !longPressed.Contains(b) && !suppressClick) action(); };
        iconTransforms[b] = (scale, bounce);
        return b;
    }
    ContextMenu Menu()
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint }; menu.Opened += (_, _) => { openMenus++; hideTimer.Stop(); }; menu.Closed += (_, _) => { openMenus--; ScheduleHide(); }; return menu;
    }
    static void Item(ContextMenu menu, string label, Action action, bool? check = null)
    {
        var item = new MenuItem { Header = label, IsCheckable = check.HasValue, IsChecked = check == true }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    void Add(string[] files)
    {
        foreach (var f in files) if (!settings.Entries.Any(e => e.Target.Equals(f, StringComparison.OrdinalIgnoreCase))) settings.Entries.Add(new(Path.GetFileNameWithoutExtension(f), f));
        Save(); Render();
    }
    void Move(Entry entry, int offset)
    {
        int index = settings.Entries.IndexOf(entry), next = index + offset;
        if (next < 0 || next >= settings.Entries.Count) return;
        settings.Entries.RemoveAt(index); settings.Entries.Insert(next, entry); Save(); Render();
    }
    void Render()
    {
        previewTimer.Stop(); preview?.Close(); preview = null; hidden = false; runningIndicators.Clear();longPressed.Clear(); iconTransforms.Clear(); launchGlows.Clear(); magnifyValues.Clear(); magnifySpeeds.Clear(); panel.Children.Clear(); panel.Orientation = settings.Edge is "Left" or "Right" ? Orientation.Vertical : Orientation.Horizontal;
        ApplySurface();
        bool? previousSystemGroup = null;
        foreach (var e in settings.Entries.ToArray())
        {
            bool systemGroup = IsSystemEntry(e);
            
            previousSystemGroup = systemGroup;
            Button? entryButton = null;
            entryButton = Make(e.Name[..Math.Min(e.Name.Length, 2)], () => { previewTimer.Stop(); preview?.Close(); preview = null; var windows = RunningWindows.Find(e.Target); if (windows.Count > 0){RunningWindows.Toggle(windows, clickForeground);ActivityStore.Write("窗口切换",e.Name,e.Target);} else if(Directory.Exists(e.Target))FolderMenu(e.Target);else {if(entryButton!=null)PlayLaunch(entryButton);ActivityStore.Launch(e.Target,e.Name);} });
            var b = entryButton!; b.ToolTip = e.Name;
            b.Tag="app:"+e.Target;
            b.MouseEnter += (_, _) => { if (draggingEntry != null) return; hoverEntry = e; hoverButton = b; previewTimer.Stop(); if(settings.HoverWindowPreview)previewTimer.Start(); };
            b.MouseLeave += (_, _) => { previewTimer.Stop(); closeTimer.Start(); };
            var icon = ShellIcons.Cached(e.Target);
            if (icon != null) { var image = new Image { Source = icon, Width = EffectiveIconSize, Height = EffectiveIconSize, SnapsToDevicePixels = true }; RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); b.Content = image; }
            else if (e.Target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) b.Content = IconVisual.Settings(true);
            else b.Content = e.Target.StartsWith("shell:") ? "♲" : "▣";
            if(icon==null)_=LoadDockIcon(b,e.Target);
            var iconContent = b.Content is UIElement element ? element : new TextBlock { Text = b.Content?.ToString(), FontSize = 22, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.Content = null; // Detach from the Button before placing the icon in the indicator layout.
            var iconLayout = new Grid { Width = CellSize, Height = CellSize };
            iconLayout.Children.Add(iconContent);
            var indicator = new Border { Width = 14, Height = 1.5, CornerRadius = new CornerRadius(.75), Background = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false, Visibility = Visibility.Hidden };
            iconLayout.Children.Add(indicator); b.Content = iconLayout;
            runningIndicators[indicator] = RunningWindows.WindowTarget(e.Target);
            var menu = Menu();
            Item(menu,"打开应用",()=>Launch(e.Target));
            Item(menu, "退出程序", () => ExitEntry(e));
            if(Path.GetExtension(RunningWindows.WindowTarget(e.Target)).Equals(".exe",StringComparison.OrdinalIgnoreCase))Item(menu,"强制退出…",()=>ForceExitEntry(e));
            Item(menu, "显示窗口", () => WithWindow(e, RunningWindows.Activate));
            Item(menu, "最大化窗口", () => WithWindow(e, RunningWindows.Maximize));
            Item(menu, "最小化窗口", () => WithWindow(e, RunningWindows.Minimize));
            menu.Items.Add(new Separator());
            if(settings.Groups.Count>0){var move=new MenuItem{Header="移动分组"};foreach(var group in settings.Groups){var targetGroup=group;var option=new MenuItem{Header=group.Name,Icon=GroupIcons.Create(group,18)};option.Click+=(_,_)=>{if(!targetGroup.Entries.Any(item=>item.Target==e.Target))targetGroup.Entries.Add(e);settings.Entries.Remove(e);ActivityStore.Write("移动分组",e.Name,e.Target,targetGroup.Name);Save();Render();};move.Items.Add(option);}menu.Items.Add(move);}
            Item(menu, "向前移动", () => Move(e, -1)); Item(menu, "向后移动", () => Move(e, 1)); Item(menu, "移除入口", () => { settings.Entries.Remove(e); Save(); Render(); }); menu.Opened+=(_,_)=>
            {
                string target=RunningWindows.WindowTarget(e.Target);bool hasWindows=RunningWindows.Find(target).Count>0;bool running=hasWindows||RunningWindows.RunningTargets([target]).Contains(target);
                foreach(var action in menu.Items.OfType<MenuItem>())
                {
                    var title=action.Header?.ToString();
                    if(title=="打开应用")action.Visibility=running?Visibility.Collapsed:Visibility.Visible;
                    else if(title=="退出程序")action.Visibility=hasWindows?Visibility.Visible:Visibility.Collapsed;
                    else if(title=="强制退出…")action.Visibility=running?Visibility.Visible:Visibility.Collapsed;
                    else if(title is "显示窗口" or "最大化窗口" or "最小化窗口")action.Visibility=hasWindows?Visibility.Visible:Visibility.Collapsed;
                }
            };
            b.ContextMenu = menu;AttachLongPress(b); panel.Children.Add(b);
        }
        
        foreach(var group in settings.Groups){var groupButton=Make("▦",()=>GroupMenu(group));groupButton.Content=GroupIcons.Create(group);groupButton.ToolTip=group.Name+" · "+group.Entries.Count+" 个应用（停留展开）";groupButton.Tag="group:"+group.Name;var groupMenu=Menu();Item(groupMenu,"移除分组",()=>{settings.Groups.Remove(group);Save();Render();});groupButton.ContextMenu=groupMenu;AttachLongPress(groupButton);panel.Children.Add(groupButton);}
        var library=Make("",()=>ShowApplicationCenter());library.Content=IconVisual.Applications();library.ToolTip="应用中心 · 启动与添加应用";library.Tag="action:library";panel.Children.Add(library);
        if(settings.ShowFileSearch){var search=Make("",ShowFileSearch);search.Content=IconVisual.Search();search.ToolTip="文件搜索 · "+(settings.FileSearchHotkey=="Alt+Space"?"Alt+空格":"Ctrl+Alt+F");search.Tag="action:search";panel.Children.Add(search);}
        panel.Children.Add(new WidgetCarousel(settings,CellSize){Tag="action:widgets"});
        if (settings.ShowDirectionArrows){var direction=DirectionControl();direction.Tag="action:position";panel.Children.Add(direction);}
        var preferences = Make("", OpenAppearance);
        preferences.Content = IconVisual.Settings(false);preferences.Tag="action:settings";
        preferences.ToolTip = "个性化外观与设置";
        panel.Children.Add(preferences);
        if(settings.ShowExitButton){var exit=Make("",ConfirmExit);exit.Content=IconVisual.Exit();exit.Tag="action:exit";exit.ToolTip="退出windock";panel.Children.Add(exit);}
        UpdateShortcut();ApplyDockOrder();Position(); ScheduleHide(); _ = RefreshRunning();ScheduleReflection();
    }
    async Task LoadDockIcon(Button button,string target)
    {
        var icon=await ShellIcons.LoadAsync(target);
        if(icon==null)return;
        await Dispatcher.InvokeAsync(()=>{
            if(closed||!panel.Children.Contains(button)||button.Content is not Grid layout||layout.Children.Count==0)return;
            layout.Children.RemoveAt(0);var image=new Image{Source=icon,Width=EffectiveIconSize,Height=EffectiveIconSize,SnapsToDevicePixels=true};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);layout.Children.Insert(0,image);
            // Icons arrive asynchronously; refresh the mirror once they are on screen.
            ScheduleReflection();
        },DispatcherPriority.Background);
    }
    static IntPtr MouseActivationHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return new IntPtr(3); }
        return IntPtr.Zero;
    }
    void PlayShimmer()
    {
        shimmer.BeginAnimation(OpacityProperty,null);shimmer.Opacity=0;shimmer.Child=null;shimmer.Background=null;shimmer.BorderBrush=null;shimmer.BorderThickness=new Thickness(0);
        if(closed||hidden||!settings.AmbientShimmer||settings.AmbientEffect=="None"||!SystemParameters.ClientAreaAnimation||surface.ActualWidth<1||surface.ActualHeight<1)return;
        bool vertical=settings.Edge is "Left" or "Right";
        double width=surface.ActualWidth,height=surface.ActualHeight;
        shimmer.Clip=new RectangleGeometry(new Rect(0,0,width,height),settings.CornerRadius,settings.CornerRadius);
        var duration=TimeSpan.FromSeconds(4);
        if(settings.AmbientEffect=="Breathe")
        {
            var glow=new RadialGradientBrush();glow.GradientStops.Add(new GradientStop(Color.FromArgb(38,155,216,255),0));glow.GradientStops.Add(new GradientStop(Colors.Transparent,1));shimmer.Background=glow;
        }
        else if(settings.AmbientEffect=="Edge")
        {
            var edge=new LinearGradientBrush(Color.FromArgb(0,120,201,255),Color.FromArgb(160,180,229,255),45);edge.GradientStops.Insert(1,new GradientStop(Color.FromArgb(15,120,201,255),.35));
            shimmer.BorderThickness=new Thickness(1.5);shimmer.BorderBrush=edge;
        }
        else
        {
            // Move a cached visual with a render transform; do not repaint the gradient each frame.
            double span=vertical?height:width;
            double band=Math.Clamp(span*.18,65,140);
            var brush=new LinearGradientBrush{MappingMode=BrushMappingMode.Absolute,StartPoint=new Point(0,0),EndPoint=vertical?new Point(32,band):new Point(band,32)};
            brush.GradientStops.Add(new GradientStop(Colors.Transparent,0));brush.GradientStops.Add(new GradientStop(Color.FromArgb(8,172,221,255),.2));brush.GradientStops.Add(new GradientStop(Color.FromArgb(65,241,250,255),.5));brush.GradientStops.Add(new GradientStop(Color.FromArgb(8,172,221,255),.8));brush.GradientStops.Add(new GradientStop(Colors.Transparent,1));brush.Freeze();
            var movement=new TranslateTransform();
            var light=new System.Windows.Shapes.Rectangle{Width=vertical?width:band+height,Height=vertical?band+width:height,Fill=brush,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,RenderTransform=movement,CacheMode=new BitmapCache()};
            shimmer.Child=new Grid{Children={light}};
            var travel=new DoubleAnimation(-(band+(vertical?width:height)),span+band,duration);
            movement.BeginAnimation(vertical?TranslateTransform.YProperty:TranslateTransform.XProperty,travel);
        }
        var opacity=new DoubleAnimationUsingKeyFrames();opacity.KeyFrames.Add(new SplineDoubleKeyFrame(0,TimeSpan.Zero));opacity.KeyFrames.Add(new SplineDoubleKeyFrame(1,TimeSpan.FromSeconds(1),new KeySpline(.25,0,.25,1)));opacity.KeyFrames.Add(new SplineDoubleKeyFrame(1,TimeSpan.FromSeconds(3)));opacity.KeyFrames.Add(new SplineDoubleKeyFrame(0,duration,new KeySpline(.25,0,.25,1)));shimmer.BeginAnimation(OpacityProperty,opacity);
    }    void Magnify(Point? cursor)
    {
        magnifyCursor = cursor;
        if (!magnifying && (!cursor.HasValue || settings.HoverScale <= 1)) return;
        if (!magnifying) { magnifying = true; magnifyStamp = TimeSpan.Zero; CompositionTarget.Rendering += MagnifyFrame; }
        MagnifyFrame(null, EventArgs.Empty);
    }
    // A short, lightly damped spring keeps the zoom glued to the cursor; restarting keyframe
    // animations on every mouse move was what made the old bounce stutter.
    void MagnifyFrame(object? sender, EventArgs args)
    {
        double dt = args is RenderingEventArgs tick && magnifyStamp != TimeSpan.Zero ? (tick.RenderingTime - magnifyStamp).TotalSeconds : 1/60.0;
        TimeSpan now = args is RenderingEventArgs stamp ? stamp.RenderingTime : magnifyStamp;
        if (args is RenderingEventArgs) magnifyStamp = now;
        dt = Math.Clamp(dt,.001,.05);
        bool vertical = settings.Edge is "Left" or "Right";
        double peak = Math.Max(1,settings.HoverScale);
        double reach = Math.Max(28,CellSize*Math.Clamp(settings.HoverRange,1,7));
        double stiffness = 180 + 420*Math.Clamp(settings.HoverSmoothing,0,1);
        double damping = 2*Math.Sqrt(stiffness)*.72;
        bool settled = true;
        foreach (var b in panel.Children.OfType<Button>())
        {
            var center = b.TranslatePoint(new Point(b.ActualWidth/2,b.ActualHeight/2),panel);
            double influence = magnifyCursor.HasValue ? Math.Max(0,1-Math.Abs(vertical ? center.Y-magnifyCursor.Value.Y : center.X-magnifyCursor.Value.X)/reach) : 0;
            double target = 1 + (peak-1) * influence * influence;
            magnifyValues.TryGetValue(b,out double value); if (!double.IsFinite(value) || value <= 0) value = 1;
            magnifySpeeds.TryGetValue(b,out double speed); if (!double.IsFinite(speed)) speed = 0;
            speed = (speed + (target-value) * stiffness * dt) * Math.Exp(-damping * dt);
            value += speed * dt;
            if (!double.IsFinite(value) || value < 1) { value = 1; if (speed < 0) speed = 0; }
            magnifyValues[b] = value; magnifySpeeds[b] = speed;
            if (!iconTransforms.TryGetValue(b,out var layers)) continue;
            layers.Scale.ScaleX = value; layers.Scale.ScaleY = value;
            Panel.SetZIndex(b,(int)((value-1)*140));
            if (Math.Abs(target-value) > .002 || Math.Abs(speed) > .02) settled = false;
        }
        if (settings.ReflectionRatio > 0 && (!magnifyCursor.HasValue || settled || (now - reflectionStamp).TotalMilliseconds > 40)) { reflectionStamp = now; RefreshReflection(); }
        if (!magnifyCursor.HasValue && settled) { CompositionTarget.Rendering -= MagnifyFrame; magnifying = false; magnifyStamp = TimeSpan.Zero; magnifyValues.Clear(); magnifySpeeds.Clear(); }
    }
    // Launch feedback: an optional coloured halo plus a hop away from the dock edge.
    void PlayLaunch(Button button)
    {
        if (!iconTransforms.TryGetValue(button,out var layers)) return;
        double duration = Math.Clamp(settings.LaunchEffectDuration,0,1);
        double total = 260+640*duration;
        bool vertical = settings.Edge is "Left" or "Right";
        double sign = settings.Edge switch{"Bottom"=>-1,"Top"=>1,"Left"=>1,_=>-1};
        if (LaunchAccent(button) is Color paint && GlowFor(button) is Border glow)
        {
            var brush = new RadialGradientBrush(Color.FromArgb(190,paint.R,paint.G,paint.B),Color.FromArgb(0,paint.R,paint.G,paint.B)) { MappingMode = BrushMappingMode.RelativeToBoundingBox, RadiusX = .62, RadiusY = .62, Center = new Point(.5,.5), GradientOrigin = new Point(.5,.5) };
            glow.Background = brush;
            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new SplineDoubleKeyFrame(0,TimeSpan.Zero));
            fade.KeyFrames.Add(new SplineDoubleKeyFrame(.95,TimeSpan.FromMilliseconds(total*.22),new KeySpline(.2,.8,.3,1)));
            fade.KeyFrames.Add(new SplineDoubleKeyFrame(.55,TimeSpan.FromMilliseconds(total*.55),new KeySpline(.4,0,.6,1)));
            fade.KeyFrames.Add(new SplineDoubleKeyFrame(0,TimeSpan.FromMilliseconds(total),new KeySpline(.4,0,.7,1)));
            glow.BeginAnimation(OpacityProperty,fade);
            if (glow.RenderTransform is ScaleTransform stretch)
            {
                var grow = new DoubleAnimation(.8,1.15,TimeSpan.FromMilliseconds(total)){ EasingFunction = new CubicEase{EasingMode=EasingMode.EaseOut} };
                stretch.BeginAnimation(ScaleTransform.ScaleXProperty,grow); stretch.BeginAnimation(ScaleTransform.ScaleYProperty,grow);
            }
        }
        double amount = Math.Clamp(settings.LaunchBounce,0,1);
        if (amount <= 0) return;
        double distance = EffectiveIconSize*.45*amount;
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(sign*distance,TimeSpan.FromMilliseconds(total*.19),new KeySpline(.15,.9,.3,1)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(0,TimeSpan.FromMilliseconds(total*.52),new KeySpline(.5,0,.6,1)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(sign*distance*.32,TimeSpan.FromMilliseconds(total*.73),new KeySpline(.15,.9,.3,1)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(0,TimeSpan.FromMilliseconds(total),new KeySpline(.5,0,.6,1)));
        layers.Bounce.BeginAnimation(vertical?TranslateTransform.XProperty:TranslateTransform.YProperty,animation);
    }
    Border? GlowFor(Button button)
    {
        if (button.Content is not Grid layout) return null;
        if (launchGlows.TryGetValue(button,out var cached) && layout.Children.Contains(cached)) return cached;
        var glow = new Border { Width = CellSize*1.7, Height = CellSize*1.7, CornerRadius = new CornerRadius(CellSize), Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5,.5), RenderTransform = new ScaleTransform(.8,.8) };
        layout.Children.Insert(0,glow); launchGlows[button] = glow; return glow;
    }
    // Hot-Track samples the icon so the halo matches the artwork instead of a fixed accent.
    Color? LaunchAccent(Button button)
    {
        if (settings.LaunchEffectColor == "Custom")
        {
            try { if (ColorConverter.ConvertFromString(settings.LaunchEffectColorHex) is Color custom) return custom; } catch { }
        }
        else if (settings.LaunchEffectColor == "HotTrack" && button.Content is Grid layout && Dominant(layout) is Color sample) return sample;
        return null;
    }
    static Color? Dominant(Visual root)
    {
        var image = Descendants(root).OfType<Image>().FirstOrDefault()?.Source as BitmapSource;
        if (image == null || image.PixelWidth == 0) return null;
        try
        {
            var scaled = new TransformedBitmap(image,new ScaleTransform(12.0/image.PixelWidth,12.0/image.PixelHeight));
            var converted = new FormatConvertedBitmap(scaled,PixelFormats.Bgra32,null,0);
            int stride = converted.PixelWidth*4; var pixels = new byte[stride*converted.PixelHeight]; converted.CopyPixels(pixels,stride,0);
            long r=0,g=0,b=0; int strong=0,all=0;
            for (int i=0;i+3<pixels.Length;i+=4)
            {
                byte alpha=pixels[i+3]; if (alpha<128) continue;
                byte blue=pixels[i],green=pixels[i+1],red=pixels[i+2];
                int max=Math.Max(red,Math.Max(green,blue)),min=Math.Min(red,Math.Min(green,blue));
                all++; if (max-min<44||max<48) continue;
                r+=red; g+=green; b+=blue; strong++;
            }
            if (strong>0) return Color.FromRgb((byte)(r/strong),(byte)(g/strong),(byte)(b/strong));
            if (all>0) return Color.FromRgb((byte)(r/all),(byte)(g/all),(byte)(b/all));
        }
        catch { }
        return null;
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i=0;i<count;i++){ var child=VisualTreeHelper.GetChild(root,i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    void ShowPreview()
    {
        if (!settings.HoverWindowPreview || draggingEntry != null || hoverEntry == null || hoverButton?.IsMouseOver != true) return;
        preview?.Close(); preview = null;
        var windows = RunningWindows.Find(hoverEntry.Target);
        if (windows.Count == 0) return;
        var previewOwner=hoverButton;ToolTipService.SetIsEnabled(previewOwner,false);var window = new WindowPreview(this,windows); preview = window;
        window.Closed += (_, _) => {ToolTipService.SetIsEnabled(previewOwner,true); if (preview == window) preview = null; ScheduleHide(); };
        window.MouseEnter += (_, _) => closeTimer.Stop();
        window.MouseLeave += (_, _) => closeTimer.Start();
        hideTimer.Stop(); window.Show(); window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var point = hoverButton.PointToScreen(new Point(hoverButton.ActualWidth/2,hoverButton.ActualHeight/2));
        point = new Point(point.X/dpi.DpiScaleX,point.Y/dpi.DpiScaleY);
        var r = SelectedWorkArea();
        double left = point.X-window.ActualWidth/2, top = point.Y-window.ActualHeight-45;
        if(settings.Edge=="Top") top=point.Y+45;
        if(settings.Edge=="Left") { left=point.X+45; top=point.Y-window.ActualHeight/2; }
        if(settings.Edge=="Right") { left=point.X-window.ActualWidth-45; top=point.Y-window.ActualHeight/2; }
        window.Left=Math.Clamp(left,r.Left,Math.Max(r.Left,r.Right-window.ActualWidth));
        window.Top=Math.Clamp(top,r.Top,Math.Max(r.Top,r.Bottom-window.ActualHeight));
    }
    void ShowApplicationCenter()
    {
        if(addApplicationsWindow!=null){addApplicationsWindow.Close();}
        hideTimer.Stop();previewTimer.Stop();preview?.Close();preview=null;
        addApplicationsWindow=new AddApplicationsWindow(settings,AddEntry,true){Owner=this,Changed=()=>{Save();Render();}};openMenus++;
        addApplicationsWindow.Closed+=(_,_)=>{addApplicationsWindow=null;openMenus--;ScheduleHide();};PlaceOverlay(addApplicationsWindow,settings.ApplicationCenterFullscreen);addApplicationsWindow.Show();
    }
    void ShowAddMenu(Button anchor)
    {
        if(addApplicationsWindow!=null){addApplicationsWindow.Activate();return;}
        previewTimer.Stop();preview?.Close();preview=null;hideTimer.Stop();
        addApplicationsWindow=new AddApplicationsWindow(settings,AddEntry){Owner=this};openMenus++;
        addApplicationsWindow.Closed+=(_,_)=>{addApplicationsWindow=null;openMenus--;ScheduleHide();};PlaceOverlay(addApplicationsWindow);addApplicationsWindow.Show();
    }    void AttachLongPress(Button button)
    {
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(550)};
        Point start=default;
        void Cancel()=>timer.Stop();
        button.PreviewMouseLeftButtonDown+=(_,e)=>{longPressed.Remove(button);start=e.GetPosition(panel);timer.Stop();timer.Start();};
        button.PreviewMouseMove+=(_,e)=>
        {
            var point=e.GetPosition(panel);
            if(e.LeftButton!=MouseButtonState.Pressed||Math.Abs(point.X-start.X)>=SystemParameters.MinimumHorizontalDragDistance||Math.Abs(point.Y-start.Y)>=SystemParameters.MinimumVerticalDragDistance)Cancel();
        };
        button.PreviewMouseLeftButtonUp+=(_,e)=>{Cancel();if(longPressed.Contains(button))e.Handled=true;};
        button.MouseLeave+=(_,_)=>Cancel();
        button.LostMouseCapture+=(_,_)=>Cancel();
        button.Unloaded+=(_,_)=>{Cancel();longPressed.Remove(button);};
        timer.Tick+=(_,_)=>
        {
            Cancel();
            if(Mouse.LeftButton!=MouseButtonState.Pressed||!button.IsMouseOver||draggingEntry!=null||dockDragging||button.ContextMenu==null)return;
            longPressed.Add(button);previewTimer.Stop();closeTimer.Stop();preview?.Close();preview=null;hideTimer.Stop();Magnify(null);
            button.ReleaseMouseCapture();
            var menu=button.ContextMenu;menu.PlacementTarget=button;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;menu.IsOpen=true;
        };
    }
    string? draggingKey;
    Button? reorderItem;
    void ApplyDockOrder()
    {
        var items=panel.Children.OfType<FrameworkElement>().OrderBy(item=>{int i=settings.DockOrder.IndexOf(item.Tag as string??"");return i<0?int.MaxValue:i;}).ToArray();panel.Children.Clear();foreach(var item in items){panel.Children.Add(item);AttachItemDrag(item);}
    }
    // Dragging an icon reorders it with a live swap: a frame loop polls the primary button and
    // the cursor (the same proven pattern as DockDragFrame), slides the icon across neighbours and
    // persists the new visual order on release. OLE DoDragDrop is unreliable on layered windows.
    void AttachItemDrag(FrameworkElement item)
    {
        Point start=default;bool down=false;
        item.PreviewMouseLeftButtonDown+=(_,e)=>{start=e.GetPosition(panel);down=true;};item.PreviewMouseLeftButtonUp+=(_,_)=>down=false;
        item.PreviewMouseMove+=(_,e)=>
        {
            if(!down||e.LeftButton!=MouseButtonState.Pressed||draggingKey!=null||item is Button button&&longPressed.Contains(button))return;
            var point=e.GetPosition(panel);
            if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            down=false;
            if(item.Tag is not string tag)return;
            StartReorder(item,tag);e.Handled=true;
        };
    }
    void StartReorder(FrameworkElement item,string tag)
    {
        draggingKey=tag;reorderItem=item as Button;
        draggingEntry=new Entry("","");previewTimer.Stop();preview?.Close();preview=null;hideTimer.Stop();
        if(reorderItem!=null){reorderItem.Opacity=.45;reorderItem.Cursor=Cursors.Hand;}
        CompositionTarget.Rendering+=ReorderFrame;
    }
    void ReorderFrame(object? sender,EventArgs e)
    {
        if(reorderItem==null||!DesktopIntegration.PrimaryButton){EndReorder();return;}
        if(!DesktopIntegration.Cursor(out var cursor))return;
        var dpi=VisualTreeHelper.GetDpi(this);
        Point point;
        try{point=panel.PointFromScreen(new Point(cursor.X,cursor.Y));}catch{return;}
        bool vertical=settings.Edge is "Left" or "Right";
        var items=panel.Children.OfType<FrameworkElement>().Where(i=>i.Tag is string).ToList();
        var hit=items.FirstOrDefault(i=>{
            var topleft=i.TranslatePoint(new Point(0,0),panel);
            return vertical?point.Y>=topleft.Y&&point.Y<topleft.Y+i.ActualHeight:point.X>=topleft.X&&point.X<topleft.X+i.ActualWidth;
        });
        if(hit!=null&&!Equals(hit,reorderItem))
        {
            var to=panel.Children.IndexOf(hit);
            panel.Children.Remove(reorderItem);panel.Children.Insert(to,reorderItem);
            UpdateLayout();
        }
    }
    void EndReorder()
    {
        CompositionTarget.Rendering-=ReorderFrame;
        var item=reorderItem;reorderItem=null;
        if(item!=null)
        {
            item.Opacity=1;item.Cursor=null;
            var order=panel.Children.OfType<FrameworkElement>().Where(i=>i.Tag is string).Select(i=>(string)i.Tag).ToList();
            if(!order.SequenceEqual(settings.DockOrder)){settings.DockOrder=order;Save();}
        }
        draggingKey=null;
        Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{draggingEntry=null;ScheduleHide();}));
    }
    const string ReorderFormat = "SuperDock.Entry.Reorder";
    void AttachReorder(Button button, Entry entry)
    {
        Point start = default;
        button.PreviewMouseLeftButtonDown += (_, e) => start = e.GetPosition(panel);
        button.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || draggingEntry != null || longPressed.Contains(button)) return;
            var point = e.GetPosition(panel);
            if (Math.Abs(point.X-start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y-start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            previewTimer.Stop(); closeTimer.Stop(); preview?.Close(); preview = null; hideTimer.Stop();
            draggingEntry = entry; button.Opacity = .45; Magnify(null);
            try { DragDrop.DoDragDrop(button, new DataObject(ReorderFormat, entry.Target), DragDropEffects.Move); }
            finally
            {
                button.Opacity = 1;
                foreach (var item in panel.Children.OfType<Button>()) item.Opacity = 1;
                // Keep the drag release from also launching or minimizing the program.
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { draggingEntry = null; ScheduleHide(); }));
            }
            e.Handled = true;
        };
    }
    int DropIndex(Point point)
    {
        bool vertical = settings.Edge is "Left" or "Right";
        int index = 0;
        foreach (var button in panel.Children.OfType<Button>().Take(settings.Entries.Count))
        {
            var center = button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2), panel);
            if ((vertical ? point.Y < center.Y : point.X < center.X)) return index;
            index++;
        }
        return index;
    }
    void DockDragOver(object sender, DragEventArgs e)
    {
        if (draggingEntry != null && e.Data.GetDataPresent(ReorderFormat))
        {
            e.Effects = DragDropEffects.Move;
            int index = DropIndex(e.GetPosition(panel));
            var buttons = panel.Children.OfType<Button>().Take(settings.Entries.Count).ToArray();
            for (int i=0;i<buttons.Length;i++) buttons[i].Opacity = settings.Entries[i] == draggingEntry ? .45 : i == Math.Min(index,buttons.Length-1) ? .65 : 1;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; }
    }
    void DockDrop(object sender, DragEventArgs e)
    {
        if (draggingEntry != null && e.Data.GetDataPresent(ReorderFormat))
        {
            int oldIndex = settings.Entries.IndexOf(draggingEntry);
            int next = DropIndex(e.GetPosition(panel));
            if (oldIndex >= 0)
            {
                settings.Entries.RemoveAt(oldIndex);
                if (next > oldIndex) next--;
                settings.Entries.Insert(Math.Clamp(next,0,settings.Entries.Count),draggingEntry);
                Save(); Render();
            }
            e.Effects = DragDropEffects.Move; e.Handled = true;
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files) { Add(files); e.Handled = true; }
    }
    void WithWindow(Entry entry, Action<IntPtr> action)
    {
        previewTimer.Stop(); preview?.Close(); preview = null;
        var window = RunningWindows.Find(entry.Target).FirstOrDefault();
        if (window != null) action(window.Handle);
    }
    static bool IsSystemEntry(Entry entry) => entry.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
        || entry.Target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)
        || Directory.Exists(entry.Target);
    void AddDivider()
    {
        bool vertical = settings.Edge is "Left" or "Right";
        panel.Children.Add(new Border
        {
            Width = vertical ? 34 : 1,
            Height = vertical ? 1 : 30,
            Background = new SolidColorBrush(Color.FromArgb(90, 220, 229, 245)),
            Margin = vertical ? new Thickness(0,8,0,8) : new Thickness(9,0,9,0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        });
    }
    async Task RefreshRunning()
    {
        if (refreshingRunning || closed) return;
        refreshingRunning = true;
        try
        {
            var targets = runningIndicators.Values.ToArray();
            var foreground = RunningWindows.ExternalForeground();
            if (foreground == IntPtr.Zero) foreground = lastExternalForeground;
            var states = await RunningWindows.StatesAsync(targets, foreground);
            if (closed) return;
            foreach (var (indicator,target) in runningIndicators)
            {
                var state = states.GetValueOrDefault(target, AppState.Stopped);
                indicator.Visibility = state == AppState.Stopped ? Visibility.Hidden : Visibility.Visible;
                indicator.Background = state switch
                {
                    AppState.Foreground => new SolidColorBrush(Color.FromRgb(77, 222, 135)),
                    AppState.Minimized => new SolidColorBrush(Color.FromRgb(84, 163, 255)),
                    AppState.BackgroundProcess => new SolidColorBrush(Color.FromRgb(232, 182, 94)),
                    _ => new SolidColorBrush(Color.FromRgb(179, 189, 204))
                };
            }
        }
        finally { refreshingRunning = false; ScheduleReflection(); }
    }
    async void ForceExitEntry(Entry entry)
    {
        previewTimer.Stop();preview?.Close();preview=null;
        openMenus++;hideTimer.Stop();
        try
        {
            if(!GlassDialog.Confirm($"将结束「{entry.Name}」的所有匹配进程。\n未保存的内容可能丢失。","强制退出","强制退出",true))return;
            var result=await Task.Run(()=>RunningWindows.ForceExit(entry.Target));ActivityStore.Write("强制退出",entry.Name,entry.Target,$"终止请求 {result.Ended}，失败 {result.Failed}");
            if(result.Failed>0)GlassDialog.Show($"已发送 {result.Ended} 个进程的终止请求，另有 {result.Failed} 个无法结束（可能需要管理员权限）。");
            else if(result.Ended==0)GlassDialog.Show("没有找到对应的运行进程。");
            await RefreshRunning();
        }
        finally{openMenus--;ScheduleHide();}
    }
    void ExitEntry(Entry entry)
    {
        previewTimer.Stop(); preview?.Close(); preview = null;
        if (!RunningWindows.RequestExit(entry.Target))
            GlassDialog.Show("未找到可正常关闭的程序窗口。若程序仅在托盘运行，请从托盘菜单退出。", "SuperDock");
        _ = RefreshRunning();
    }
    static double Valid(double value,double min,double max,double fallback) => double.IsFinite(value) ? Math.Clamp(value,min,max) : fallback;
    double EffectiveIconSize => Math.Min(settings.IconSize, settings.DockHeight - 14);
    double CellSize => Math.Min(settings.DockHeight-12,Math.Max(30, EffectiveIconSize + 8));
    double ReflectionBand => settings.ReflectionRatio > 0 ? Math.Max(2,Math.Round(Math.Clamp(settings.ReflectionRatio,0,1) * CellSize)) : 0;
    double SurfaceExtent => settings.DockHeight + ReflectionBand;
    void ApplySurface()
    {
        bool vertical = settings.Edge is "Left" or "Right";
        panel.Width=vertical?CellSize:double.NaN;
        double padding = Math.Max(4,(settings.DockHeight - CellSize)/2);
        double band = ReflectionBand;
        surface.Padding = vertical
            ? new Thickness(padding+(settings.Edge=="Left"?band:0),12,padding+(settings.Edge=="Right"?band:0),12)
            : new Thickness(12,padding+(settings.Edge=="Top"?band:0),12,padding+(settings.Edge=="Bottom"?band:0));
        // Reserve transparent viewport space for transformed icons, independently of the glass body.
        double room=Math.Ceiling((CellSize+8)*(settings.HoverScale-1))+12;
        var reserve=settings.Edge switch{"Bottom"=>new Thickness(room,room,room,0),"Top"=>new Thickness(room,0,room,room),"Left"=>new Thickness(0,room,room,room),_=>new Thickness(room,room,0,room)};
        surface.Margin=reserve;shimmer.Margin=reserve;shimmer.CornerRadius=new CornerRadius(settings.CornerRadius);if(!settings.AmbientShimmer){shimmer.BeginAnimation(OpacityProperty,null);shimmer.Opacity=0;}
        var inset=surface.Padding;
        dockScroll.Padding=new Thickness(0);panel.Margin=new Thickness(reserve.Left+inset.Left,reserve.Top+inset.Top,reserve.Right+inset.Right,reserve.Bottom+inset.Bottom);
        surface.Width=vertical?SurfaceExtent:double.NaN;surface.Height=vertical?double.NaN:SurfaceExtent;
        reflection.Visibility=band>0?Visibility.Visible:Visibility.Collapsed;
        if(band>0)
        {
            // The mirror lives in the extra band facing the screen edge; the icons keep their own row.
            reflection.Margin=settings.Edge switch
            {
                "Top"=>new Thickness(reserve.Left+inset.Left,reserve.Top+padding,reserve.Right+inset.Right,reserve.Bottom+inset.Bottom+CellSize),
                "Left"=>new Thickness(reserve.Left+padding,reserve.Top+inset.Top,reserve.Right+inset.Right+CellSize,reserve.Bottom+inset.Bottom),
                "Right"=>new Thickness(reserve.Left+inset.Left+CellSize,reserve.Top+inset.Top,reserve.Right+padding,reserve.Bottom+inset.Bottom),
                _=>new Thickness(reserve.Left+inset.Left,reserve.Top+inset.Top+CellSize,reserve.Right+inset.Right,reserve.Bottom+padding)
            };
            reflection.CornerRadius=new CornerRadius(Math.Max(0,settings.CornerRadius-2));
            reflection.Background=reflectionBrush;
            reflectionBrush.RelativeTransform=null;
            var fade=new LinearGradientBrush{StartPoint=settings.Edge switch{"Top"=>new Point(0,1),"Left"=>new Point(1,0),_=>new Point(0,0)},EndPoint=settings.Edge switch{"Top"=>new Point(0,0),"Left"=>new Point(0,0),"Right"=>new Point(1,0),_=>new Point(0,1)}};
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(170,255,255,255),0));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(42,255,255,255),.55));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),1));
            reflection.OpacityMask=fade;
        }
        else { reflection.Background=null; reflectionBrush.ImageSource=null; }
        surface.CornerRadius = new CornerRadius(settings.CornerRadius);RoundClip.SetRadius(surface,settings.CornerRadius);
        surface.Cursor=settings.DockDraggable?Cursors.SizeAll:null;
        var fill = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,1) };
        // A floor of 1/255 keeps the surface hit-testable (layered windows drop clicks on fully
        // transparent pixels) while staying visually fully transparent at 0%.
        byte alpha = Math.Max((byte)1, (byte)(settings.GlassOpacity * 255));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,67,80,105),0));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,28,37,52),.55));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,46,61,82),1));
        surface.Background = fill;
        if(IsLoaded){DesktopIntegration.Blur(this,false);Dispatcher.BeginInvoke(new Action(PlayShimmer));}
        // At 0% opacity the whole glass disappears: only the icons remain visible.
        surface.BorderBrush = alpha <= 1 ? null : (Brush)FindResource("GlassStroke");
    }
    // Snapshot the icon row by rendering its container and cropping the row rect: rendering the
    // row itself yields an empty bitmap (layout offset). The mirror is then flipped and cut to the
    // band at 1:1 scale, so it never squeezes the whole row into the visible slice.
    void RefreshReflection()
    {
        double band=ReflectionBand;
        if(settings.ReflectionRatio<=0||!IsLoaded||band<1||panel.ActualWidth<1||panel.ActualHeight<1||dockScroll.ActualWidth<1||dockScroll.ActualHeight<1){reflectionBrush.ImageSource=null;return;}
        var dpi=VisualTreeHelper.GetDpi(this);double scale=dpi.DpiScaleX;bool vertical=settings.Edge is "Left" or "Right";
        int width=Math.Max(1,(int)Math.Ceiling(dockScroll.ActualWidth*scale)),height=Math.Max(1,(int)Math.Ceiling(dockScroll.ActualHeight*scale));
        var frame=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32);
        frame.Render(dockScroll);
        var row=panel.TransformToAncestor(dockScroll).TransformBounds(new Rect(0,0,panel.ActualWidth,panel.ActualHeight));
        int x=Math.Max(0,(int)Math.Round(row.X*scale)),y=Math.Max(0,(int)Math.Round(row.Y*scale));
        int rowWidth=Math.Min(width-x,(int)Math.Max(1,Math.Round(row.Width*scale))),rowHeight=Math.Min(height-y,(int)Math.Max(1,Math.Round(row.Height*scale)));
        if(rowWidth<1||rowHeight<1){reflectionBrush.ImageSource=null;return;}
        var rowBitmap=new CroppedBitmap(frame,new Int32Rect(x,y,rowWidth,rowHeight));
        // Slide the mirrored row so the edge touching the icons lands on the band edge that faces them.
        double offsetX=settings.Edge=="Left"?band-row.Width:0,offsetY=settings.Edge=="Top"?band-row.Height:0;
        var mirror=new DrawingVisual();
        using(var dc=mirror.RenderOpen())
        {
            dc.PushTransform(vertical?new ScaleTransform(-1,1,row.Width/2,0):new ScaleTransform(1,-1,0,row.Height/2));
            dc.DrawImage(rowBitmap,new Rect(offsetX,offsetY,row.Width,row.Height));
            dc.Pop();
        }
        int bandWidth=vertical?Math.Max(1,(int)Math.Round(band*scale)):rowWidth,bandHeight=vertical?rowHeight:Math.Max(1,(int)Math.Round(band*scale));
        var bitmap=new RenderTargetBitmap(bandWidth,bandHeight,96*scale,96*scale,PixelFormats.Pbgra32);
        bitmap.Render(mirror);
        reflectionBrush.ImageSource=bitmap;
    }
    // A cached mirror keeps the reflection off the per-frame render path, so hovering stays at full frame rate.
    void ScheduleReflection()
    {
        if(settings.ReflectionRatio<=0||reflectionPending)return;
        reflectionPending=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>{reflectionPending=false;if(!closed)RefreshReflection();}));
    }
    void ConfirmExit()
    {
        openMenus++;hideTimer.Stop();previewTimer.Stop();preview?.Close();preview=null;
        try{if(GlassDialog.Confirm("确定退出windock？","退出windock")){if(Coordinator!=null)Coordinator.Exit();else Close();}}
        finally{openMenus--;if(!closed)ScheduleHide();}
    }
    void OpenAppearance()
    {
        if(Coordinator!=null){Coordinator.OpenSettings(this);return;}OpenSettingsFor(this);
    }
    internal void OpenSettingsFor(MainWindow source)
    {
        if (appearanceWindow != null) { appearanceWindow.Activate(); return; }
        hideTimer.Stop(); previewTimer.Stop(); preview?.Close(); preview = null;
        appearanceWindow = new AppearanceWindow(settings, () => { Theme.Apply(settings.ThemeMode);Save(); Render(); }, AddEntry, () => Launch("ms-settings:taskbar"), ConfirmExit) { Owner = this };
        openMenus++;
        appearanceWindow.Closed += (_,_) => { appearanceWindow = null; openMenus--; ScheduleHide(); };
        source.PlaceOverlay(appearanceWindow);appearanceWindow.Show();
    }
    void SetEdge(string edge)
    {
        shimmer.BeginAnimation(OpacityProperty,null);shimmer.Opacity=0;shimmer.Child=null;
        dockScroll.ScrollToHorizontalOffset(0);dockScroll.ScrollToVerticalOffset(0);
        settings.Edge = edge; settings.DockOffset = 0; settings.DockLift = 0; hidden = false; Save(); Render();InvalidateMeasure();UpdateLayout();Position();
    }
    PositionFlyout? positionFlyout;
    FrameworkElement DirectionControl()
    {
        string name=settings.Edge switch{"Top"=>"顶部","Left"=>"左侧","Right"=>"右侧",_=>"底部"};
        var button=Make("",()=>{});button.Content=DockPositionVisual.Create(settings.Edge,true);button.ToolTip="停靠位置 · "+name;
        button.Click+=(_,_)=>
        {
            if(draggingEntry!=null)return;if(positionFlyout is {IsOpen:true}){positionFlyout.IsOpen=false;return;}
            hideTimer.Stop();openMenus++;positionFlyout=new PositionFlyout(button,settings.Edge,SetEdge);positionFlyout.Closed+=(_,_)=>{positionFlyout=null;openMenus--;ScheduleHide();};positionFlyout.IsOpen=true;        };
        return button;
    }    static bool Usable(Rect area) => !area.IsEmpty && double.IsFinite(area.Width) && double.IsFinite(area.Height) && area.Width >= 1 && area.Height >= 1;
    // A monitor can report an empty or zero-sized area (unplugged, disabled or still initialising).
    // Falling back keeps positioning and dragging functional instead of clamping every offset to zero.
    Rect SelectedWorkArea()
    {
        var monitor=AssignedMonitor??DesktopIntegration.Monitors().FirstOrDefault(m=>m.Id==settings.MonitorId);
        if(string.IsNullOrEmpty(monitor.Id))monitor=DesktopIntegration.Monitors().FirstOrDefault();
        if(!string.IsNullOrEmpty(monitor.Id)&&Usable(monitor.Area))
        {
            var dpi=VisualTreeHelper.GetDpi(this);
            var area=new Rect(monitor.Area.X/dpi.DpiScaleX,monitor.Area.Y/dpi.DpiScaleY,monitor.Area.Width/dpi.DpiScaleX,monitor.Area.Height/dpi.DpiScaleY);
            if(Usable(area))return area;
        }
        if(Usable(SystemParameters.WorkArea))return SystemParameters.WorkArea;
        return new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,Math.Max(1,SystemParameters.VirtualScreenWidth),Math.Max(1,SystemParameters.VirtualScreenHeight));
    }
    void FolderMenu(string folder)
    {
        var menu=Menu();Item(menu,"在资源管理器中打开",()=>Launch(folder));menu.Items.Add(new Separator());
        try{foreach(var file in Directory.EnumerateFileSystemEntries(folder).Take(100)){var target=file;Item(menu,Path.GetFileName(file),()=>{if(Directory.Exists(target))FolderMenu(target);else Launch(target);});}}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Item(menu,"无法读取此文件夹",()=>{});}
        menu.MaxHeight=Math.Max(150,SelectedWorkArea().Height-100);menu.IsOpen=true;
    }
    internal static void RemoveGroupedEntry(Settings settings,AppGroup group,Entry entry,bool permanent)
    {
        bool Match(Entry candidate)=>string.Equals(RunningWindows.WindowTarget(candidate.Target),RunningWindows.WindowTarget(entry.Target),StringComparison.OrdinalIgnoreCase);
        group.Entries.RemoveAll(e=>Match(e));
        if(permanent){settings.Entries.RemoveAll(e=>Match(e));foreach(var other in settings.Groups)other.Entries.RemoveAll(e=>Match(e));}
        else if(!settings.Entries.Any(Match))settings.Entries.Add(entry);
        ActivityStore.Write(permanent?"彻底移除入口":"移回Dock",entry.Name,entry.Target);
    }
    GroupFlyout? groupFlyout;
    AppGroup? expandedGroup;
    void GroupMenu(AppGroup group,bool hover=false)
    {
        if(groupFlyout is {IsOpen:true}){if(ReferenceEquals(expandedGroup,group)){if(!hover)groupFlyout.Collapse();}else groupFlyout.Collapse(()=>{if(!closed&&(!hover||ReferenceEquals(groupHoverCandidate,group)))GroupMenu(group,hover);});return;}
        var anchor=panel.Children.OfType<FrameworkElement>().FirstOrDefault(i=>Equals(i.Tag,"group:"+group.Name));if(anchor==null)return;
        hideTimer.Stop();previewTimer.Stop();preview?.Close();preview=null;openMenus++;expandedGroup=group;
        groupFlyout=new GroupFlyout(anchor,group,settings.Edge,()=>{Save();Render();},()=>{settings.Groups.Remove(group);Save();Render();},(app,permanent)=>{RemoveGroupedEntry(settings,group,app,permanent);Save();Render();},hover);
        var openedFlyout=groupFlyout;openedFlyout.Closed+=(_,_)=>{if(ReferenceEquals(groupFlyout,openedFlyout)){groupFlyout=null;expandedGroup=null;}openMenus--;ScheduleHide();};openedFlyout.IsOpen=true;
    }    void AddEntry(Entry e)
    {
        var target=RunningWindows.WindowTarget(e.Target);
        if(ApplicationPlacement.Read(settings).ContainsKey(target))return;
        settings.Entries.Add(e);
        if(entryRefreshPending)return;entryRefreshPending=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            if(!entryRefreshPending)return;entryRefreshPending=false;if(closed)return;Save();Render();
        }));
    }
}


















































