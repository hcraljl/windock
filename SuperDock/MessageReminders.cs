using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
namespace SuperDock;

// Independently implemented against the public Windows shell-hook API.
internal static class MessageReminders
{
    public static readonly string[] Clients=["QQ","微信","企业微信","钉钉","飞书"];
    static readonly Dictionary<string,long> last=[];
    static Window? toast;static string? toastClient;static TextBlock? toastSender,toastBody;static DispatcherTimer? dismiss;
    [DllImport("user32.dll")]static extern bool RegisterShellHookWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    public static string? Client(string executable)=>System.IO.Path.GetFileNameWithoutExtension(executable).ToLowerInvariant() switch{"qq"=>"QQ","weixin" or "wechat"=>"微信","wxwork"=>"企业微信","dingtalk"=>"钉钉","feishu" or "lark"=>"飞书",_=>null};
    public static void Attach(MainWindow window,MainWindow.Settings settings)
    {
        MessageContentSources.Start(window,settings);
        var handle=new WindowInteropHelper(window).Handle;uint message=RegisterWindowMessage("SHELLHOOK");
        if(!RegisterShellHookWindow(handle)){ActivityStore.Write("消息提醒",message:"系统提醒监听注册失败");return;}
        var pending=new HashSet<IntPtr>();
        HwndSource.FromHwnd(handle)?.AddHook((IntPtr h,int msg,IntPtr kind,IntPtr source,ref bool handled)=>{
            if(msg==(int)message&&kind.ToInt64()==0x8006&&settings.MessageRemindersEnabled&&pending.Add(source)){
                // The native hook must return immediately: even ProcessName can block on another process.
                _=HandleFlash(source);
            }
            return IntPtr.Zero;
        });
        async Task HandleFlash(IntPtr source)
        {
            try{
                var signal=await Task.Run(()=>{
                    GetWindowThreadProcessId(source,out uint pid);
                    using var process=Process.GetProcessById((int)pid);
                    return (Client:Client(process.ProcessName),Pid:pid);
                });
                var client=signal.Client;if(window.IsDockClosed||client==null||!settings.MessageRemindersEnabled||!settings.MessageReminderClients.Contains(client))return;
                string key=client+":"+signal.Pid;long now=Environment.TickCount64;
                // A continuous flash is one unread episode, not one new message per blink.
                bool repeated=last.TryGetValue(key,out var time)&&now-time<5000;last[key]=now;if(repeated)return;
                if(MessageContentSources.RecentToast(client))return;
                Display(window,settings,client,client+" 有新的消息或提醒",source);
                var preview=client=="微信"&&MessageContentSources.Details(settings,client)?await MessageContentSources.WechatPreview(source):null;
                if(preview!=null&&!MessageContentSources.RecentToast(client)&&!window.IsDockClosed&&settings.MessageRemindersEnabled&&settings.MessageReminderClients.Contains(client))UpdateOrDisplay(window,settings,client,preview,source);
            }catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
            finally{pending.Remove(source);}
        }
    }
    // 预览时长由设置驱动：1-5 秒自动收起，0 = 一直显示直到用户点击查看。
    public static string Describe(MainWindow.Settings settings)=>settings.MessagePreviewSeconds<=0?"一直显示":settings.MessagePreviewSeconds+" 秒";
    static void ArmDismiss(MainWindow.Settings settings,Window popup)
    {
        dismiss?.Stop();dismiss=null;
        if(settings.MessagePreviewSeconds<=0)return;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(Math.Max(300,settings.MessagePreviewSeconds*1000-160))};
        timer.Tick+=(_,_)=>{timer.Stop();var fade=new System.Windows.Media.Animation.DoubleAnimation(0,TimeSpan.FromMilliseconds(160));fade.Completed+=(_,_)=>{if(popup.IsVisible)popup.Close();};popup.BeginAnimation(UIElement.OpacityProperty,fade);};
        dismiss=timer;timer.Start();
    }
    public static void UpdateOrDisplay(MainWindow owner,MainWindow.Settings settings,string client,string title,IntPtr source)
    {
        if(toast is {IsVisible:true}&&toastClient==client&&toastSender!=null&&toastBody!=null){
            var lines=title.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            toastSender.Text=lines.Length>1?lines[0]:client;toastBody.Text=lines.Length>1?string.Join("\n",lines.Skip(1)):title;
            ((FrameworkElement)toast.Content).BeginAnimation(UIElement.OpacityProperty,null);((FrameworkElement)toast.Content).Opacity=1;toast.UpdateLayout();ArmDismiss(settings,toast);return;
        }
        Display(owner,settings,client,title,source);
    }
    public static void UpdateOpacity(double opacity)
    {
        if(toast?.Content is Border border&&toastClient!=null)border.Background=MessageGlass.PopupSurface(toastClient,opacity);
    }    public static void Display(MainWindow owner,MainWindow.Settings settings,string client,string title,IntPtr source)
    {
        if(owner.IsDockClosed)return;toast?.Close();if(owner.Coordinator!=null&&DesktopIntegration.Cursor(out var cursor))owner=owner.Coordinator.Docks.FirstOrDefault(d=>d.PhysicalWorkArea.Contains(cursor))??owner;
        var anchor=owner.MessageAnchor(client);if(anchor==null)return;var finish=owner.BeginMessage(anchor);
        var popup=new Window{Title="windock · 消息提醒",Width=320,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Owner=owner,AllowsTransparency=true,Background=Brushes.Transparent};
        var lines=(title??"").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);string sender=lines.Length>1?lines[0]:client;string message=lines.Length>1?string.Join("\n",lines.Skip(1)):string.IsNullOrWhiteSpace(title)?"有新的消息":title;
        var body=new StackPanel();var header=new DockPanel();var time=new TextBlock{Text=DateTime.Now.ToString("HH:mm"),FontSize=10,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(time,Dock.Right);header.Children.Add(time);header.Children.Add(new Image{Source=NotificationLogo.Create(client),Width=22,Height=22,Margin=new Thickness(0,0,8,0)});toastSender=new TextBlock{Text=sender,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};header.Children.Add(toastSender);body.Children.Add(header);
        // 来源与内容一目了然：昵称/群聊名在标题行，"来自 客户端"补充来源，正文展示消息内容。
        body.Children.Add(new TextBlock{Text="来自 "+client,FontSize=10.5,Foreground=Theme.Muted,Margin=new Thickness(0,3,0,0)});
        toastBody=new TextBlock{Text=message,FontSize=13,Foreground=Theme.Text,Opacity=.88,TextWrapping=TextWrapping.Wrap,MaxHeight=96,Margin=new Thickness(0,8,0,0)};body.Children.Add(toastBody);
        GetWindowThreadProcessId(source,out uint originalPid);
        if(source!=IntPtr.Zero)body.Children.Add(new TextBlock{Text="点击气泡查看会话",FontSize=10.5,Foreground=Theme.Muted,Opacity=.8,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,9,0,0)});
        popup.MouseLeftButtonUp+=(_,_)=>{GetWindowThreadProcessId(source,out uint pid);if(source!=IntPtr.Zero&&pid==originalPid){ShowWindow(source,9);SetForegroundWindow(source);}popup.Close();};
        popup.Content=new Border{CornerRadius=new CornerRadius(16),Background=MessageGlass.PopupSurface(client,owner.MessageReminderOpacity),BorderBrush=MessageGlass.Rim,BorderThickness=new Thickness(.7),Padding=new Thickness(16,13,16,14),Child=body,Cursor=source!=IntPtr.Zero?System.Windows.Input.Cursors.Hand:System.Windows.Input.Cursors.Arrow};
        void Position(){if(!anchor.IsVisible){popup.Close();return;}var a=anchor.PointToScreen(new Point());var b=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight));var dpi=VisualTreeHelper.GetDpi(popup);double w=popup.ActualWidth*dpi.DpiScaleX,h=popup.ActualHeight*dpi.DpiScaleY;double x=(a.X+b.X-w)/2,y=a.Y-h-12;
            if(owner.DockEdge=="Top")y=b.Y+12;else if(owner.DockEdge=="Left"){x=b.X+12;y=(a.Y+b.Y-h)/2;}else if(owner.DockEdge=="Right"){x=a.X-w-12;y=(a.Y+b.Y-h)/2;}
            var area=owner.PhysicalWorkArea;DesktopIntegration.MovePhysical(popup,Math.Clamp(x,area.Left+6,Math.Max(area.Left+6,area.Right-w-6)),Math.Clamp(y,area.Top+6,Math.Max(area.Top+6,area.Bottom-h-6)));
        }
        EventHandler moved=(_,_)=>Position();SizeChangedEventHandler resized=(_,_)=>Position();owner.LocationChanged+=moved;owner.SizeChanged+=resized;
        popup.Loaded+=(_,_)=>{Position();((FrameworkElement)popup.Content).BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(90)));};popup.DpiChanged+=(_,_)=>popup.Dispatcher.BeginInvoke(new Action(Position));
        toast=popup;toastClient=client;popup.Closed+=(_,_)=>{dismiss?.Stop();dismiss=null;owner.LocationChanged-=moved;owner.SizeChanged-=resized;finish();if(ReferenceEquals(toast,popup))toast=null;};
        ArmDismiss(settings,popup);popup.Show();
    }
}
