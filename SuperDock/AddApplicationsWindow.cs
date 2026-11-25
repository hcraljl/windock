using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
namespace SuperDock;
internal sealed class AddApplicationsWindow : Window
{
    readonly MainWindow.Settings settings;
    readonly Action<MainWindow.Entry> add;
    readonly UniformGrid cards=new(){Columns=6,VerticalAlignment=VerticalAlignment.Top};
    public Action? Changed {get;set;}
    readonly bool launcher;
    readonly MainWindow.AppGroup? group;
    readonly TextBox search=new(){ToolTip="搜索应用",Margin=new Thickness(0,14,0,14)};
    readonly TextBlock status=new(){FontSize=11,Foreground=Theme.Muted,Margin=new Thickness(0,0,0,12)};
    readonly DispatcherTimer searchTimer=new(){Interval=TimeSpan.FromMilliseconds(120)};
    readonly CancellationTokenSource lifetime=new();
    readonly List<(string Target,Button Button,MenuItem Menu)> pins=[];
    List<MainWindow.Entry> apps=[];
    bool choosing,closing,ready;
    int generation;
    public AddApplicationsWindow(MainWindow.Settings settings,Action<MainWindow.Entry> add,bool launcher=false,MainWindow.AppGroup? group=null)
    {
        this.settings=settings;this.add=add;this.launcher=launcher;this.group=group;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Windock;component/SettingsTheme.xaml",UriKind.Relative)});
        Title=group?.Name??(launcher?"应用中心":"添加应用");Width=920;Height=660;MaxHeight=SystemParameters.WorkArea.Height-40;MaxWidth=SystemParameters.WorkArea.Width-40;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;Foreground=Theme.Text;FontFamily=new FontFamily("Microsoft YaHei UI");
        if(group==null&&launcher&&settings.ApplicationCenterFullscreen){var area=SystemParameters.WorkArea;MaxWidth=area.Width;MaxHeight=area.Height;Width=area.Width;Height=area.Height;}
        if(group!=null){Width=Math.Min(MaxWidth,Math.Clamp(group.Entries.Count,3,5)*136+64);Height=Math.Min(MaxHeight,190+Math.Max(1,(int)Math.Ceiling(group.Entries.Count/(double)Math.Clamp(group.Entries.Count,3,5)))*158);}
        var body=new DockPanel();var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
        var header=new DockPanel();var close=Button("✕",Close);DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(new TextBlock{Text=Title,FontSize=23,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});top.Children.Add(header);
        if(group!=null){var remove=Button("移除分组",()=>{settings.Groups.Remove(group);Changed?.Invoke();Close();});remove.HorizontalAlignment=HorizontalAlignment.Right;remove.Margin=new Thickness(0,8,0,0);top.Children.Add(remove);}
        search.Height=42;search.Padding=new Thickness(12,6,12,6);
        var searchRow=new DockPanel{MaxWidth=group==null&&launcher&&settings.ApplicationCenterFullscreen?560:double.PositiveInfinity,HorizontalAlignment=HorizontalAlignment.Stretch};
        var searchIcon=new TextBlock{Text="\uE721",FontFamily=new FontFamily("Segoe Fluent Icons"),Foreground=Theme.Muted,FontSize=18,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,8,0)};DockPanel.SetDock(searchIcon,Dock.Left);searchRow.Children.Add(searchIcon);searchRow.Children.Add(search);
        if(group==null||group.Entries.Count>8)top.Children.Add(searchRow);
        SizeChanged+=(_,_)=>cards.Columns=Math.Max(3,(int)((ActualWidth-60)/(group==null&&launcher&&settings.ApplicationCenterFullscreen?180:135)));top.Children.Add(status);
        var file=Button("选择程序或文件…",()=>{
            choosing=true;try{var dialog=new OpenFileDialog{Multiselect=true,Title="添加到 Dock"};if(dialog.ShowDialog(this)==true){foreach(var path in dialog.FileNames)add(new MainWindow.Entry(System.IO.Path.GetFileNameWithoutExtension(path),path));RefreshPins();}}finally{choosing=false;}
        });file.Margin=new Thickness(0,12,0,0);DockPanel.SetDock(file,Dock.Bottom);if(group==null)body.Children.Add(file);
        body.Children.Add(new ScrollViewer{Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        GlassWindow.Prepare(this);Content=new Border{Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(23),Padding=new Thickness(28),Child=body};
        search.TextChanged+=(_,_)=>{searchTimer.Stop();searchTimer.Start();};searchTimer.Tick+=(_,_)=>{searchTimer.Stop();if(ready&&!closing)Refresh();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
        Closing+=(_,_)=>{closing=true;searchTimer.Stop();lifetime.Cancel();};Closed+=(_,_)=>lifetime.Dispose();
        Deactivated+=(_,_)=>{if(!choosing&&!closing)Dispatcher.BeginInvoke(new Action(()=>{if(!choosing&&!closing&&!IsActive&&IsVisible)Close();}));};
        Loaded+=async(_,_)=>{
            var area=SystemParameters.WorkArea;Left=area.Left+(area.Width-ActualWidth)/2;Top=area.Top+(area.Height-ActualHeight)/2;search.Focus();status.Text="正在读取应用…";
            try{
                apps=group!=null?group.Entries.ToList():(await ApplicationCatalog.GetAsync().WaitAsync(lifetime.Token)).ToList();
                if(closing)return;ready=true;searchTimer.Stop();Refresh();
            }catch(OperationCanceledException){}catch(Exception ex){if(!closing)status.Text="应用读取失败，请重新打开";ActivityStore.Write("应用中心读取失败",message:ex.Message,error:ex);}
        };
    }
    Button Button(string text,Action action){var b=new Button{Content=text,Style=(Style)FindResource("SettingsButton")};b.Click+=(_,_)=>action();return b;}
    void Pin(MainWindow.Entry app){if(!ApplicationPlacement.Read(settings).ContainsKey(RunningWindows.WindowTarget(app.Target)))add(app);RefreshPins();}
    void RefreshPins()
    {
        var placements=ApplicationPlacement.Read(settings);
        foreach(var item in pins){
            placements.TryGetValue(item.Target,out var placement);bool added=placement!=null;
            item.Button.Content=placement?.Label??"＋ 添加到 Dock";item.Button.ToolTip=placement?.Description??"添加到 Dock";
            item.Button.Background=added?Theme.Card:Theme.Selected;item.Button.IsEnabled=!added;item.Button.Opacity=1;
            ToolTipService.SetShowOnDisabled(item.Button,true);
            item.Menu.Header=placement?.Label??"添加到 Dock";item.Menu.ToolTip=placement?.Description;item.Menu.IsEnabled=!added;
        }
    }
    async Task LoadIcon(Image image,Border placeholder,string target,int version)
    {
        try{var icon=await ShellIcons.LoadAsync(target).WaitAsync(lifetime.Token);if(closing||version!=generation||icon==null)return;image.Source=icon;placeholder.Visibility=Visibility.Collapsed;}catch(OperationCanceledException){}
    }
    void Refresh()
    {
        int version=++generation;
        if(group!=null){int columns=Math.Clamp(group.Entries.Count,3,5);Width=Math.Min(MaxWidth,columns*136+64);Height=group.Entries.Count==0?220:Math.Min(MaxHeight,(group.Entries.Count>8?240:190)+Math.Max(1,(int)Math.Ceiling(group.Entries.Count/(double)columns))*158);}
        cards.Children.Clear();pins.Clear();var matches=apps.Where(a=>a.Name.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToArray();status.Text=$"{matches.Length} 个应用";
        if(launcher&&group==null&&string.IsNullOrWhiteSpace(search.Text))foreach(var folder in settings.Groups){
            var face=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};face.Children.Add(GroupIcons.Create(folder,56));face.Children.Add(new TextBlock{Text=folder.Name,Foreground=Theme.Text,FontSize=12,Margin=new Thickness(0,12,0,0),HorizontalAlignment=HorizontalAlignment.Center});
            var tile=Button("",()=>{choosing=true;var window=new AddApplicationsWindow(settings,add,true,folder){Owner=Owner,Changed=Changed};window.Closed+=(_,_)=>{choosing=false;if(IsVisible){RefreshPins();Activate();}};window.Show();});tile.Style=(Style)FindResource("LauncherTile");tile.Content=face;tile.Height=settings.ApplicationCenterFullscreen?180:142;tile.Margin=new Thickness(8);tile.Background=Theme.Card;
            var folderMenu=new ContextMenu();var delete=new MenuItem{Header="移除分组"};delete.Click+=(_,_)=>{settings.Groups.Remove(folder);Changed?.Invoke();Refresh();};folderMenu.Items.Add(delete);tile.ContextMenu=folderMenu;cards.Children.Add(tile);
        }
        foreach(var app in matches){
            double size=group==null&&launcher&&settings.ApplicationCenterFullscreen?72:48;
            var face=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};var launchFace=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
            var iconHost=new Grid{Width=size,Height=size,Margin=new Thickness(0,0,0,8)};
            var placeholder=new Border{Background=Theme.Card,CornerRadius=new CornerRadius(size*.23),Child=new TextBlock{Text=app.Name.Length>0?app.Name[..1]:"▦",Foreground=Theme.Muted,FontSize=22,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center}};
            var image=new Image{Source=ShellIcons.Cached(app.Target),Width=size,Height=size};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);iconHost.Children.Add(placeholder);iconHost.Children.Add(image);if(image.Source!=null)placeholder.Visibility=Visibility.Collapsed;else _=LoadIcon(image,placeholder,app.Target,version);
            launchFace.Children.Add(iconHost);launchFace.Children.Add(new TextBlock{Text=app.Name,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=100,TextAlignment=TextAlignment.Center,FontSize=12,Foreground=Theme.Text,Height=18});
            var launch=Button("",()=>{if(ActivityStore.Launch(app.Target,app.Name))Close();});launch.Style=(Style)FindResource("LauncherTile");launch.Content=launchFace;launch.Background=Brushes.Transparent;launch.Padding=new Thickness(5);launch.ToolTip="打开 "+app.Name;face.Children.Add(launch);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,0)};
            var pinButton=Button("＋ 添加到 Dock",()=>Pin(app));pinButton.FontSize=10;pinButton.Padding=new Thickness(6,5,6,5);pinButton.Margin=new Thickness(0);actions.Children.Add(pinButton);face.Children.Add(actions);
            var tile=new Border{Child=face,Background=Brushes.Transparent,CornerRadius=new CornerRadius(14),Margin=new Thickness(3,8,3,8),Height=group==null&&launcher&&settings.ApplicationCenterFullscreen?190:156,Padding=new Thickness(2,8,2,8),VerticalAlignment=VerticalAlignment.Top,ToolTip=app.Name};
            var menu=new ContextMenu();var pin=new MenuItem{Header="固定到 Dock"};pin.Click+=(_,_)=>Pin(app);menu.Items.Add(pin);pins.Add((RunningWindows.WindowTarget(app.Target),pinButton,pin));
            if(group!=null)foreach(var (permanent,label) in new[]{(false,"移出分组，回到 Dock"),(true,"彻底移除入口")}){
                var remove=new MenuItem{Header=label};remove.Click+=(_,_)=>{MainWindow.RemoveGroupedEntry(settings,group,app,permanent);apps=group.Entries.ToList();Changed?.Invoke();Refresh();};menu.Items.Add(remove);
            }
            tile.ContextMenu=menu;cards.Children.Add(tile);
        }
        RefreshPins();if(matches.Length==0)status.Text=group!=null?"分组暂无应用":"没有匹配的应用";
    }
}
