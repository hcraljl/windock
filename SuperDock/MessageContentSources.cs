using System.Windows.Threading;
using System.Windows.Automation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
namespace SuperDock;

internal static class MessageContentSources
{
    public static string Status {get;private set;}="消息详情需要 Windows 通知读取权限";
    public static string WechatStatus {get;private set;}="可读取系统通知或微信已开放的未读会话预览";
    static readonly Dictionary<string,long> recentToasts=[];
    public static bool Details(MainWindow.Settings settings,string client)=>settings.MessageDetailOverrides.TryGetValue(client,out var enabled)?enabled:settings.MessagePreviewEnabled;
    public static bool RecentToast(string client)=>recentToasts.TryGetValue(client,out long time)&&Environment.TickCount64-time<4000;
    public static string? NotificationClient(string identity,string name)=>name.Equals("企业微信",StringComparison.OrdinalIgnoreCase)||name.Equals("WeCom",StringComparison.OrdinalIgnoreCase)||identity.Equals("WXWork",StringComparison.OrdinalIgnoreCase)||identity.Equals("Tencent.WeWork",StringComparison.OrdinalIgnoreCase)||identity.StartsWith("Tencent.WeWork.",StringComparison.OrdinalIgnoreCase)?"企业微信":IsQq(identity,name)?"QQ":name.Equals("微信",StringComparison.OrdinalIgnoreCase)||name.Equals("WeChat",StringComparison.OrdinalIgnoreCase)||name.Equals("Weixin",StringComparison.OrdinalIgnoreCase)||identity.Equals("com.tencent.wechat",StringComparison.OrdinalIgnoreCase)||identity.Equals("Weixin",StringComparison.OrdinalIgnoreCase)?"微信":null;
    static readonly HashSet<string> seen=[];
    static bool initialized,busy;
    static long lastWecomToast;
    static readonly SemaphoreSlim wechatWorker=new(1);
    public static bool IsQq(string identity,string name)=>name.Equals("QQ",StringComparison.OrdinalIgnoreCase)||identity.Equals("QQ",StringComparison.OrdinalIgnoreCase)||identity.Equals("Tencent.QQ",StringComparison.OrdinalIgnoreCase)||identity.StartsWith("Tencent.QQ.",StringComparison.OrdinalIgnoreCase)||identity.StartsWith("Tencent.QQ_",StringComparison.OrdinalIgnoreCase);
    public static async Task<string> RequestPermission()
    {
        try{var result=await UserNotificationListener.Current.RequestAccessAsync();Status=result==UserNotificationListenerAccessStatus.Allowed?"系统通知已授权 · 支持 QQ、微信与企微通知详情":"未授予通知权限，保留图标提醒";}
        catch(Exception ex){Status="通知读取不可用："+ex.Message;}
        return Status;
    }
    record Notice(string Client,string Body,IntPtr Source);
    public static void Start(MainWindow owner,MainWindow.Settings settings)
    {
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(250)};
        var dirty=new HashSet<uint>();var fingerprints=new Dictionary<string,string>();var knownClients=new Dictionary<string,string>();long refreshTurn=0;
        bool stopped=false,requested=false,events=false;long retryEventAt=0;UserNotificationListener? subscribed=null;
        void Changed(UserNotificationListener sender,UserNotificationChangedEventArgs args){if(!stopped){uint id=args.UserNotificationId;owner.Dispatcher.BeginInvoke(new Action(()=>{if(stopped)return;dirty.Add(id);_ = Poll();}),DispatcherPriority.Normal);}}
        void Subscribe(){if(events||Environment.TickCount64<retryEventAt)return;retryEventAt=Environment.TickCount64+60000;try{var listener=UserNotificationListener.Current;if(listener.GetAccessStatus()!=UserNotificationListenerAccessStatus.Allowed)return;listener.NotificationChanged+=Changed;subscribed=listener;events=true;}catch{/* Unpackaged clients may reject events; keep bounded polling. */}}
        async Task Poll(){
            if(stopped)return;if(busy){requested=true;return;}
            var clients=settings.MessageRemindersEnabled?settings.MessageReminderClients.ToArray():[];
            var details=clients.ToDictionary(c=>c,c=>Details(settings,c));
            if(clients.Length==0){initialized=false;seen.Clear();WecomContentReader.Reset();return;}
            Subscribe();var changedIds=dirty.ToArray();dirty.Clear();busy=true;
            try{
                var result=await Task.Run(async()=>{
                    var output=new List<Notice>();
                    try{
                        var listener=UserNotificationListener.Current;
                        if(listener.GetAccessStatus()==UserNotificationListenerAccessStatus.Allowed){
                            bool partial=initialized&&changedIds.Length>0;
                            IReadOnlyList<UserNotification> notices;
                            if(partial){var changed=new List<UserNotification>();foreach(uint id in changedIds)try{var notice=listener.GetNotification(id);if(notice!=null)changed.Add(notice);}catch{}notices=changed;}
                            else notices=await listener.GetNotificationsAsync(NotificationKinds.Toast);
                            var active=partial?new HashSet<string>(seen):new HashSet<string>();
                            var refreshKeys=knownClients.Keys.Where(k=>clients.Contains(knownClients[k])).ToArray();string? refreshKey=refreshKeys.Length>0?refreshKeys[(int)(refreshTurn++%refreshKeys.Length)]:null;
                            foreach(var notice in notices){
                                string key=notice.Id+":"+notice.CreationTime.UtcTicks;active.Add(key);
                                // Cache ALL old notifications, including unrelated apps; avoid repeated cross-process text reads.
                                if(initialized&&!partial&&seen.Contains(key)&&key!=refreshKey)continue;
                                try{
                                    var client=knownClients.TryGetValue(key,out var known)?known:NotificationClient(notice.AppInfo.AppUserModelId??"",notice.AppInfo.DisplayInfo.DisplayName??"");
                                    if(client==null||!clients.Contains(client))continue;knownClients[key]=client;
                                    var text=notice.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
                                    string content=text==null?"":string.Join("\n",text.Select(t=>t.Text));
                                    if(fingerprints.TryGetValue(key,out var previous)&&previous==content)continue;
                                    fingerprints[key]=content;if(!initialized)continue;
                                    if(client=="企业微信")Interlocked.Exchange(ref lastWecomToast,Environment.TickCount64);
                                    output.Add(new(client,details[client]&&!string.IsNullOrWhiteSpace(content)?content:client+" 有新的消息",FindClientWindow(client)));
                                }catch{/* An expired/malformed notification must not discard the remaining messages. */}
                            }
                            if(!partial)foreach(var key in knownClients.Keys.Where(k=>!active.Contains(k)).ToArray()){fingerprints.Remove(key);knownClients.Remove(key);}
                            seen.Clear();seen.UnionWith(active);initialized=true;Status="系统通知监听中";
                        }
                    }catch{Status="系统通知暂不可用，保留图标提醒";}
                    return output;
                });
                if(owner.IsDockClosed)return;
                foreach(var notice in result.GroupBy(n=>n.Client).Select(g=>g.First())){
                    if(!settings.MessageRemindersEnabled||!settings.MessageReminderClients.Contains(notice.Client))continue;
                    recentToasts[notice.Client]=Environment.TickCount64;
                    MessageReminders.UpdateOrDisplay(owner,settings,notice.Client,Details(settings,notice.Client)?notice.Body:notice.Client+" 有新的消息",notice.Source);
                }
            }finally{busy=false;if(requested&&!stopped){requested=false;_ = owner.Dispatcher.BeginInvoke(new Action(()=>_ = Poll()),DispatcherPriority.Normal);}}
        }
        timer.Tick+=(_,_)=>_ = Poll();
        bool previewBusy=false;var previewTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(3)};
        previewTimer.Tick+=async(_,_)=>{
            if(previewBusy||stopped)return;
            if(!settings.MessageRemindersEnabled||!settings.MessageReminderClients.Contains("企业微信")||!Details(settings,"企业微信")){WecomContentReader.Reset();return;}
            previewBusy=true;try{
                var rows=await Task.Run(WecomContentReader.ReadChanges);
                if(!stopped&&settings.MessageRemindersEnabled&&settings.MessageReminderClients.Contains("企业微信")&&Details(settings,"企业微信")&&Environment.TickCount64-Interlocked.Read(ref lastWecomToast)>8000)
                    foreach(var row in rows.TakeLast(1)){recentToasts["企业微信"]=Environment.TickCount64;MessageReminders.UpdateOrDisplay(owner,settings,"企业微信",row.Text,row.Window);}
            }finally{previewBusy=false;}
        };
        owner.Closed+=(_,_)=>{stopped=true;timer.Stop();previewTimer.Stop();if(subscribed!=null)try{subscribed.NotificationChanged-=Changed;}catch{}};
        timer.Start();previewTimer.Start();_ = Poll();
    }
    internal static IntPtr FindClientWindow(string client)
    {
        string[] names=client switch{"企业微信"=>["WXWork"],"微信"=>["Weixin","WeChat"],"QQ"=>["QQ"],_=>[]};
        foreach(var name in names)foreach(var process in System.Diagnostics.Process.GetProcessesByName(name))using(process){try{if(process.MainWindowHandle!=IntPtr.Zero)return process.MainWindowHandle;}catch{}}
        return IntPtr.Zero;
    }
    public static bool QqListening {get{try{return UserNotificationListener.Current.GetAccessStatus()==UserNotificationListenerAccessStatus.Allowed;}catch{return false;}}}
    public static string? ParseWechatRow(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||!System.Text.RegularExpressions.Regex.IsMatch(name,@"\d+\s*条(?:未读|新消息)|\[\d+条\]|未读消息"))return null;
        var lines=name.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(lines.Any(l=>l is "免打扰" or "消息免打扰" or "折叠的群聊"))return null;
        var cleaned=lines.Where(l=>!System.Text.RegularExpressions.Regex.IsMatch(l,@"^(?:\d+\s*条(?:未读消息|未读|新消息)|\d{1,2}:\d{2}|置顶|已置顶)$")).Select(l=>System.Text.RegularExpressions.Regex.Replace(l,@"\[\d+条\]\s*","")).ToArray();
        return cleaned.Length>=2?string.Join("\n",cleaned.Take(3))[..Math.Min(240,string.Join("\n",cleaned.Take(3)).Length)]:null;
    }
    public static async Task<string?> WechatPreview(IntPtr window)
    {
        if(window==IntPtr.Zero||!await wechatWorker.WaitAsync(0))return null;
        Task<string?>? read=null;
        try{read=Task.Run(()=>{
            var root=AutomationElement.FromHandle(window);var queue=new Queue<AutomationElement>();queue.Enqueue(root);var watch=System.Diagnostics.Stopwatch.StartNew();int count=0;
            while(queue.Count>0&&count++<350&&watch.ElapsedMilliseconds<450){
                var element=queue.Dequeue();var value=element.Current;
                if((value.ControlType==ControlType.ListItem||value.AutomationId.StartsWith("session_item_",StringComparison.OrdinalIgnoreCase))&&ParseWechatRow(value.Name) is { } preview){WechatStatus="会话预览可读 · 已识别发送人与消息摘要";return preview;}
                for(var child=TreeWalker.ControlViewWalker.GetFirstChild(element);child!=null&&queue.Count<350;child=TreeWalker.ControlViewWalker.GetNextSibling(child))queue.Enqueue(child);
            }WechatStatus="未找到可读的未读会话；可尝试开启微信系统通知";return (string?)null;
        });
            _=read.ContinueWith(t=>{_ =t.Exception;wechatWorker.Release();},TaskScheduler.Default);
            return await read.WaitAsync(TimeSpan.FromMilliseconds(650));
        }catch{if(read==null)wechatWorker.Release();WechatStatus="微信接口暂不可读，保留图标提醒";return null;}
    }
}
