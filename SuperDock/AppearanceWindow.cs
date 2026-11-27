using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace SuperDock;
internal sealed partial class AppearanceWindow : Window
{
    readonly MainWindow.Settings settings;
    readonly Action changed;
    readonly Action<MainWindow.Entry>? addEntry;
    readonly Action? systemSettings,quit;
    readonly StackPanel content=new();
    readonly StackPanel tabHost=new();
    readonly ScrollViewer scroller=new();
    readonly TextBlock pageCaption=new(){FontSize=11,Foreground=Muted,Margin=new Thickness(0,8,0,0)};
    readonly TextBlock pageTitle=new(){FontSize=23,FontWeight=FontWeights.SemiBold,Foreground=Ink,FontFamily=new FontFamily("Georgia, SimSun, Microsoft YaHei UI")};
    readonly Dictionary<string,Button> navigation=new();
    readonly CancellationTokenSource lifetime=new();
    string page="桌面偏好";
    public AppearanceWindow(MainWindow.Settings settings,Action changed,Action<MainWindow.Entry>? addEntry=null,Action? taskbarSettings=null,Action? quit=null)
    {
        Theme.Apply(settings.ThemeMode);pageTitle.Foreground=Ink;pageCaption.Foreground=Muted;this.settings=settings;this.changed=changed;this.addEntry=addEntry;systemSettings=taskbarSettings;this.quit=quit;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Windock;component/SettingsTheme.xaml",UriKind.Relative)});ApplyGlassResources();Title="windock · 设置";Width=800;Height=660;MaxHeight=Math.Max(420,SystemParameters.WorkArea.Height-40);MaxWidth=Math.Max(600,SystemParameters.WorkArea.Width-40);MinWidth=700;MinHeight=480;Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanResizeWithGrip;ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");Foreground=Ink;UseLayoutRounding=true;
        GlassWindow.Prepare(this,24);var shell=new Border{CornerRadius=new CornerRadius(24),BorderThickness=new Thickness(1),BorderBrush=Stroke,Background=PageBg};Content=shell;
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(176)});layout.ColumnDefinitions.Add(new ColumnDefinition());shell.Child=layout;
        var sidebarSurface=new Border{Background=SidebarBrush,CornerRadius=new CornerRadius(8,0,0,8)};layout.Children.Add(sidebarSurface);var sidebar=new DockPanel{Margin=new Thickness(12,18,12,18)};sidebarSurface.Child=sidebar;
        var brand=new StackPanel{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var identity=new Grid{Margin=new Thickness(0,0,0,0),Height=42};identity.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(38)});identity.ColumnDefinitions.Add(new ColumnDefinition());
        var brandIcon=new Image{Source=Branding.Logo,Width=32,Height=32,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(brandIcon,BitmapScalingMode.HighQuality);identity.Children.Add(brandIcon);
        var brandName=new TextBlock{Text="windock",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,0,0)};Grid.SetColumn(brandName,1);identity.Children.Add(brandName);brand.Children.Add(identity);BuildSettingsSearch(brand);
        var footer=new TextBlock{Text="更改自动保存",Foreground=Muted,FontSize=11,LineHeight=20,Margin=new Thickness(8,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);sidebar.Children.Add(footer);
        var nav=new StackPanel();sidebar.Children.Add(nav);
        foreach(var (name,glyph) in new[]{("桌面偏好","\uE790"),("今日代办","\uE762"),("应用管理","\uE8B7"),("使用记录","\uE9D9"),("高级设置","\uE713")})
        {
            var button=Button("",()=>Navigate(name));var navFace=new StackPanel{Orientation=Orientation.Horizontal};navFace.Children.Add(new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=15,Width=27,VerticalAlignment=VerticalAlignment.Center});navFace.Children.Add(new TextBlock{Text=name,FontSize=13,VerticalAlignment=VerticalAlignment.Center});button.Content=navFace;button.Style=(Style)FindResource("SettingsNavigation");button.MinHeight=36;button.Padding=new Thickness(12,0,10,0);button.BorderThickness=new Thickness(0);button.FocusVisualStyle=null;button.Margin=new Thickness(0,0,0,4);button.HorizontalContentAlignment=HorizontalAlignment.Left;navigation[name]=button;nav.Children.Add(button);
        }
        var area=new DockPanel{Margin=new Thickness(24,22,16,20)};Grid.SetColumn(area,1);layout.Children.Add(area);
        var header=new Grid{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);area.Children.Add(header);var titles=new StackPanel();titles.Children.Add(pageTitle);titles.Children.Add(pageCaption);header.Children.Add(titles);header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is not System.Windows.Controls.Button&&e.LeftButton==MouseButtonState.Pressed)DragMove();};
        var close=Button("✕",Close);close.Width=28;close.Height=28;close.MinHeight=28;close.BorderThickness=new Thickness(0);close.Background=Brushes.Transparent;close.Padding=new Thickness(0);close.HorizontalContentAlignment=HorizontalAlignment.Center;close.ToolTip="关闭设置";close.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(close);
        pageTitle.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)DragMove();};
        DockPanel.SetDock(tabHost,Dock.Top);area.Children.Add(tabHost);scroller.Content=content;scroller.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroller.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroller.Padding=new Thickness(0,0,12,0);area.Children.Add(scroller);
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.F&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){settingsSearch?.Focus();settingsSearch?.SelectAll();e.Handled=true;}else if(e.Key==Key.Escape){if(!string.IsNullOrEmpty(settingsSearch?.Text))settingsSearch.Clear();else Close();}};Closed+=(_,_)=>{lifetime.Cancel();lifetime.Dispose();};Navigate("桌面偏好");
    }
    Brush SidebarBrush=>RailBg;
    Brush SettingsCard=>CardBg;
    // —— 设置工作台暖色主题（参考 WorkBuddy 日常集风格：暖米色底/白色大圆角卡片/衬线标题/梅紫点缀）——
    static Brush W(string hex)=>(Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
    static Brush Ink=>Theme.Dark?W("#F2EAE0"):W("#3C3327");
    static Brush Muted=>Theme.Dark?W("#B5A593"):W("#A18D76");
    static Brush CardBg=>Theme.Dark?W("#3A322B"):W("#FFFFFF");
    static Brush Stroke=>Theme.Dark?W("#4A4034"):W("#EDE2D2");
    static Brush Hover=>Theme.Dark?W("#453B32"):W("#F7F0E5");
    static Brush Wash=>Theme.Dark?W("#4C3941"):W("#F3E3E9");
    static Brush Accent=>Theme.Dark?W("#ECC9D6"):W("#6B4055"); // 强调前景/描边
    static Brush AccentSolid=>W("#6B4055"); // 开关等实底强调
    static Brush PageBg=>Theme.Dark?W("#2A2420"):W("#F6F1E9");
    static Brush RailBg=>Theme.Dark?W("#322B25"):W("#EFE7DB");
    static Brush TrackBg=>Theme.Dark?W("#4A4034"):W("#E4DACB");
    void ApplyGlassResources()
    {
        // 窗口级覆写共享样式里的 DynamicResource，只影响设置工作台，不污染全局主题
        Resources["ThemeText"]=Ink;Resources["ThemeMuted"]=Muted;Resources["ThemeCard"]=CardBg;Resources["ThemeStroke"]=Stroke;Resources["ThemeHover"]=Hover;Resources["ThemeSelected"]=Wash;Resources["SwitchOn"]=AccentSolid;Resources["SwitchOff"]=TrackBg;Resources["SwitchBorder"]=Stroke;Resources["ThemeTrack"]=TrackBg;Resources["SettingsAccent"]=Accent;
    }
    void ApplyWindowTheme()
    {
        ApplyGlassResources();Foreground=Ink;pageTitle.Foreground=Ink;pageCaption.Foreground=Muted;
        if(Content is Border shell){shell.Background=PageBg;GlassWindow.Refresh(this);shell.BorderBrush=Stroke;if(shell.Child is Grid layout&&layout.Children[0] is Border sidebar){sidebar.Background=SidebarBrush;Recolor(sidebar);}}
    }
    void Recolor(DependencyObject root)
    {
        if(root is TextBlock text)text.Foreground=text.FontSize<=11?Muted:Ink;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Recolor(VisualTreeHelper.GetChild(root,i));
    }
    Button Button(string text,Action action)
    {
        var button=new Button{Content=text,Style=(Style)FindResource("SettingsButton"),FontSize=13,Padding=new Thickness(11,6,11,6),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,MinHeight=30,Cursor=Cursors.Hand,Margin=new Thickness(0,0,0,8)};button.Click+=(_,_)=>action();return button;
    }
    TextBlock Text(string text,int size=13)=>new(){Text=text,FontSize=size,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
    void Heading(string text)=>content.Children.Add(new TextBlock{Text=text,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Muted,Margin=new Thickness(0,20,0,10)});
    void PreferenceRow(UIElement row)
    {
        StackPanel rows;
        if(content.Children.Count>0&&content.Children[content.Children.Count-1] is Border{Tag:"preferenceRows",Child:StackPanel existing}){rows=existing;rows.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromArgb(22,128,144,166)),Margin=new Thickness(14,0,14,0)});}
        else{rows=new StackPanel();content.Children.Add(new Border{Tag="preferenceRows",Child=rows,Background=SettingsCard,CornerRadius=new CornerRadius(16),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(0,2,0,10)});}
        rows.Children.Add(new Border{Padding=new Thickness(12,8,12,8),Child=row});
    }
    void Card(UIElement child)
    {
        UniformGrid grid;
        if(content.Children.Count>0&&content.Children[content.Children.Count-1] is UniformGrid existing&&existing.Tag as string=="cards")grid=existing;
        else{grid=new UniformGrid{Columns=2,Tag="cards",Margin=new Thickness(-4,0,-4,4)};grid.SizeChanged+=(_,_)=>{int columns=grid.ActualWidth<440?1:2;if(grid.Columns!=columns)grid.Columns=columns;};content.Children.Add(grid);}
        grid.Children.Add(new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(16),Margin=new Thickness(4,4,4,4),Child=child});
    }    void Toggle(string title,string description,bool value,Action<bool> update,bool showDescription=false,bool wide=false)
    {
        var row=new DockPanel{MinHeight=28};
        var button=new ToggleButton{Style=(Style)FindResource("SettingsSwitch"),IsChecked=value,IsThreeState=false,VerticalAlignment=VerticalAlignment.Center,ToolTip=title};
        System.Windows.Automation.AutomationProperties.SetName(button,title);
        button.Click+=(_,_)=>{update(button.IsChecked==true);changed();Navigate(page);};
        DockPanel.SetDock(button,Dock.Right);row.Children.Add(button);        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};labels.Children.Add(new TextBlock{Text=title,FontSize=13,FontWeight=FontWeights.Normal,Margin=new Thickness(0),TextWrapping=TextWrapping.Wrap});if(showDescription){var detail=Text(description,11);detail.Margin=new Thickness(0,8,0,0);labels.Children.Add(detail);}row.ToolTip=description;row.Children.Add(labels);if(wide)PreferenceRow(row);else Card(row);
    }
    void Range(string title,string description,double min,double max,double value,string unit,Action<double> update,double step=1)
    {
        var stack=new StackPanel{ToolTip=description};var header=new DockPanel();var label=new TextBlock{Text=$"{value:0.##}{unit}",FontSize=12,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(label,Dock.Right);header.Children.Add(label);header.Children.Add(new TextBlock{Text=title,FontSize=13});stack.Children.Add(header);
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,SmallChange=step,Margin=new Thickness(0,7,0,0)};slider.ValueChanged+=(_,_)=>{label.Text=$"{slider.Value:0.##}{unit}";update(slider.Value);changed();};stack.Children.Add(slider);Card(stack);
    }
    void Navigate(string name)
    {
        if(name=="通用设置")name="高级设置";
        if(name=="桌面组件"){desktopTab="桌面组件";name="桌面偏好";}
        if(name=="数据中心"){recordsTab="应用统计";name="使用记录";}if(name=="日志中心"){recordsTab="运行日志";name="使用记录";}
        if(settingsSearch!=null&&settingsSearch.Text.Length>0){changingSearch=true;settingsSearch.Clear();changingSearch=false;}
        double offset=page==name?scroller.VerticalOffset:0;page=name;pageTitle.Text=name;pageCaption.Visibility=Visibility.Collapsed;tabHost.Children.Clear();content.Children.Clear();Dispatcher.BeginInvoke(new Action(()=>scroller.ScrollToVerticalOffset(offset)));
        foreach(var pair in navigation){pair.Value.Tag=pair.Key==name?"selected":null;pair.Value.Background=pair.Key==name?Wash:Brushes.Transparent;pair.Value.FontWeight=pair.Key==name?FontWeights.SemiBold:FontWeights.Normal;}
        switch(name)
        {
            case "桌面偏好":
                DesktopPreferences();break;
            case "应用管理": Applications();break;
            case "桌面组件": Components();break;
            case "使用记录": Records();break;
            case "今日代办": Todos();break;
            default:
                Maintenance();break;
        }
    }
    string desktopTab="布局交互";
    void DesktopPreferences()
    {
        Tabs(["布局交互","外观特效","常用入口","桌面组件"],desktopTab,value=>{desktopTab=value;scroller.ScrollToTop();Navigate(page);});
        switch(desktopTab){case "外观特效":AppearancePreferences();break;case "常用入口":BuiltInEntries();break;case "桌面组件":Components();break;default:DockPreferences();ShortcutPreferences();break;}
    }
    readonly HashSet<string> expandedSections=[];
    void Fold(string title,Action build)
    {
        int start=content.Children.Count;build();var stack=new StackPanel();
        while(content.Children.Count>start){var child=content.Children[start];content.Children.RemoveAt(start);stack.Children.Add(child);}
        var section=new Expander{Header=title,Content=stack,IsExpanded=expandedSections.Contains(title),Foreground=Ink,Padding=new Thickness(4),Margin=new Thickness(0,8,0,0)};
        section.Expanded+=(_,_)=>expandedSections.Add(title);section.Collapsed+=(_,_)=>expandedSections.Remove(title);
        content.Children.Add(new Border{CornerRadius=new CornerRadius(8),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Background=SettingsCard,Child=section});
    }
    void ThemePreferences()
    {
        Heading("界面主题");
        var themes=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var (mode,label) in new[]{("Light","亮色玻璃"),("Dark","暗色玻璃")}){
            bool selected=settings.ThemeMode==mode;var choice=Button("",()=>{settings.ThemeMode=mode;changed();ApplyWindowTheme();Navigate(page);});choice.Width=122;choice.Padding=new Thickness(0);choice.Margin=new Thickness(0,0,8,0);choice.Background=Brushes.Transparent;choice.BorderBrush=selected?(Brush)Resources["SettingsAccent"]:Stroke;
            var face=new StackPanel();face.Children.Add(new Viewbox{Width=120,Height=56,Child=ThemeDiagram(mode=="Dark")});var caption=new DockPanel{Margin=new Thickness(10,8,10,9)};var check=new TextBlock{Text=selected?"✓":"",Foreground=(Brush)Resources["SettingsAccent"],FontSize=12};DockPanel.SetDock(check,Dock.Right);caption.Children.Add(check);caption.Children.Add(new TextBlock{Text=label,FontSize=12});face.Children.Add(caption);choice.Content=face;themes.Children.Add(choice);
        }
        content.Children.Add(themes);
    }    void EffectPreferences()
    {
                Heading("氛围特效");
                var effects=new UniformGrid{Columns=2,Margin=new Thickness(0,0,0,16)};
                foreach(var (id,title,description) in new[]{("None","关闭特效","保持纯净玻璃"),("Sweep","斜向扫光","倾斜光带 · 匀速掠过"),("Breathe","呼吸微光","柔和光晕 · 缓缓明灭"),("Edge","边缘流光","轮廓高光 · 轻盈闪耀")})
                {
                    bool active=id=="None"?!settings.AmbientShimmer||settings.AmbientEffect=="None":settings.AmbientShimmer&&settings.AmbientEffect==id;
                    var effect=Button("",()=>{settings.AmbientEffect=id;settings.AmbientShimmer=id!="None";changed();Navigate(page);});effect.Margin=new Thickness(5);effect.Padding=new Thickness(12,10,12,10);effect.Background=active?Wash:CardBg;
                    var labels=new StackPanel();labels.Children.Add(new TextBlock{Text=(active?"✓  ":"")+title,FontSize=12,FontWeight=FontWeights.Normal});effect.ToolTip=description;effect.Content=labels;effects.Children.Add(effect);
                }
                effects.ToolTip="每 12 秒播放一轮，隐藏时暂停";content.Children.Add(effects);
                Toggle("原生背景模糊","模糊 Dock 背后的桌面；实际效果取决于 Windows 合成器",settings.BackgroundBlur,v=>settings.BackgroundBlur=v,wide:true);
    }
    void SizePreferences()
    {
        var header=new DockPanel{Margin=new Thickness(0,16,0,8)};var reset=Button("恢复默认外观",()=>{settings.DockHeight=68;settings.GlassOpacity=.8;settings.IconSize=36;settings.IconSpacing=5;settings.HoverScale=1.9;settings.CornerRadius=18;settings.ReflectionRatio=0;settings.LaunchBounce=.5;settings.HoverScale=1.9;settings.HoverRange=2.5;settings.HoverSmoothing=.5;settings.HoverPauseOnDrag=false;settings.LaunchEffectColor="None";settings.LaunchEffectColorHex="#5AA8FF";settings.LaunchEffectDuration=.5;changed();Navigate(page);});reset.FontSize=11;reset.MinHeight=26;reset.Padding=new Thickness(8,3,8,3);reset.Margin=new Thickness(0);DockPanel.SetDock(reset,Dock.Right);header.Children.Add(reset);header.Children.Add(new TextBlock{Text="尺寸与质感",FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center});content.Children.Add(header);
                Range("栏体高度","纵向停靠时对应栏体宽度",48,120,settings.DockHeight," px",v=>settings.DockHeight=v);
                Range("玻璃不透明度","0% 为完全透明，仅改变背景，不影响图标清晰度",0,100,settings.GlassOpacity*100,"%",v=>settings.GlassOpacity=v/100);
                Range("图标大小","超出栏体容纳范围时会自动适配",24,64,settings.IconSize," px",v=>settings.IconSize=v);
                Range("图标间距","调整入口之间的留白",0,16,settings.IconSpacing," px",v=>settings.IconSpacing=v);
                Range("圆角","从利落直角到柔和曲线",0,32,settings.CornerRadius," px",v=>settings.CornerRadius=v);
                Range("图标倒影","占图标高度的比例，0% 关闭；镜像按原尺寸裁切，不会压缩",0,100,settings.ReflectionRatio*100,"%",v=>settings.ReflectionRatio=v/100);
                HoverLaunchPreferences();

    }
    // Nexus keeps the frequency effect controls on their own page; two tiles open a
    // dedicated property sheet instead of cramming every knob into the main list.
    void HoverLaunchPreferences()
    {
        Heading("效果");
        var tiles=new UniformGrid{Columns=2,Width=440,HorizontalAlignment=HorizontalAlignment.Left};
        tiles.Children.Add(EffectCard("悬停放大","",HoverSummary(),()=>OpenEffectEditor(true)));
        tiles.Children.Add(EffectCard("启动传送","",LaunchSummary(),()=>OpenEffectEditor(false)));
        content.Children.Add(tiles);
    }
    string HoverSummary()
    {
        string smooth=settings.HoverSmoothing<.34?"慢":settings.HoverSmoothing>.66?"快":"中";
        return $"放大 {settings.HoverScale:0.##} 倍 · 范围 {settings.HoverRange:0.#} 个图标 · 平滑 {smooth}"+(settings.HoverPauseOnDrag?" · 拖放时暂停":"");
    }
    string LaunchSummary()
    {
        string paint=settings.LaunchEffectColor switch{"Custom"=>"着色 "+settings.LaunchEffectColorHex.ToUpperInvariant(),"HotTrack"=>"着色 Hot-Track",_=>"无着色"};
        string span=settings.LaunchEffectDuration<.34?"短":settings.LaunchEffectDuration>.66?"长":"适中";
        return paint+" · 持续 "+span;
    }
    Button EffectCard(string title,string glyph,string summary,Action open)
    {
        var button=Button("",open);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Top;button.Padding=new Thickness(12,11,12,11);button.MinHeight=96;button.Margin=new Thickness(3);button.ToolTip=title+" · 点击自定义";
        var stack=new StackPanel();
        var head=new StackPanel{Orientation=Orientation.Horizontal};
        head.Children.Add(new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=16,Width=25,Foreground=(Brush)FindResource("SettingsAccent"),VerticalAlignment=VerticalAlignment.Center});
        head.Children.Add(new TextBlock{Text=title,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Ink,VerticalAlignment=VerticalAlignment.Center});
        stack.Children.Add(head);
        stack.Children.Add(new TextBlock{Text=summary,FontSize=11,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,0),LineHeight=17});
        stack.Children.Add(new TextBlock{Text="效果设置 ›",FontSize=11,Foreground=(Brush)FindResource("SettingsAccent"),Margin=new Thickness(0,8,0,0)});
        button.Content=stack;return button;
    }
    void OpenEffectEditor(bool hover)
    {
        var editor=new EffectEditorWindow(settings,hover,()=>{changed();Navigate(page);}){Owner=this};
        editor.ShowDialog();
    }
    void DockPreferences()
    {
                Heading("停靠位置");var directions=new UniformGrid{Columns=4,Width=340,HorizontalAlignment=HorizontalAlignment.Left};foreach(var (edge,label) in new[]{("Left","← 左"),("Top","↑ 上"),("Bottom","↓ 下"),("Right","→ 右")}){var button=Button(label,()=>{settings.Edge=edge;changed();Navigate(page);});var face=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};var positionPreview=new Viewbox{Width=42,Height=28,Stretch=Stretch.Uniform,Child=DockPositionVisual.Create(edge)};face.Children.Add(positionPreview);face.Children.Add(new TextBlock{Text=label[2..],HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,0)});button.Content=face;button.Padding=new Thickness(8);button.HorizontalContentAlignment=HorizontalAlignment.Center;button.Margin=new Thickness(3);if(settings.Edge==edge)button.Background=Wash;directions.Children.Add(button);}content.Children.Add(directions);
                Heading("显示器");
                var monitors=DesktopIntegration.Monitors();
                Toggle("所有显示器同步显示","共享应用、分组、排序和外观，显示器拔插后自动适配",settings.ShowOnAllMonitors,value=>{settings.ShowOnAllMonitors=value;if(!value&&settings.MonitorIds.Count==0&&monitors.Count>0)settings.MonitorIds.Add(monitors.Any(m=>m.Id==settings.MonitorId)?settings.MonitorId:monitors[0].Id);});
                if(!settings.ShowOnAllMonitors)for(int index=0;index<monitors.Count;index++){
                    var monitor=monitors[index];bool selected=settings.MonitorIds.Count>0?settings.MonitorIds.Contains(monitor.Id):monitor.Id==(string.IsNullOrEmpty(settings.MonitorId)?monitors[0].Id:settings.MonitorId);
                    Toggle("显示器 "+(index+1)+(index==0?" · 主显示器":""),monitor.Id,selected,value=>{if(value){if(!settings.MonitorIds.Contains(monitor.Id))settings.MonitorIds.Add(monitor.Id);}else{settings.MonitorIds.Remove(monitor.Id);if(settings.MonitorIds.Count==0)settings.MonitorIds.Add(monitors[0].Id);}settings.MonitorId=settings.MonitorIds.FirstOrDefault()??"";});
                }
                ActionCard("任务栏设置","打开 Windows 任务栏设置","\uE713",()=>systemSettings?.Invoke());
                Heading("交互行为");
                Toggle("智能避让","其他窗口覆盖时退到后方；移到停靠边缘或按快捷键唤起",settings.SmartLayering,v=>settings.SmartLayering=v);
                Toggle("自动隐藏","移出后隐藏，靠近边缘时显示",settings.AutoHide,v=>settings.AutoHide=v);
                Toggle("悬浮预览","悬浮应用图标时显示窗口预览",settings.HoverWindowPreview,v=>settings.HoverWindowPreview=v);
                Toggle("拖动 Dock","按住图标或空白处向任意方向拖动整条 Dock，拖到屏幕边缘会吸附并切换停靠方向；按住 Ctrl 拖动仍可调整图标顺序",settings.DockDraggable,v=>settings.DockDraggable=v);
                
    }
    UIElement PreviewWidget(bool performance) => new Border{Background=SettingsCard,CornerRadius=new CornerRadius(14),Margin=new Thickness(0,0,0,16),Child=new WidgetView(settings,performance)};
    void BuiltInEntries()
    {
        Heading("Dock 功能入口");Toggle("文件搜索","基于 Everything 的本机文件搜索",settings.ShowFileSearch,v=>settings.ShowFileSearch=v);Toggle("位置按钮","在 Dock 显示停靠位置切换入口",settings.ShowDirectionArrows,v=>settings.ShowDirectionArrows=v);Toggle("退出按钮","在 Dock 显示退出入口，点击后需要确认",settings.ShowExitButton,v=>settings.ShowExitButton=v);
                var entries=new[]{new MainWindow.Entry("资源管理器","explorer.exe"),new MainWindow.Entry("计算器","calc.exe"),new MainWindow.Entry("回收站","shell:RecycleBinFolder"),new MainWindow.Entry("Windows 设置","ms-settings:"),new MainWindow.Entry("任务管理器","taskmgr.exe"),new MainWindow.Entry("桌面",Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),new MainWindow.Entry("下载",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"))};
                Heading("常用系统入口");
                foreach(var entry in entries)
                {
                    bool selected=settings.Entries.Any(e=>string.Equals(e.Target,entry.Target,StringComparison.OrdinalIgnoreCase));
                    Toggle(entry.Name,"在 Dock 中显示此入口",selected,value=>
                    {
                        if(value){if(!settings.Entries.Any(e=>string.Equals(e.Target,entry.Target,StringComparison.OrdinalIgnoreCase)))settings.Entries.Add(entry);}
                        else settings.Entries.RemoveAll(e=>string.Equals(e.Target,entry.Target,StringComparison.OrdinalIgnoreCase));
                    });
                }

    }
    void ActionCard(string title,string description,string glyph,Action action)
    {
        var face=new Grid();face.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});face.ColumnDefinitions.Add(new ColumnDefinition());face.Children.Add(new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),Foreground=(Brush)Resources["SettingsAccent"],FontSize=19,VerticalAlignment=VerticalAlignment.Center});
        var labels=new StackPanel();labels.Children.Add(new TextBlock{Text=title,FontSize=13});labels.Children.Add(new TextBlock{Text=description,FontSize=11,Foreground=Muted,Margin=new Thickness(0,4,0,0),TextWrapping=TextWrapping.Wrap});Grid.SetColumn(labels,1);face.Children.Add(labels);
        var button=Button("",action);button.Content=face;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0);button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(0);Card(button);
    }    bool customInterval;
    void Maintenance()
    {
        Tabs(["消息提醒","系统维护","关于更新"],generalTab,value=>{generalTab=value;scroller.ScrollToTop();Navigate(page);});
        switch(generalTab){
            case "消息提醒":NotificationPreferences();break;
            case "系统维护":GeneralPreferences();BackupPreferences();break;
            case "关于更新":AboutPreferences();break;
            default:NotificationPreferences();break;
        }
    }
    string generalTab="消息提醒";
    void ShortcutPreferences()
    {
        Heading("全局快捷键");
        content.Children.Add(Text("按 Alt+S 可随时打开或关闭设置窗口。",11));
        Toggle("快捷键显隐","隐藏后再次按快捷键显示 Dock",settings.DockHotkeyEnabled,v=>settings.DockHotkeyEnabled=v);
        var shortcuts=new UniformGrid{Columns=3,Width=380,HorizontalAlignment=HorizontalAlignment.Left};foreach(var key in new[]{"Alt+D","Ctrl+Alt+D","Ctrl+Shift+D"}){var button=Button(key,()=>{settings.DockHotkey=key;changed();Navigate(page);});button.Background=settings.DockHotkey==key?Wash:SettingsCard;button.Margin=new Thickness(4);shortcuts.Children.Add(button);}content.Children.Add(shortcuts);content.Children.Add(Text(MainWindow.ShortcutStatus,11));Toggle("文件搜索快捷键","Alt+空格或 Ctrl+Alt+F 打开原版搜索窗口",settings.FileSearchHotkeyEnabled,v=>settings.FileSearchHotkeyEnabled=v);var searchKeys=new StackPanel{Orientation=Orientation.Horizontal};foreach(var key in new[]{"Alt+Space","Ctrl+Alt+F"}){var option=Button(key=="Alt+Space"?"Alt+空格":key,()=>{settings.FileSearchHotkey=key;changed();Navigate(page);});option.Background=settings.FileSearchHotkey==key?Wash:SettingsCard;option.Margin=new Thickness(0,0,8,8);searchKeys.Children.Add(option);}content.Children.Add(searchKeys);content.Children.Add(Text(MainWindow.FileSearchShortcutStatus,11));
        Heading("截图与贴图");
        Toggle("截图快捷键","框选屏幕截图，可涂鸦、标注、复制、保存或贴图",settings.ScreenshotHotkeyEnabled,v=>settings.ScreenshotHotkeyEnabled=v);
        var screenshotKeys=new StackPanel{Orientation=Orientation.Horizontal};foreach(var key in new[]{"F1","F2","F3","F4"}){var option=Button(key,()=>{settings.ScreenshotHotkey=key;changed();Navigate(page);});option.Background=settings.ScreenshotHotkey==key?Wash:SettingsCard;option.Margin=new Thickness(0,0,8,8);screenshotKeys.Children.Add(option);}content.Children.Add(screenshotKeys);
        content.Children.Add(Text(MainWindow.ScreenshotShortcutStatus,11));
        Toggle("贴图快捷键","将截图或剪贴板中的图片贴到桌面置顶显示：拖动移动、滚轮缩放、右键菜单可标注/复制/保存",settings.PinHotkeyEnabled,v=>settings.PinHotkeyEnabled=v);
        var pinKeys=new StackPanel{Orientation=Orientation.Horizontal};foreach(var key in new[]{"F1","F2","F3","F4"}){var option=Button(key,()=>{settings.PinHotkey=key;changed();Navigate(page);});option.Background=settings.PinHotkey==key?Wash:SettingsCard;option.Margin=new Thickness(0,0,8,8);pinKeys.Children.Add(option);}content.Children.Add(pinKeys);
        content.Children.Add(Text(MainWindow.PinShortcutStatus,11));
        content.Children.Add(Text("截图进行中按贴图键可直接钉住当前选区；若两个快捷键相同，贴图会为截图让位并在上方提示。",11));
        Heading("翻译快捷键");
        Toggle("翻译快捷键","框选屏幕文字，自动识别并中英互译（中文译为英文，英文译为中文）",settings.TranslateHotkeyEnabled,v=>settings.TranslateHotkeyEnabled=v);
        var translateKeys=new StackPanel{Orientation=Orientation.Horizontal};foreach(var key in new[]{"Alt+F","Ctrl+Shift+F"}){var option=Button(key,()=>{settings.TranslateHotkey=key;changed();Navigate(page);});option.Background=settings.TranslateHotkey==key?Wash:SettingsCard;option.Margin=new Thickness(0,0,8,8);translateKeys.Children.Add(option);}content.Children.Add(translateKeys);
        content.Children.Add(Text(MainWindow.TranslateShortcutStatus,11));
        // Umi-OCR 本机接口（参考 Umi-OCR 默认 http://127.0.0.1:1224）：识别与翻译优先走它，离线可用。
        var apiRow=new Grid{Margin=new Thickness(0,4,0,0)};
        apiRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        apiRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        apiRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        apiRow.Children.Add(new TextBlock{Text="Umi-OCR 接口",FontSize=12.5,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
        var apiInput=new TextBox{Height=32,FontSize=12,Padding=new Thickness(8,2,8,2),VerticalContentAlignment=VerticalAlignment.Center,Text=settings.OcrApiUrl,ToolTip="本机 Umi-OCR 的 HTTP 接口地址，默认 http://127.0.0.1:1224"};
        Grid.SetColumn(apiInput,1);apiRow.Children.Add(apiInput);
        var apiSave=Button("保存",()=>{var value=apiInput.Text.Trim();settings.OcrApiUrl=string.IsNullOrWhiteSpace(value)?"http://127.0.0.1:1224":value;changed();Navigate(page);});
        apiSave.MinWidth=64;Grid.SetColumn(apiSave,2);apiSave.Margin=new Thickness(8,0,0,0);apiRow.Children.Add(apiSave);
        content.Children.Add(new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(14,10,12,10),Margin=new Thickness(0,6,0,4),Child=apiRow});
        content.Children.Add(Text("OCR 与翻译优先使用本机 Umi-OCR（离线可用）；未运行时自动改用系统 OCR 与在线翻译。",11));
    }
    void GeneralPreferences()
    {
        Heading("运行与启动");

        using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            Toggle("登录时启动","仅为当前 Windows 用户设置，可随时关闭",key?.GetValue("SuperDock")!=null,value=>{try{using var run=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(value)run.SetValue("SuperDock","\""+Environment.ProcessPath+"\"");else run.DeleteValue("SuperDock",false);}catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException){GlassDialog.Show("无法更新启动设置："+ex.Message);}});
        ActionCard("退出windock","退出前确认，结束当前运行","\uE7E8",()=>quit?.Invoke());
    }
    void BackupPreferences()
    {
        Heading("配置备份");
        ActionCard("导出备份","将当前配置保存到文件","\uE898",()=>
        {
            var dialog=new Microsoft.Win32.SaveFileDialog{Filter="SuperDock 设置|*.json",FileName="SuperDock-backup.json"};if(dialog.ShowDialog(this)!=true)return;
            try{File.WriteAllText(dialog.FileName,System.Text.Json.JsonSerializer.Serialize(settings,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}catch(IOException ex){GlassDialog.Show("备份失败："+ex.Message);}
        });
        ActionCard("恢复备份","从文件恢复配置","\uE8A7",()=>
        {
            var dialog=new Microsoft.Win32.OpenFileDialog{Filter="SuperDock 设置|*.json"};if(dialog.ShowDialog(this)!=true)return;
            try
            {
                var restored=System.Text.Json.JsonSerializer.Deserialize<MainWindow.Settings>(File.ReadAllText(dialog.FileName))??throw new System.Text.Json.JsonException();
                if(!new[]{"Light","Dark"}.Contains(restored.ThemeMode)||!new[]{"Left","Right","Top","Bottom"}.Contains(restored.Edge)||restored.Entries==null||restored.Groups==null||restored.Entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Target)||string.IsNullOrWhiteSpace(e.Name))||restored.Groups.Any(g=>g==null||string.IsNullOrWhiteSpace(g.Name)||g.Entries==null||g.Entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Target)||string.IsNullOrWhiteSpace(e.Name))))throw new System.Text.Json.JsonException("无效的入口或分组");
                bool Valid(double v,double min,double max)=>double.IsFinite(v)&&v>=min&&v<=max;
                if(string.IsNullOrWhiteSpace(restored.WidgetColor)||!System.Text.RegularExpressions.Regex.IsMatch(restored.WidgetColor.Trim(),"^#[0-9A-Fa-f]{6}$")||!new[]{"F1","F2","F3","F4"}.Contains(restored.ScreenshotHotkey)||!new[]{"F1","F2","F3","F4"}.Contains(restored.PinHotkey)||restored.WidgetIntervalSeconds<1||restored.WidgetIntervalSeconds>3600||restored.MessagePreviewSeconds is < 0 or > 5||restored.MessagePreviewLastSeconds is < 1 or > 5||!Valid(restored.DockHeight,48,120)||!Valid(restored.IconSize,24,64)||!Valid(restored.GlassOpacity,0,1)||!Valid(restored.IconSpacing,0,16)||!Valid(restored.HoverScale,1,2.5)||!Valid(restored.CornerRadius,0,32))throw new System.Text.Json.JsonException("外观参数超出范围");
                var local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperDock");Directory.CreateDirectory(local);File.WriteAllText(Path.Combine(local,"settings.before-restore.json"),System.Text.Json.JsonSerializer.Serialize(settings));
                foreach(var property in typeof(MainWindow.Settings).GetProperties())property.SetValue(settings,property.GetValue(restored));changed();ApplyWindowTheme();Navigate(page);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){GlassDialog.Show("恢复失败，当前配置未更改："+ex.Message);}
        });
        content.Children.Add(Text("恢复前自动备份当前设置。",11));
    }
    void AboutPreferences()
    {
        var about=new DockPanel{Margin=new Thickness(0,0,0,20)};about.Children.Add(new Image{Source=Branding.Logo,Width=58,Height=58,Margin=new Thickness(0,0,16,0)});var identity=new StackPanel{VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(new TextBlock{Text="windock",FontSize=22,FontWeight=FontWeights.SemiBold,Foreground=Ink});identity.Children.Add(Text("版本 "+(typeof(MainWindow).Assembly.GetName().Version?.ToString(3)??"1.1.2"),12));about.Children.Add(identity);content.Children.Add(about);
        UpdateControls();

    }
    FrameworkElement LauncherDiagram(bool full)
    {
        var canvas=new Canvas{Width=280,Height=150,ClipToBounds=true};
        canvas.Children.Add(new Border{Width=280,Height=150,CornerRadius=new CornerRadius(12),BorderBrush=Stroke,BorderThickness=new Thickness(1),Background=new LinearGradientBrush(Color.FromRgb(98,151,187),Color.FromRgb(133,114,167),35)});
        double x=full?7:48,y=full?7:30,w=full?266:184,h=full?136:96;
        var panel=new Border{Width=w,Height=h,CornerRadius=new CornerRadius(9),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(150,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(105,237,242,255)),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=12,ShadowDepth=3,Opacity=.2}};Canvas.SetLeft(panel,x);Canvas.SetTop(panel,y);canvas.Children.Add(panel);
        var search=new Border{Width=w*.4,Height=8,CornerRadius=new CornerRadius(4),Background=new SolidColorBrush(Color.FromArgb(125,255,255,255))};Canvas.SetLeft(search,x+w*.3);Canvas.SetTop(search,y+10);canvas.Children.Add(search);
        int columns=full?7:5,rows=full?3:2;var colors=new[]{"#69B5F1","#8D7FE5","#F1958E","#77C9AB","#E4BA70"};double cell=w/(columns+1),gap=(h-27)/(rows+1);
        for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){var icon=new Border{Width=full?18:16,Height=full?18:16,CornerRadius=new CornerRadius(4),BorderBrush=new SolidColorBrush(Color.FromArgb(120,255,255,255)),BorderThickness=new Thickness(.6),Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[(row+col)%colors.Length]))};Canvas.SetLeft(icon,x+cell*(col+1)-8);Canvas.SetTop(icon,y+20+gap*(row+1)-8);canvas.Children.Add(icon);}
        var dock=new Border{Width=95,Height=9,CornerRadius=new CornerRadius(4.5),Background=new SolidColorBrush(Color.FromArgb(135,255,255,255))};Canvas.SetLeft(dock,92);Canvas.SetTop(dock,137);canvas.Children.Add(dock);
        return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=150};
    }
    FrameworkElement GroupDiagram(MainWindow.AppGroup group)
    {
        var canvas=new Canvas{Width=200,Height=112,ClipToBounds=true};
        canvas.Children.Add(new Border{Width=200,Height=112,CornerRadius=new CornerRadius(13),Background=new LinearGradientBrush(Color.FromRgb(109,161,190),Color.FromRgb(142,128,179),35),BorderBrush=Stroke,BorderThickness=new Thickness(1)});
        var dock=new Border{Width=48,Height=48,CornerRadius=new CornerRadius(13),Background=new SolidColorBrush(Color.FromArgb(100,255,255,255)),BorderBrush=new SolidColorBrush(Color.FromArgb(120,255,255,255)),BorderThickness=new Thickness(1),Child=GroupIcons.Create(group,32)};Canvas.SetLeft(dock,10);Canvas.SetTop(dock,32);canvas.Children.Add(dock);
        var arrow=new TextBlock{Text="›",FontSize=24,Foreground=Brushes.White};Canvas.SetLeft(arrow,61);Canvas.SetTop(arrow,38);canvas.Children.Add(arrow);
        var apps=new UniformGrid{Columns=3,Rows=2,Margin=new Thickness(8)};
        foreach(var entry in group.Entries.Take(6)){var source=ShellIcons.Load(entry.Target);var slot=new Border{Margin=new Thickness(3),CornerRadius=new CornerRadius(5),Background=new SolidColorBrush(Color.FromArgb(38,255,255,255)),Child=source==null?new TextBlock{Text=entry.Name[..1],Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}:new Image{Source=source,Width=22,Height=22}};apps.Children.Add(slot);}
        for(int i=apps.Children.Count;i<6;i++)apps.Children.Add(new Border{Margin=new Thickness(5),CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(65,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255))});
        var panel=new Border{Width=112,Height=82,CornerRadius=new CornerRadius(11),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(150,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(75,245,248,255)),Child=apps,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=9,ShadowDepth=3,Opacity=.18}};Canvas.SetLeft(panel,78);Canvas.SetTop(panel,15);canvas.Children.Add(panel);
        return new Viewbox{Width=78,Height=44,Child=canvas,Stretch=Stretch.Uniform};
    }
    void LauncherSettings()
    {
        Heading("应用中心效果");
        var modes=new UniformGrid{Columns=2};
        foreach(var (full,label) in new[]{(false,"小窗模式"),(true,"全屏模式")})
        {
            bool selected=settings.ApplicationCenterFullscreen==full;
            var choice=Button("",()=>{settings.ApplicationCenterFullscreen=full;changed();Navigate(page);});choice.Background=Brushes.Transparent;choice.BorderBrush=selected?(Brush)Resources["SettingsAccent"]:Stroke;choice.Margin=new Thickness(5);choice.Padding=new Thickness(12);choice.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            var face=new StackPanel();face.Children.Add(new Viewbox{Height=100,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,Child=LauncherDiagram(full)});
            var caption=new DockPanel{Margin=new Thickness(0,10,0,0)};var check=new TextBlock{Text=selected?"✓":"○",Foreground=Ink,FontSize=16};DockPanel.SetDock(check,Dock.Right);caption.Children.Add(check);caption.Children.Add(new TextBlock{Text=label,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Ink});face.Children.Add(caption);
            var detail=Text(full?"铺满屏幕，专注查找应用":"居中浮窗，保留桌面视野",11);detail.Margin=new Thickness(0,6,0,0);face.Children.Add(detail);choice.Content=face;modes.Children.Add(choice);
        }
        content.Children.Add(modes);        Heading("应用分组");
        var groupCards=new UniformGrid{Columns=2};groupCards.SizeChanged+=(_,_)=>{int columns=groupCards.ActualWidth<440?1:2;if(groupCards.Columns!=columns)groupCards.Columns=columns;};content.Children.Add(groupCards);
        foreach(var group in settings.Groups.ToArray())
        {
            var body=new StackPanel{Margin=new Thickness(0,10,0,0)};var header=new StackPanel{Orientation=Orientation.Horizontal};header.Children.Add(GroupDiagram(group));var summary=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,8,0)};summary.Children.Add(new TextBlock{Text=group.Name,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Ink});summary.Children.Add(new TextBlock{Text=group.Entries.Count+" 个应用",Foreground=Muted,FontSize=12,Margin=new Thickness(0,5,0,0)});header.Children.Add(summary);
            var rename=new DockPanel{Margin=new Thickness(0,12,0,0)};var newName=new TextBox{Text=group.Name,Height=36,Padding=new Thickness(10,4,10,4),Margin=new Thickness(0,0,8,0)};
            var saveName=Button("重命名",()=>{var value=newName.Text.Trim();if(value.Length==0||settings.Groups.Any(g=>g!=group&&g.Name==value))return;int index=settings.Groups.IndexOf(group);if(index<0)return;settings.Groups[index]=group with{Name=value};for(int i=0;i<settings.DockOrder.Count;i++)if(settings.DockOrder[i]=="group:"+group.Name)settings.DockOrder[i]="group:"+value;changed();Navigate(page);});saveName.Margin=new Thickness(0);DockPanel.SetDock(saveName,Dock.Right);rename.Children.Add(saveName);rename.Children.Add(newName);body.Children.Add(rename);
            summary.Children.Add(Button("更换图标",()=>ChooseGroupIcon(group)));
            body.Children.Add(Button("解散分组，保留应用",()=>{foreach(var entry in group.Entries)if(!settings.Entries.Any(e=>e.Target==entry.Target))settings.Entries.Add(entry);settings.Groups.Remove(group);changed();Navigate(page);}));
            var editor=new Expander{Header=header,Content=body,IsExpanded=false};groupCards.Children.Add(new Border{VerticalAlignment=VerticalAlignment.Top,Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(8),Margin=new Thickness(4),Child=editor});
        }
    }
    void ChooseGroupIcon(MainWindow.AppGroup group)
    {
        var iconWindow=new Window{Owner=this,Title="选择分组图标",Width=410,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterOwner};GlassWindow.Prepare(iconWindow);var body=new StackPanel();
        body.Children.Add(new TextBlock{Text="选择分组图标",Foreground=Ink,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
            var icons=new UniformGrid{Columns=4,Margin=new Thickness(0,12,0,8)};
            foreach(var preset in GroupIcons.BuiltIn){var option=Button(preset.Name,()=>{group.Icon=preset.Name;group.CustomIcon="";changed();iconWindow.Close();Navigate(page);});var face=new StackPanel{Orientation=Orientation.Horizontal};face.Children.Add(GroupIcons.Create(new MainWindow.AppGroup("",[]){Icon=preset.Name},24));face.Children.Add(new TextBlock{Text=preset.Name,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center});option.Content=face;option.Margin=new Thickness(3);icons.Children.Add(option);}body.Children.Add(icons);
            body.Children.Add(Button("选择自定义图标…",()=>{var picker=new Microsoft.Win32.OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp"};if(picker.ShowDialog(this)!=true)return;try{var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperDock","group-icons");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid()+Path.GetExtension(picker.FileName));File.Copy(picker.FileName,path);group.CustomIcon=path;changed();iconWindow.Close();Navigate(page);}catch(IOException ex){GlassDialog.Show("图标导入失败："+ex.Message);}}));

        var cancel=Button("取消",iconWindow.Close);body.Children.Add(cancel);iconWindow.Content=new Border{Background=CardBg,BorderBrush=Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(20),Child=body};iconWindow.ShowDialog();
    }
    void Applications()
    {
        LauncherSettings();
        Heading("新建分组");
        var row=new DockPanel{MaxWidth=440,HorizontalAlignment=HorizontalAlignment.Left};var name=new TextBox{MinWidth=280,Height=32,Padding=new Thickness(10,4,10,4),ToolTip="分组名称",Margin=new Thickness(0,0,10,0)};
        var create=Button("创建分组",()=>{var value=name.Text.Trim();if(value.Length==0||settings.Groups.Any(g=>g.Name==value)){name.ToolTip="请输入不重复的分组名称";return;}settings.Groups.Add(new MainWindow.AppGroup(value,[]));changed();Navigate(page);});create.Margin=new Thickness(0);DockPanel.SetDock(create,Dock.Right);row.Children.Add(create);row.Children.Add(name);content.Children.Add(new Border{Background=SettingsCard,CornerRadius=new CornerRadius(9),Padding=new Thickness(12),Margin=new Thickness(0,0,0,16),HorizontalAlignment=HorizontalAlignment.Left,Child=row});
    }    void Components()
    {
        
        Heading("选择组件");Toggle("性能监测","CPU · 内存 · 网络",settings.PerformanceWidget,v=>settings.PerformanceWidget=v,true);
        Toggle("城市天气","实时天气与气温",settings.WeatherWidget,v=>settings.WeatherWidget=v,true);
        Toggle("时钟与日历","时间、日期与月历",settings.ClockWidget,v=>settings.ClockWidget=v,true);
        Toggle("磁盘空间","系统磁盘剩余容量",settings.StorageWidget,v=>settings.StorageWidget=v,true);
        Heading("显示颜色");
        var palette=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};
        foreach(var(name,hex) in new[]{("白色","#FFFFFF"),("黑色","#000000"),("暖黄","#FFD66E"),("橘色","#FFA45C"),("粉色","#FF9EC4"),("天蓝","#7FC5F5"),("薄荷绿","#7FE0C3"),("淡紫","#BFA8FF"),("青灰","#C4D3E0")})
        {
            var swatch=new Border{Width=30,Height=30,CornerRadius=new CornerRadius(15),Background=new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),BorderBrush=settings.WidgetColor==hex?new SolidColorBrush(System.Windows.Media.Color.FromRgb(96,171,255)):new SolidColorBrush(System.Windows.Media.Color.FromArgb(90,255,255,255)),BorderThickness=new Thickness(settings.WidgetColor==hex?2.4:1),Cursor=Cursors.Hand,Margin=new Thickness(0,0,8,0),ToolTip=name};
            swatch.MouseLeftButtonUp+=(_,_)=>{settings.WidgetColor=hex;WidgetPaint.Apply(hex);changed();Navigate(page);};
            palette.Children.Add(swatch);
        }
        var colorName=new TextBlock{Text="当前："+(new[]{("白色","#FFFFFF"),("黑色","#000000"),("暖黄","#FFD66E"),("橘色","#FFA45C"),("粉色","#FF9EC4"),("天蓝","#7FC5F5"),("薄荷绿","#7FE0C3"),("淡紫","#BFA8FF"),("青灰","#C4D3E0")}.FirstOrDefault(c=>c.Item2==settings.WidgetColor).Item1??settings.WidgetColor),FontSize=12,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center};
        palette.Children.Add(colorName);
        content.Children.Add(new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(14),Margin=new Thickness(6,6,6,14),Child=palette});
        content.Children.Add(Text("切换后立即生效，并随配置保存。",11));
                Heading("轮播设置");
        var intervals=new UniformGrid{Columns=5,Width=380,HorizontalAlignment=HorizontalAlignment.Left};
        foreach(int seconds in new[]{5,15,30,60}){var preset=Button(seconds+" 秒",()=>{settings.WidgetIntervalSeconds=seconds;changed();Navigate(page);});preset.Margin=new Thickness(0,0,8,0);preset.Background=settings.WidgetIntervalSeconds==seconds?Wash:CardBg;intervals.Children.Add(preset);}var customize=Button("自定义",()=>{customInterval=!customInterval;Navigate(page);});customize.Margin=new Thickness(0);customize.Background=customInterval?Wash:SettingsCard;intervals.Children.Add(customize);content.Children.Add(intervals);
        var custom=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,8)};var intervalInput=new TextBox{Text=settings.WidgetIntervalSeconds.ToString(),Width=90,Height=36,Padding=new Thickness(10,4,10,4),VerticalContentAlignment=VerticalAlignment.Center,ToolTip="自定义秒数，1–3600"};
        var apply=Button("应用秒数",()=>{if(int.TryParse(intervalInput.Text,out int seconds)&&seconds>=1&&seconds<=3600){settings.WidgetIntervalSeconds=seconds;changed();Navigate(page);}else intervalInput.ToolTip="请输入 1–3600 秒";});apply.Margin=new Thickness(10,0,0,0);custom.Children.Add(intervalInput);custom.Children.Add(apply);if(customInterval||!new[]{5,15,30,60}.Contains(settings.WidgetIntervalSeconds))content.Children.Add(custom);
        if(!settings.WeatherWidget)return;Heading("天气配置");
        var weather=new StackPanel();
        var current=new TextBlock{Text=string.IsNullOrEmpty(settings.WeatherCity)?"选择城市":settings.WeatherCity,FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(0,0,0,16)};weather.Children.Add(current);
        var input=new DockPanel{LastChildFill=true};weather.Children.Add(input);
        var results=new UniformGrid{Columns=2,Margin=new Thickness(0,12,0,0)};
        var status=Text("支持中文、拼音或英文",11);status.Margin=new Thickness(0,12,0,0);
        var search=Button("搜索",()=>{});search.Margin=new Thickness(10,0,0,0);search.MinWidth=80;search.HorizontalContentAlignment=HorizontalAlignment.Center;DockPanel.SetDock(search,Dock.Right);input.Children.Add(search);
        var city=new TextBox{Height=38,FontSize=13,Padding=new Thickness(12,4,12,4),ToolTip="城市名称，例如 上海 / Shanghai",VerticalContentAlignment=VerticalAlignment.Center};input.Children.Add(city);
        weather.Children.Add(status);weather.Children.Add(results);
        content.Children.Add(new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(14),Margin=new Thickness(6,6,6,14),Child=weather});
        city.KeyDown+=(_,e)=>{if(e.Key==Key.Enter&&search.IsEnabled){search.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));e.Handled=true;}};        search.Click+=async(_,_)=>
        {
            string query=city.Text.Trim();if(query.Length<2){status.Text="请输入至少两个字符的城市名称";return;}search.IsEnabled=false;status.Text="正在搜索…";results.Children.Clear();
            try
            {
                var places=await WeatherService.Search(query,lifetime.Token);if(lifetime.IsCancellationRequested||(page!="桌面偏好"||desktopTab!="桌面组件"))return;status.Text=places.Count==0?"未找到城市，试试英文或拼音名称":"请选择城市：";
                foreach(var place in places){var choice=Button(place.Name,()=>{settings.WeatherCity=place.Name;settings.WeatherLatitude=place.Latitude;settings.WeatherLongitude=place.Longitude;changed();Navigate("桌面组件");});choice.Content=new TextBlock{Text=place.Name,TextWrapping=TextWrapping.Wrap};choice.Margin=new Thickness(4);results.Children.Add(choice);}
            }
            catch(Exception ex) when(ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException){if(!lifetime.IsCancellationRequested){ActivityStore.Write("天气搜索失败",message:ex.Message,error:ex);status.Text="城市搜索失败，请检查网络后重试。";}}
            finally{search.IsEnabled=true;}
        };


        content.Children.Add(new Expander{Header="数据说明",Margin=new Thickness(0,12,0,0),Content=Text("GPU 为最繁忙引擎占用；网络包含虚拟网卡；天气为模型估算。",11)});
    }
    void Todos()
    {
        // —— 添加待办：内容输入 + 日期/时间选择 ——
        Heading("添加待办");
        var input=new TextBox{Height=38,FontSize=13,Padding=new Thickness(12,4,12,4),VerticalContentAlignment=VerticalAlignment.Center};
        if(input.Text.Length==0)input.ToolTip="输入待办内容，例如 周五提交报告";
        var pickers=new Grid{Margin=new Thickness(0,10,0,0)};
        pickers.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        pickers.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        pickers.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        pickers.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var day=new ComboBox{Width=128,Height=36,VerticalContentAlignment=VerticalAlignment.Center,FontSize=12.5,Margin=new Thickness(0,0,8,0)};
        var time=new ComboBox{Width=104,Height=36,VerticalContentAlignment=VerticalAlignment.Center,FontSize=12.5,Margin=new Thickness(0,0,8,0)};
        var today=DateTime.Today;
        for(int offset=0;offset<7;offset++){var date=today.AddDays(offset);day.Items.Add(new ComboBoxItem{Content=offset==0?"今天":offset==1?"明天":date.ToString("周ddd"),Tag=date});}
        var slots=new List<DateTime>();
        for(int hour=0;hour<24;hour++)foreach(var minute in new[]{0,30})slots.Add(new DateTime(today.Year,today.Month,today.Day,hour,minute,0));
        foreach(var slot in slots)time.Items.Add(new ComboBoxItem{Content=slot.ToString("HH:mm"),Tag=slot});
        day.SelectedIndex=0;time.SelectedIndex=0;
        var add=Button("添加",()=>{
            var text=input.Text.Trim();
            if(text.Length==0){input.Focus();return;}
            if(day.SelectedItem is not ComboBoxItem dayItem||dayItem.Tag is not DateTime date)return;
            if(time.SelectedItem is not ComboBoxItem timeItem||timeItem.Tag is not DateTime slot)return;
            var remind=new DateTime(date.Year,date.Month,date.Day,slot.Hour,slot.Minute,0);
            if(remind<=DateTime.Now)remind=remind.AddDays(1); // 已过时刻顺延一天，保持“未来一周内”有效
            settings.Todos.Add(new MainWindow.TodoItem{Text=text,RemindAt=remind});
            changed();input.Clear();input.Focus();Navigate(page);
        });
        add.MinWidth=88;Grid.SetColumn(add,3);
        Grid.SetColumn(day,1);Grid.SetColumn(time,2);
        pickers.Children.Add(day);pickers.Children.Add(time);pickers.Children.Add(add);
        var editor=new StackPanel{Children={input,pickers}};
        content.Children.Add(new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(14,12,12,12),Margin=new Thickness(6,4,6,10),Child=editor});
        content.Children.Add(Text("支持未来一周内 · 到点弹窗提醒未完成事项",11));
        // —— 未完成列表 ——
        var pending=settings.Todos.Where(item=>item!=null&&!item.Done).OrderBy(item=>item.RemindAt).ToList();
        var finished=settings.Todos.Where(item=>item!=null&&item.Done).OrderBy(item=>item.RemindAt).ToList();
        Heading(pending.Count==0?"未完成":$"未完成 · {pending.Count}");
        if(settings.Todos.Count==0)
        {
            var empty=new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(16,18,16,18),Margin=new Thickness(6,0,6,8),Child=new TextBlock{Text="暂无待办 · 添加后到点会自动弹窗提醒",FontSize=12,Foreground=Muted,TextAlignment=TextAlignment.Center,LineHeight=20}};
            content.Children.Add(empty);
        }
        foreach(var item in pending)content.Children.Add(TodoRow(item,true));
        if(finished.Count>0)
        {
            Heading($"已完成 · {finished.Count}");
            foreach(var item in finished)content.Children.Add(TodoRow(item,false));
            content.Children.Add(Text("已完成的待办可继续保留或删除，不影响未完成事项的提醒。",11));
        }
    }
    // 待办行卡片：左侧完成切换、中间内容与时间徽标（过期高亮）、右侧删除。
    FrameworkElement TodoRow(MainWindow.TodoItem item,bool pending)
    {
        var body=new StackPanel{Margin=new Thickness(12,0,8,0)};
        body.Children.Add(new TextBlock{Text=item.Text,FontSize=13.5,FontWeight=pending?FontWeights.Medium:FontWeights.Normal,Foreground=pending?Ink:Muted,TextWrapping=TextWrapping.Wrap,TextDecorations=pending?null:System.Windows.TextDecorations.Strikethrough});
        bool overdue=pending&&item.RemindAt<DateTime.Now;
        var chip=new Border{CornerRadius=new CornerRadius(7),Padding=new Thickness(7,2,7,2),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,5,0,0),Background=overdue?Theme.Get("ThemeDanger"):CardBg,Child=new TextBlock{Text=(overdue?"已过提醒时间 · ":"提醒 ")+item.RemindAt.ToString("MM/dd HH:mm"),FontSize=10.5,Foreground=overdue?Theme.Get("ThemeDanger"):Muted}};
        body.Children.Add(chip);
        var toggle=new Button{Content=new TextBlock{Text=pending?"✓":"",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=13,Foreground=pending?Muted:Ink},Style=(Style)FindResource("GlassButton"),Width=28,Height=28,Padding=new Thickness(0),Background=pending?Brushes.Transparent:CardBg,VerticalAlignment=VerticalAlignment.Center,ToolTip=pending?"标记为未完成":"标记完成"};
        toggle.Click+=(_,_)=>{item.Done=!item.Done;changed();Navigate(page);};
        var remove=new Button{Content=new TextBlock{Text="",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=12,Foreground=Muted},Style=(Style)FindResource("GlassButton"),Width=28,Height=28,Padding=new Thickness(0),Background=Brushes.Transparent,VerticalAlignment=VerticalAlignment.Center,ToolTip="删除"};
        remove.Click+=(_,_)=>{settings.Todos.Remove(item);changed();Navigate(page);};
        var row=new Grid{Opacity=pending?1:.72};
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        Grid.SetColumn(toggle,0);Grid.SetColumn(body,1);Grid.SetColumn(remove,2);
        row.Children.Add(toggle);row.Children.Add(body);row.Children.Add(remove);
        var card=new Border{Background=SettingsCard,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(16),Padding=new Thickness(10,9,8,9),Margin=new Thickness(6,0,6,8),Child=row};
        card.MouseEnter+=(_,_)=>card.Background=Theme.Get("ThemeHover");
        card.MouseLeave+=(_,_)=>card.Background=SettingsCard;
        return card;
    }
}
