using System.Windows.Automation;
namespace SuperDock;

// Read only accessibility data exposed by the client. Never open conversations or modify its process.
internal static class WecomContentReader
{
    internal record Preview(string Text,IntPtr Window);
    static readonly SemaphoreSlim worker=new(1);
    static HashSet<string>? previous;
    static long nextRead;
    public static string Status {get;private set;}="系统通知优先；会话详情取决于企微可访问性";
    public static void Reset(){previous=null;}
    internal static string? ParseRow(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Contains("免打扰")||!System.Text.RegularExpressions.Regex.IsMatch(name,@"未读|\[\d+条\]|\d+\s*条新消息"))return null;
        var lines=name.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var content=lines.Where(l=>!System.Text.RegularExpressions.Regex.IsMatch(l,@"^(?:\d+\s*条(?:未读消息|未读|新消息)|未读\s*\d*|\d{1,2}:\d{2})$")).ToArray();
        return content.Length>=2?string.Join("\n",content.Take(3))[..Math.Min(240,string.Join("\n",content.Take(3)).Length)]:null;
    }
    public static async Task<List<Preview>> ReadChanges()
    {
        if(Environment.TickCount64<nextRead||!await worker.WaitAsync(0))return [];
        nextRead=Environment.TickCount64+3000;
        var read=Task.Run(()=>{
            try{
                var handle=MessageContentSources.FindClientWindow("企业微信");
                if(handle==IntPtr.Zero){previous=null;nextRead=Environment.TickCount64+15000;return new List<Preview>();}
                var queue=new Queue<AutomationElement>();queue.Enqueue(AutomationElement.FromHandle(handle));
                var rows=new Dictionary<string,string>();var watch=System.Diagnostics.Stopwatch.StartNew();int count=0;
                while(queue.Count>0&&count++<220&&watch.ElapsedMilliseconds<250){
                    var element=queue.Dequeue();var value=element.Current;
                    if(value.ControlType==ControlType.ListItem&&ParseRow(value.Name) is {} text)rows.TryAdd(value.Name,text);
                    for(var child=TreeWalker.ControlViewWalker.GetFirstChild(element);child!=null&&queue.Count<220&&watch.ElapsedMilliseconds<250;child=TreeWalker.ControlViewWalker.GetNextSibling(child))queue.Enqueue(child);
                }
                // Incomplete trees must not reset the baseline and replay old rows later.
                if(queue.Count>0){nextRead=Environment.TickCount64+30000;Status="会话读取受限，使用系统通知与图标提醒";return new List<Preview>();}
                var changes=previous==null?[]:rows.Where(r=>!previous.Contains(r.Key)).Select(r=>new Preview(r.Value,handle)).ToList();
                previous=rows.Keys.ToHashSet();if(rows.Count==0)nextRead=Environment.TickCount64+10000;Status=rows.Count>0?"未读会话可读 · 自动检测摘要变化":"未发现可读未读会话，使用系统通知";
                return changes;
            }catch{nextRead=Environment.TickCount64+30000;Status="企微未开放会话详情，使用系统通知";return new List<Preview>();}
        });
        // A hung accessibility provider keeps this single slot; never accumulate blocked workers.
        _=read.ContinueWith(t=>{_ =t.Exception;worker.Release();},TaskScheduler.Default);
        try{return await read.WaitAsync(TimeSpan.FromMilliseconds(500));}catch{nextRead=Environment.TickCount64+60000;Status="企微会话读取超时，使用系统通知";return [];}
    }
}
