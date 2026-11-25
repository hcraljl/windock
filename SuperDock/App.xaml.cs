using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace SuperDock;
public partial class App : Application
{
    SingleInstance? instance;
    DockCoordinator? coordinator;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Component test hosts load this application for its resources only.
        if(System.Reflection.Assembly.GetEntryAssembly()!=typeof(App).Assembly)return;
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try{
            instance=new SingleInstance();if(!instance.Acquire()){ActivityStore.Write("重复启动",message:"已通知正在运行的 windock 显示 Dock");instance.Dispose();instance=null;Shutdown();return;}
            instance.Listen(()=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>coordinator?.Reveal()));});
            // Updates are manual-only: the dock starts immediately and never phones home on launch.
            var settings=SuperDock.MainWindow.ReadSettings();Theme.Apply(settings.ThemeMode);_=Task.Run(Branding.RefreshShortcuts);
            coordinator=new DockCoordinator(settings);MainWindow=coordinator.Primary;coordinator.Start();ActivityStore.Write("启动完成",message:$"windock {typeof(App).Assembly.GetName().Version!.ToString(3)} · {coordinator.Docks.Count} 个显示器");
            // 更新流程（源始终是 GitHub Releases）：启动自动更新（可跳过）+ 运行中仅提醒。
            UpdatePrompt.BeginStartupCheck(coordinator.Primary,settings);
            UpdatePrompt.BeginRuntimeWatch(coordinator.Primary,settings);
        }catch(Exception ex){ActivityStore.Write("启动失败",message:ex.Message,error:ex);GlassDialog.Show("启动失败："+ex.Message,"windock");Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e){coordinator?.Dispose();instance?.Dispose();base.OnExit(e);}
    public App()
    {
        ActivityStore.Write("程序启动",message:"windock 启动");
        DispatcherUnhandledException+=(_,e)=>ActivityStore.Write("界面异常",message:e.Exception.Message,error:e.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>ActivityStore.Write("程序崩溃",message:"未处理异常",error:e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException+=(_,e)=>ActivityStore.Write("异步异常",message:e.Exception.Message,error:e.Exception);
        Exit+=(_,_)=>ActivityStore.Write("程序退出",message:"windock 退出");
    }
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
    static bool ContainsCursor(FrameworkElement element,CursorPoint cursor)
    {
        if(!element.IsVisible||element.ActualWidth<=0||element.ActualHeight<=0)return false;
        try{var start=element.PointToScreen(new Point(0,0));var end=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));return cursor.X>=start.X&&cursor.X<end.X&&cursor.Y>=start.Y&&cursor.Y<end.Y;}catch(InvalidOperationException){return false;}
    }
    static bool PointerInside(ContextMenu menu,CursorPoint cursor)
    {
        if(ContainsCursor(menu,cursor))return true;
        foreach(var item in menu.Items.OfType<MenuItem>())
        {
            if(item.ContextMenu is {IsOpen:true} child&&PointerInside(child,cursor))return true;
            if(item.IsSubmenuOpen&&item.Template.FindName("PART_Popup",item) is System.Windows.Controls.Primitives.Popup {Child:FrameworkElement panel}&&ContainsCursor(panel,cursor))return true;
        }
        return false;
    }
    static void CloseMenu(ContextMenu menu)
    {
        foreach(var item in menu.Items.OfType<MenuItem>()){item.IsSubmenuOpen=false;if(item.ContextMenu is {IsOpen:true} child)CloseMenu(child);}
        menu.IsOpen=false;
    }
    void MenuOpened(object sender,RoutedEventArgs e)
    {
        if(sender is not ContextMenu menu)return;
        foreach(var item in menu.Items.OfType<MenuItem>())
        {
            if(item.Icon!=null)continue;
            var name=item.Header?.ToString()??"";
            string glyph=name switch
            {
                "退出程序"=>"\uE8BB","强制退出…"=>"\uE7BA","显示窗口"=>"\uE737","最大化窗口"=>"\uE922","最小化窗口"=>"\uE921","向前移动"=>"\uE72B","向后移动"=>"\uE72A","移除入口"=>"\uE74D",
                _ when name.StartsWith("移入分组")=>"\uE8F1",
                _ when name.StartsWith("移回")=>"\uE8A7",
                _ when name.StartsWith("解散")=>"\uE8EA",
                _ when name.Contains("选择")||name.Contains("资源管理器")=>"\uE8B7",
                _=>"\uE71D"
            };
            bool danger=name.StartsWith("强制退出")||name=="移除入口";
            item.Icon=new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=15,Foreground=danger?Theme.Get("ThemeDanger"):Theme.Muted,VerticalAlignment=VerticalAlignment.Center};
            if(danger)item.Foreground=Theme.Get("ThemeDanger");
        }
        // Give the pointer time to reach a menu opened beside the icon, then dismiss after leaving.
        var opened=DateTime.UtcNow;DateTime? left=null;bool entered=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};

        timer.Tick+=(_,_)=>
        {
            if(!menu.IsOpen){timer.Stop();return;}
            if(!GetCursorPos(out var cursor))return;
            if(PointerInside(menu,cursor)){entered=true;left=null;return;}
            if(!entered&&(DateTime.UtcNow-opened).TotalMilliseconds<1000)return;
            left??=DateTime.UtcNow;
            if((DateTime.UtcNow-left.Value).TotalMilliseconds>=350){CloseMenu(menu);timer.Stop();}
        };
        timer.Start();
    }
}




