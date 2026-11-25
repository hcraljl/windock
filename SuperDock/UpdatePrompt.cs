using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace SuperDock;

// 更新行为（更新源始终是 GitHub Releases）：
// · 启动时（dock 之前未打开）：发现新版本后自动下载安装，更新窗口右下角提供
//   "跳过更新并进入"，点击则取消本次下载并直接使用当前版本；
// · 运行中（dock 已打开时发布了新版本）：只弹气泡提醒用户手动检查，不自动下载；
// · 设置页"检查更新"手动入口保持不变，可用"启动时自动更新"开关关闭自动流程。
internal static class UpdatePrompt
{
    static string? promptedVersion;
    public static async void BeginStartupCheck(MainWindow dock,MainWindow.Settings settings)
    {
        if(!settings.AutomaticUpdates)return;
        try
        {
            await Task.Delay(4000);
            if(dock.IsDockClosed||!dock.IsLoaded)return;
            var release=await UpdateService.Check();
            if(release==null||dock.IsDockClosed)return;
            ShowInstaller(dock,release);
        }
        catch(Exception ex){ActivityStore.Write("更新检查",message:"启动检查失败："+ex.Message);}
    }
    public static void BeginRuntimeWatch(MainWindow dock,MainWindow.Settings settings)
    {
        if(dock.Coordinator==null||dock.Coordinator.Docks.Count==0)return;
        bool busy=false;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMinutes(30)};
        timer.Tick+=async(_,_)=>{
            if(busy||dock.IsDockClosed)return;busy=true;
            try
            {
                var version=await UpdateService.QuietCheck();
                if(version==null||dock.IsDockClosed||version==promptedVersion)return;
                promptedVersion=version;
                ActivityStore.Write("更新提醒",message:"运行中发现新版本 "+version+"，已提醒手动检查");
                ShowNotice(dock,version);
            }
            finally{busy=false;}
        };
        dock.Closed+=(_,_)=>timer.Stop();
        timer.Start();
    }
    // 运行中提示气泡：锚在设置图标上方，仅提醒，不自动下载；点击打开设置页。
    static void ShowNotice(MainWindow dock,string version)
    {
        var anchor=dock.SettingsAnchor();if(anchor==null)return;
        var finish=dock.BeginMessage(anchor);
        var popup=new Window{Title="windock · 更新提醒",Width=316,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Owner=dock,AllowsTransparency=true,Background=Brushes.Transparent};
        var body=new StackPanel();
        var header=new DockPanel();var badge=new TextBlock{Text="新版本",FontSize=10.5,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(badge,Dock.Right);header.Children.Add(badge);header.Children.Add(new TextBlock{Text="发现 windock "+version,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text});body.Children.Add(header);
        body.Children.Add(new TextBlock{Text="更新源已发布新版本。请在 高级设置 › 关于更新 › 检查更新 中手动升级。",FontSize=12,Foreground=Theme.Text,Opacity=.86,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
        body.Children.Add(new TextBlock{Text="点击打开设置",FontSize=10.5,Foreground=Theme.Muted,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,9,0,0)});
        popup.Content=new Border{CornerRadius=new CornerRadius(16),Background=MessageGlass.PopupSurface("windock",dock.MessageReminderOpacity),BorderBrush=MessageGlass.Rim,BorderThickness=new Thickness(.7),Padding=new Thickness(16,13,16,14),Child=body,Cursor=System.Windows.Input.Cursors.Hand};
        popup.MouseLeftButtonUp+=(_,_)=>{popup.Close();if(!dock.IsDockClosed)dock.OpenSettingsFor(dock);};
        void Position(){if(!anchor.IsVisible){popup.Close();return;}var a=anchor.PointToScreen(new Point());var b=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight));var dpi=VisualTreeHelper.GetDpi(popup);double w=popup.ActualWidth*dpi.DpiScaleX,h=popup.ActualHeight*dpi.DpiScaleY;double x=(a.X+b.X-w)/2,y=a.Y-h-12;
            if(dock.DockEdge=="Top")y=b.Y+12;else if(dock.DockEdge=="Left"){x=b.X+12;y=(a.Y+b.Y-h)/2;}else if(dock.DockEdge=="Right"){x=a.X-w-12;y=(a.Y+b.Y-h)/2;}
            var area=dock.PhysicalWorkArea;DesktopIntegration.MovePhysical(popup,Math.Clamp(x,area.Left+6,Math.Max(area.Left+6,area.Right-w-6)),Math.Clamp(y,area.Top+6,Math.Max(area.Top+6,area.Bottom-h-6)));
        }
        EventHandler moved=(_,_)=>Position();SizeChangedEventHandler resized=(_,_)=>Position();dock.LocationChanged+=moved;dock.SizeChanged+=resized;
        popup.Loaded+=(_,_)=>Position();
        var auto=new DispatcherTimer{Interval=TimeSpan.FromSeconds(9)};
        auto.Tick+=(_,_)=>{auto.Stop();popup.Close();};
        popup.Closed+=(_,_)=>{auto.Stop();dock.LocationChanged-=moved;dock.SizeChanged-=resized;finish();};
        popup.Show();auto.Start();
    }
    // 启动自动更新窗口：发现新版本后自动下载，右下角"跳过更新并进入"随时可取消。
    static void ShowInstaller(MainWindow dock,UpdateRelease release)
    {
        ActivityStore.Write("自动更新",message:"发现新版本 "+release.Version+"，开始自动下载");
        var cancel=new System.Threading.CancellationTokenSource();
        var window=new Window{Title="windock · 自动更新",Width=420,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        GlassWindow.Prepare(window);
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock{Text="发现新版本 "+release.Version,FontSize=19,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text});
        var notes=(release.Notes??"").Replace("\r","").Trim();int cut=notes.IndexOf("## 安装包",StringComparison.Ordinal);if(cut>0)notes=notes[..cut].Trim();if(notes.Length>240)notes=notes[..240].TrimEnd()+"…";
        if(notes.Length>0)panel.Children.Add(new TextBlock{Text=notes,TextWrapping=TextWrapping.Wrap,Foreground=Theme.Muted,FontSize=12.5,Margin=new Thickness(0,12,0,0),MaxHeight=180});
        var status=new TextBlock{Text="正在连接更新源…",FontSize=12.5,Foreground=Theme.Text,Margin=new Thickness(0,16,0,0)};panel.Children.Add(status);
        var actions=new DockPanel{Margin=new Thickness(0,18,0,0)};
        var skip=new Button{Content="跳过更新并进入",Style=(Style)Application.Current.FindResource("GlassButton"),MinWidth=120,Padding=new Thickness(16,9,16,9),HorizontalAlignment=HorizontalAlignment.Right};
        skip.Click+=(_,_)=>{cancel.Cancel();ActivityStore.Write("自动更新",message:"已跳过更新 "+release.Version+"，直接进入");window.Close();};
        actions.Children.Add(skip);panel.Children.Add(actions);
        window.Content=new Border{CornerRadius=new CornerRadius(20),Padding=new Thickness(24),BorderThickness=new Thickness(1),Background=GlassWindow.Surface,BorderBrush=(Brush)Application.Current.FindResource("GlassStroke"),Child=panel};
        bool installing=false;
        window.Closed+=(_,_)=>cancel.Cancel();
        _=RunAsync();
        window.Show();
        async Task RunAsync()
        {
            try
            {
                var progress=new Progress<int>(p=>window.Dispatcher.Invoke(()=>status.Text="正在下载 "+p+"%…"));
                var path=await UpdateService.Download(release,progress,cancel.Token);
                if(cancel.IsCancellationRequested)return;
                status.Text="校验通过，正在安装…";installing=true;skip.IsEnabled=false;
                UpdateService.Install(path);
            }
            catch(OperationCanceledException){}
            catch(Exception ex){ActivityStore.Write("自动更新",message:"自动更新失败："+ex.Message,error:ex);if(!cancel.IsCancellationRequested){status.Text="自动更新失败："+ex.Message+"（可在设置中手动检查）";skip.Content="关闭并进入";}}
        }
    }
}
