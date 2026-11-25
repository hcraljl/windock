using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperDock.Search;

public enum EverythingReadyKind
{
    Existing,
    NamedInstance,
    LaunchedInstalled,
    LaunchedBundled,
    Failed
}

/// <summary>当前索引服务的可用状态；NamedInstance 表示要改走 es.exe -instance 通道。</summary>
public sealed record EverythingAvailability(
    EverythingReadyKind Kind,
    string? NamedInstance,
    string Message);

/// <summary>
/// 发现本机 Everything 实例，并在索引不可用时自动拉起一个可用的进程。
/// 优先级：已运行的无名实例（SDK 直连）→ 已运行的命名实例（es.exe 查询）→
/// 启动用户已装的 Everything → 启动随 Superun 内置的便携版。
/// </summary>
public static class EverythingRuntime
{
    private const string IpcWindowClass = "EVERYTHING_TASKBAR_NOTIFICATION";
    private const int LaunchWaitMs = 9000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>SDK 能连上无名实例时直接返回，无需任何额外动作。</summary>
    public static bool HasDefaultInstance() => FindWindowW(IpcWindowClass, null) != IntPtr.Zero;

    /// <summary>找出命名实例名，例如窗口类 EVERYTHING_TASKBAR_NOTIFICATION_(1.5a) → "1.5a"。</summary>
    public static string? FindNamedInstance()
    {
        string? found = null;
        EnumWindows((hWnd, _) =>
        {
            var buffer = new StringBuilder(256);
            if (GetClassNameW(hWnd, buffer, buffer.Capacity) <= 0)
            {
                return true;
            }

            var className = buffer.ToString();
            // 命名实例的窗口类名是 EVERYTHING_TASKBAR_NOTIFICATION_(名称)，括号前有一个下划线
            var prefix = IpcWindowClass + "_(";
            if (className.StartsWith(prefix, StringComparison.Ordinal) && className.EndsWith(')'))
            {
                found = className[prefix.Length..^1];
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return string.IsNullOrWhiteSpace(found) ? null : found;
    }

    /// <summary>搜索前调用：快速判断可用通道，必要时后台拉起索引进程并等待就绪。</summary>
    public static async Task<EverythingAvailability> EnsureAsync(bool forceLaunch = false)
    {
        if (!forceLaunch)
        {
            if (HasDefaultInstance())
            {
                return new(EverythingReadyKind.Existing, null, "Everything 索引已就绪");
            }

            var named = FindNamedInstance();
            if (!string.IsNullOrEmpty(named))
            {
                // 1.5 的命名实例不接受 SDK v2 的 QUERY2 协议，改由 es.exe -instance 查询
                return new(EverythingReadyKind.NamedInstance, named,
                    $"已连接 Everything 实例（{named}）");
            }
        }

        string? installedError = null;
        string? bundledError = null;

        var installed = ResolveInstalledExecutable();
        if (installed is not null && TryLaunch(installed, bundled: false, out installedError))
        {
            if (await WaitForSdkAsync())
            {
                return new(EverythingReadyKind.LaunchedInstalled, null, "已启动本机 Everything");
            }
        }

        var bundled = GetBundledExecutable();
        if (bundled is not null && TryLaunch(bundled, bundled: true, out bundledError))
        {
            if (await WaitForSdkAsync())
            {
                return new(EverythingReadyKind.LaunchedBundled, null, "已启动内置 Everything 并建立索引");
            }
        }

        var detail = installedError ?? bundledError ?? "没有找到可用的 Everything.exe";
        return new(EverythingReadyKind.Failed, null, $"无法启动文件索引服务：{detail}");
    }

    /// <summary>尽量定位用户已安装的 Everything.exe；找不到就用内置便携版。</summary>
    public static string? ResolveInstalledExecutable()
    {
        var candidate = EverythingSearchService.ResolveEverythingExecutable();
        if (candidate is not null)
        {
            return candidate;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("Everything"))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
                catch
                {
                    // 服务进程可能不允许读取模块路径，跳过
                }
            }
        }
        catch
        {
            // 进程枚举失败时继续走内置兜底
        }

        return null;
    }

    public static string? GetBundledExecutable() => ProbeBundled("Everything.exe");

    public static string? GetBundledEsExecutable() => ProbeBundled("es.exe");

    private static string? ProbeBundled(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Everything", fileName);
        return File.Exists(path) ? path : null;
    }

    private static bool TryLaunch(string executable, bool bundled, out string? error)
    {
        error = null;
        try
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            start.ArgumentList.Add("-startup");
            start.ArgumentList.Add("-first-instance");

            if (bundled)
            {
                // 数据和索引放到用户目录，升级/卸载 Superun 都不会破坏它
                var dataDir = GetBundledDataDirectory();
                Directory.CreateDirectory(dataDir);
                var ini = Path.Combine(dataDir, "Everything.ini");
                if (!File.Exists(ini))
                {
                    var folders=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)}.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    File.WriteAllText(ini,"[Everything]\r\napp_data=0\r\nrun_as_admin=0\r\nallow_everything_service=1\r\nallow_multiple_instances=1\r\nfolders="+string.Join(",",folders.Select(f=>"\""+f.Replace("\\","\\\\")+"\""))+"\r\nfolder_monitor_changes="+string.Join(",",folders.Select(_=>"1"))+"\r\n",new UTF8Encoding(true));
                }

                var existingIni=File.ReadAllText(ini);
                if(System.Text.RegularExpressions.Regex.IsMatch(existingIni,@"(?m)^folders=.*""[A-Za-z]:[^\\""\r\n]")){
                    var roots=DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed).Select(d=>"\""+d.RootDirectory.FullName.Replace("\\","\\\\")+"\"").ToArray();
                    File.Copy(ini,ini+".before-path-migration",true);
                    existingIni=System.Text.RegularExpressions.Regex.Replace(existingIni,@"(?m)^folders=.*$","folders="+string.Join(",",roots));
                    existingIni=System.Text.RegularExpressions.Regex.Replace(existingIni,@"(?m)^folder_monitor_changes=.*$","folder_monitor_changes="+string.Join(",",roots.Select(_=>"1")));
                    File.WriteAllText(ini,existingIni,new UTF8Encoding(true));
                }
                start.ArgumentList.Add("-config");
                start.ArgumentList.Add(ini);
                start.ArgumentList.Add("-db");
                start.ArgumentList.Add(Path.Combine(dataDir, "Everything.db"));
            }

            return Process.Start(start) is not null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string GetBundledDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SuperDock", "Everything");

    private static async Task<bool> WaitForSdkAsync()
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < LaunchWaitMs)
        {
            if (HasDefaultInstance())
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }
}
