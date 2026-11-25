using System.IO;
using System.Text.Json;
using System.Diagnostics;
namespace SuperDock;
internal record ActivityEvent(DateTimeOffset Time,string Level,string Kind,string Name,string Target,string Message,string Details="");
internal record UsageCount(string Name,string Target,int Count);
internal static class ActivityStore
{
    static readonly object gate=new();
    public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperDock","history");
    public static void Write(string kind,string name="",string target="",string message="",Exception? error=null)
    {
        var entry=new ActivityEvent(DateTimeOffset.Now,error==null&&!kind.Contains("失败")?"信息":"错误",kind,name,target,message,error?.ToString()??"");
        try{lock(gate){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,entry.Time.ToString("yyyy-MM")+".jsonl"),JsonSerializer.Serialize(entry)+Environment.NewLine);}}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Debug.WriteLine("Activity log: "+ex.Message);}
    }
    public static List<UsageCount> Statistics(IEnumerable<ActivityEvent> events,DateTime start,DateTime end)=>events.Where(e=>e.Kind=="应用启动"&&e.Time.LocalDateTime>=start&&e.Time.LocalDateTime<end).GroupBy(e=>e.Target,StringComparer.OrdinalIgnoreCase).Select(g=>new UsageCount(g.Last().Name,g.Key,g.Count())).OrderByDescending(e=>e.Count).ThenBy(e=>e.Name).ToList();
    public static List<ActivityEvent> Read()
    {
        var result=new List<ActivityEvent>();
        lock(gate)
        {
            if(!Directory.Exists(Folder))return result;
            foreach(var path in Directory.EnumerateFiles(Folder,"*.jsonl").Order())
                foreach(var line in File.ReadLines(path))try{var entry=JsonSerializer.Deserialize<ActivityEvent>(line);if(entry!=null)result.Add(entry);}catch(JsonException){}
        }
        return result;
    }
    public static bool Launch(string target,string? name=null)
    {
        name??=Path.GetFileNameWithoutExtension(target);
        try
        {
            Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
            string resolved=RunningWindows.WindowTarget(target);
            bool app=Path.GetExtension(resolved).Equals(".exe",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(target).Equals(".appref-ms",StringComparison.OrdinalIgnoreCase);
            Write(app?"应用启动":"打开入口",name,resolved,"启动请求已提交");return true;
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or ArgumentException)
        {Write("启动失败",name,target,"无法打开应用",ex);GlassDialog.Show("无法打开「"+name+"」："+ex.Message);return false;}
    }
}



