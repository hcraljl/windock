using System.IO;
namespace SuperDock;
internal static class ApplicationCatalog
{
    static readonly object gate=new();
    static Task<MainWindow.Entry[]>? snapshot;
    static long expires;
    public static Task<MainWindow.Entry[]> GetAsync()
    {
        lock(gate){
            if(snapshot!=null&&(!snapshot.IsCompleted||Environment.TickCount64<expires)&&!snapshot.IsFaulted)return snapshot;
            expires=Environment.TickCount64+120000;
            return snapshot=Task.Run(()=>{
                var installed=Read();
                var running=RunningWindows.ListApps().Select(a=>new MainWindow.Entry(Path.GetFileNameWithoutExtension(a.Path),a.Path));
                // Resolve identities in the worker, rather than re-opening every shortcut per card.
                var result=installed.Concat(running).DistinctBy(e=>RunningWindows.WindowTarget(e.Target),StringComparer.OrdinalIgnoreCase).OrderBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
                return result;
            });
        }
    }
    internal static bool IsApplication(string shortcut)
    {
        var name=Path.GetFileNameWithoutExtension(shortcut);
        string[] maintenance={"uninstall","unins000","unins001","卸载","解除安装","移除程序","安装程序","setup","installer","repair","修复","维护工具","readme","read me","自述","release notes","帮助文档"};
        if(maintenance.Any(word=>name.Contains(word,StringComparison.OrdinalIgnoreCase)))return false;
        string[] systemFolders={"Administrative Tools","Windows Tools","System Tools","Windows System","Windows Accessories","Windows PowerShell","Accessibility","系统工具","管理工具","Windows 工具","Windows 系统","Windows 附件","辅助功能"};
        if(shortcut.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(part=>systemFolders.Contains(part,StringComparer.OrdinalIgnoreCase)))return false;
        var info=ShortcutTarget.Inspect(shortcut);var target=info.Target;var executable=Path.GetFileName(target);
        if(maintenance.Any(word=>executable.Contains(word,StringComparison.OrdinalIgnoreCase)))return false;
        string[] removalArgs={"/uninstall","--uninstall","-uninstall","/remove","--remove","/x ","/x{"};
        if(removalArgs.Any(arg=>info.Arguments.Contains(arg,StringComparison.OrdinalIgnoreCase)))return false;
        string[] systemHosts={"msiexec.exe","rundll32.exe","control.exe","mmc.exe","cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe","regedit.exe","msconfig.exe","taskschd.exe","SystemPropertiesAdvanced.exe","SystemPropertiesComputerName.exe","SystemPropertiesProtection.exe","SystemPropertiesRemote.exe"};
        if(systemHosts.Contains(executable,StringComparer.OrdinalIgnoreCase))return false;
        if(target.StartsWith("shell:",StringComparison.OrdinalIgnoreCase)||target.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase))return false;
        if(Path.GetExtension(shortcut).Equals(".appref-ms",StringComparison.OrdinalIgnoreCase))return true;
        return Path.GetExtension(target).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(target);
    }
    public static List<MainWindow.Entry> Read()
    {
        var apps=new List<MainWindow.Entry>();
        foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)})
        {
            if(!Directory.Exists(root))continue;
            try
            {
                foreach(var file in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint}))
                    if(Path.GetExtension(file).Equals(".lnk",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(file).Equals(".appref-ms",StringComparison.OrdinalIgnoreCase))
                        if(IsApplication(file))apps.Add(new(Path.GetFileNameWithoutExtension(file),file));
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
        return apps.DistinctBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).OrderBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}


