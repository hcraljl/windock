using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Interop;
namespace SuperDock;
public partial class MainWindow
{
    [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
    string registeredShortcut="";bool manuallyHidden;
    internal static string ShortcutStatus {get;private set;}="等待注册";
    string? fileSearchRegistered;
    internal static string FileSearchShortcutStatus {get;private set;}="等待注册";
    string? screenshotRegistered,pinRegistered,translateRegistered;
    bool settingsShortcutRegistered;
    internal static string SettingsShortcutStatus {get;private set;}="等待注册";
    internal static string ScreenshotShortcutStatus {get;private set;}="等待注册";
    internal static string PinShortcutStatus {get;private set;}="等待注册";
    internal static string TranslateShortcutStatus {get;private set;}="等待注册";
    internal static uint FunctionKey(string key)=>key switch{"F1"=>0x70,"F2"=>0x71,"F3"=>0x72,"F4"=>0x73,"F5"=>0x74,"F6"=>0x75,_=>0x70};
    void UpdateTranslateShortcut()
    {
        var desired=settings.TranslateHotkeyEnabled?settings.TranslateHotkey:"";if(translateRegistered==desired)return;translateRegistered=desired;
        var h=new WindowInteropHelper(this).Handle;UnregisterHotKey(h,0x5149);TranslateShortcutStatus=desired==""?"已关闭":"已启用";
        if(desired=="")return;
        bool ctrlShift=desired=="Ctrl+Shift+F";
        bool ok=RegisterHotKey(h,0x5149,(ctrlShift?6u:1u)|0x4000,0x46);
        if(!ok&&settings.FileSearchHotkeyEnabled&&settings.FileSearchHotkey==desired)TranslateShortcutStatus="与文件搜索快捷键冲突，请更换";
        else TranslateShortcutStatus=ok?"已启用":"快捷键被其他程序占用，请更换";
    }
    void UpdateScreenshotShortcut()
    {
        var desired=settings.ScreenshotHotkeyEnabled?settings.ScreenshotHotkey:"";if(screenshotRegistered==desired)return;screenshotRegistered=desired;
        var h=new WindowInteropHelper(this).Handle;UnregisterHotKey(h,0x5147);ScreenshotShortcutStatus=desired==""?"已关闭":null!;
        if(desired=="")return;
        bool ok=RegisterHotKey(h,0x5147,0u|0x4000,FunctionKey(desired));
        if(!ok&&settings.PinHotkeyEnabled&&settings.PinHotkey==desired)ScreenshotShortcutStatus="与贴图快捷键冲突，请更换其中之一";
        else ScreenshotShortcutStatus=ok?"已启用":"快捷键被其他程序占用，请更换";
    }
    void UpdatePinShortcut()
    {
        var desired=settings.PinHotkeyEnabled?settings.PinHotkey:"";
        if(pinRegistered==desired)return;pinRegistered=desired;
        var h=new WindowInteropHelper(this).Handle;UnregisterHotKey(h,0x5148);PinShortcutStatus=desired==""?"已关闭":null!;
        if(desired=="")return;
        if(settings.ScreenshotHotkeyEnabled&&settings.ScreenshotHotkey==desired){PinShortcutStatus="与截图快捷键冲突，请更换其中之一";pinRegistered=null;return;} // 让位给截图键；后续设置变化时重试
        bool ok=RegisterHotKey(h,0x5148,0u|0x4000,FunctionKey(desired));
        PinShortcutStatus=ok?"已启用":"快捷键被其他程序占用，请更换";
        if(!ok&&settings.ScreenshotHotkeyEnabled&&settings.ScreenshotHotkey==desired){PinShortcutStatus="与截图快捷键冲突，请更换其中之一";pinRegistered=null;}
    }
    void UpdateFileSearchShortcut(){var desired=settings.FileSearchHotkeyEnabled?settings.FileSearchHotkey:"";if(fileSearchRegistered==desired)return;fileSearchRegistered=desired;var h=new WindowInteropHelper(this).Handle;UnregisterHotKey(h,0x5146);bool space=desired=="Alt+Space";FileSearchShortcutStatus=desired==""?"已关闭":RegisterHotKey(h,0x5146,(space?1u:3u)|0x4000,space?0x20u:0x46u)?"已启用":"快捷键被其他程序占用，请更换，或点击 Dock 搜索图标";}
    // Alt+S：全局打开/关闭设置窗口（不可配置，始终注册）。
    void UpdateSettingsShortcut()
    {
        if(settingsShortcutRegistered)return;
        var h=new WindowInteropHelper(this).Handle;
        settingsShortcutRegistered=RegisterHotKey(h,0x5145,1u|0x4000,0x53); // MOD_ALT|MOD_NOREPEAT · VK_S
        SettingsShortcutStatus=settingsShortcutRegistered?"已启用":"Alt+S 被其他程序占用，设置窗口仍可从 Dock 图标打开";
    }
    void ToggleSettingsWindow()
    {
        var target=Coordinator?.Primary??this;
        if(target.appearanceWindow is { IsVisible:true } open)
        {
            open.Close();ActivityStore.Write("设置窗口",message:"Alt+S 关闭设置窗口");return;
        }
        target.OpenSettingsFor(target);
        ActivityStore.Write("设置窗口",message:"Alt+S 打开设置窗口");
    }
    void UpdateShortcut()
    {
        if(!IsLoaded||!HandlesGlobalShortcut)return;UpdateFileSearchShortcut();UpdateTranslateShortcut();UpdatePinShortcut();UpdateScreenshotShortcut();UpdateSettingsShortcut();var desired=settings.DockHotkeyEnabled?settings.DockHotkey:"";if(registeredShortcut==desired)return;
        var handle=new WindowInteropHelper(this).Handle;UnregisterHotKey(handle,0x5144);registeredShortcut=desired;
        if(desired==""){ShortcutStatus="已关闭";return;}
        uint mods=desired switch{"Alt+D"=>1u,"Ctrl+Shift+D"=>6u,_=>3u};
        bool ok=RegisterHotKey(handle,0x5144,mods|0x4000,0x44);ShortcutStatus=ok?"已启用":"快捷键被占用，请更换";
        ActivityStore.Write("快捷键",message:ShortcutStatus+" · "+desired);
    }
    IntPtr ShortcutHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x0312&&wParam.ToInt32()==0x5145){handled=true;ToggleSettingsWindow();return IntPtr.Zero;}
        if(message==0x0312&&wParam.ToInt32()==0x5146){handled=true;ShowFileSearch();return IntPtr.Zero;}
        if(message==0x0312&&wParam.ToInt32()==0x5147){handled=true;Screenshot.Begin(settings,this);ActivityStore.Write("截图",message:"快捷键触发 · "+settings.ScreenshotHotkey);return IntPtr.Zero;}
        if(message==0x0312&&wParam.ToInt32()==0x5148){handled=true;Screenshot.PinFromClipboard(settings);return IntPtr.Zero;}
        if(message==0x0312&&wParam.ToInt32()==0x5149){handled=true;Screenshot.Translate(settings,this);ActivityStore.Write("翻译",message:"快捷键触发");return IntPtr.Zero;}
        if(message==0x0312&&wParam.ToInt32()==0x5144){handled=true;if(Coordinator!=null)Coordinator.Toggle();else SetManualHidden(!manuallyHidden&&!IsLayerLowered);ActivityStore.Write("Dock显隐",message:manuallyHidden?"快捷键隐藏":"快捷键显示");}return IntPtr.Zero;
    }
    internal void SetManualHidden(bool value){manuallyHidden=value;hideTimer.Stop();previewTimer.Stop();preview?.Close();preview=null;groupFlyout?.Collapse();if(value)Hide();else{hidden=false;if(!IsVisible)Show();RaiseDock();Position();ScheduleHide();}}
    // 今日待办：主 Dock 每 30 秒检查到点未完成事项并弹窗提醒；弹窗引用挂在主窗口，避免多屏重复弹窗。
    internal Window? TodoReminderWindow {get;set;}
    readonly DispatcherTimer todoTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    void CheckTodoReminder()=>Dispatcher.BeginInvoke(new Action(()=>{if(!closed)TodoReminder.CheckAndShow(this,settings,()=>{Theme.Apply(settings.ThemeMode);Save();Render();});}));
    // 截图期间临时隐藏所有 Dock 窗口，避免自身入镜；结束后按原透明度还原。
    readonly List<(MainWindow Window,double Opacity)> captureHidden=[];
    internal void SuppressForCapture()
    {
        captureHidden.Clear();var docks=Coordinator!=null?Coordinator.Docks.ToList():new List<MainWindow>();if(!docks.Contains(this))docks.Add(this);
        foreach(var dock in docks){captureHidden.Add((dock,dock.Opacity));dock.Opacity=0;}
    }
    internal void RestoreFromCapture()
    {
        foreach(var(dock,opacity) in captureHidden)try{dock.Opacity=opacity;}catch(InvalidOperationException){}
        captureHidden.Clear();
    }
}


