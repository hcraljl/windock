using System.IO;
using System.Runtime.InteropServices;
namespace SuperDock;
internal static class ShortcutTarget
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string,((string Target,string Arguments) Info,long Expires)> cache=new(StringComparer.OrdinalIgnoreCase);
    public static string Resolve(string path) => Inspect(path).Target;
    public static (string Target,string Arguments) Inspect(string path)
    {
        if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return (path, "");
        if(cache.TryGetValue(path,out var cached)&&Environment.TickCount64<cached.Expires)return cached.Info;
        var result=Read(path);cache[path]=(result,Environment.TickCount64+120000);return result;
    }
    static (string Target,string Arguments) Read(string path)
    {
        object? shell = null, link = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return (path, "");
            shell = Activator.CreateInstance(type);
            link = ((dynamic)shell!).CreateShortcut(path);
            string target = ((dynamic)link).TargetPath;
            return (string.IsNullOrWhiteSpace(target) ? path : Environment.ExpandEnvironmentVariables(target), (string)((dynamic)link).Arguments);
        }
        catch { return (path, ""); }
        finally
        {
            if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}


