using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace SuperDock;
internal record AppWindow(IntPtr Handle, string Title);
internal record RunningApp(string Name, string Path);
internal enum AppState { Stopped, Foreground, Background, Minimized, BackgroundProcess }
internal static class RunningWindows
{
    static readonly object stateGate=new();
    static string stateKey="";static long stateExpires;static Task<Dictionary<string,AppState>>? stateTask;
    public static Task<Dictionary<string,AppState>> StatesAsync(IEnumerable<string> targets,IntPtr foreground)
    {
        var paths=targets.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();var key=foreground+"\n"+string.Join("\n",paths);
        lock(stateGate){if(stateTask!=null&&!stateTask.IsFaulted&&key==stateKey&&(!stateTask.IsCompleted||Environment.TickCount64<stateExpires))return stateTask;stateKey=key;stateExpires=Environment.TickCount64+180;return stateTask=Task.Run(()=>States(paths,foreground));}
    }
    internal static Dictionary<string, AppState> States(IEnumerable<string> targets, IntPtr foreground)
    {
        var paths = targets.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var running = RunningTargets(paths);
        var states = new Dictionary<string, AppState>(StringComparer.OrdinalIgnoreCase);
        var foregroundRoot = GetAncestor(foreground, 3);
        foreach (var target in paths)
        {
            var windows = Find(target);
            states[target] = windows.Any(w => !IsIconic(w.Handle) && GetAncestor(w.Handle, 3) == foregroundRoot && foregroundRoot != IntPtr.Zero)
                ? AppState.Foreground
                : windows.Any(w => !IsIconic(w.Handle)) ? AppState.Background
                : windows.Count > 0 ? AppState.Minimized
                : running.Contains(target) ? AppState.BackgroundProcess : AppState.Stopped;
        }
        return states;
    }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    internal static HashSet<string> RunningTargets(IEnumerable<string> targets)
    {
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == Environment.ProcessId || process.HasExited) continue;
                        if (!Path.IsPathFullyQualified(target) || string.Equals(process.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase)) running.Add(target);
                    }
                    catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
                }
            }
        }
        return running;
    }
    public static (int Ended,int Failed) ForceExit(string target)
    {
        target=WindowTarget(target);
        if(!Path.GetExtension(target).Equals(".exe",StringComparison.OrdinalIgnoreCase))return (0,0);
        int ended=0,failed=0;
        foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)))
        {
            using(process)
            {
                try
                {
                    if(process.Id==Environment.ProcessId||process.HasExited)continue;
                    if(Path.IsPathFullyQualified(target)&&!string.Equals(process.MainModule?.FileName,target,StringComparison.OrdinalIgnoreCase))continue;
                    process.Kill();ended++;
                }
                catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){failed++;}
            }
        }
        return (ended,failed);
    }
    public static bool RequestExit(string target)
    {
        bool requested = false;
        foreach (var window in Find(target))
            requested |= PostMessage(window.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE: let the app handle unsaved work.
        return requested;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    public static void PreventActivation(IntPtr hwnd) => SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000000));
    public static IntPtr ExternalForeground()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == Environment.ProcessId ? IntPtr.Zero : hwnd;
    }
    public static List<RunningApp> ListApps()
    {
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4) != IntPtr.Zero) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
            if (title.Length == 0) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId) return true;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path)) apps.TryAdd(path, new(title.ToString(), path));
            }
            catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
            return true;
        }, IntPtr.Zero);
        return apps.Values.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    internal static string WindowTarget(string target)
    {
        target = ShortcutTarget.Resolve(target);
        // DingTalk's desktop shortcut launches a bootstrapper, not the window-owning executable.
        if (Path.GetFileName(target).Equals("DingtalkLauncher.exe", StringComparison.OrdinalIgnoreCase))
        {
            var application = Path.Combine(Path.GetDirectoryName(target)!, "main", "current", "DingTalk.exe");
            if (File.Exists(application)) return application;
        }
        return target;
    }
    static bool IsAppWindow(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd)) return false;
        long style = GetWindowLongPtr(hwnd, -20).ToInt64();
        if ((style & 0x80) != 0 && (style & 0x40000) == 0) return false;
        // Some applications use a hidden owner for their main visible window.
        var owner = GetWindow(hwnd, 4);
        return owner == IntPtr.Zero || !IsWindowVisible(owner) || (style & 0x40000) != 0;
    }
    public static List<AppWindow> Find(string target)
    {
        target = WindowTarget(target);
        var name = Path.GetFileNameWithoutExtension(target);
        if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return [];
        var result = new List<AppWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsAppWindow(hwnd)) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId) return true;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (!process.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
                if (Path.IsPathFullyQualified(target))
                {
                    try { if (!string.Equals(process.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase)) return true; }
                    catch { return true; }
                }
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                if (text.Length > 0) result.Add(new(hwnd, text.ToString()));
            }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static void Activate(IntPtr hwnd) { if (IsIconic(hwnd)) ShowWindow(hwnd, 9); else ShowWindow(hwnd, 5); SetForegroundWindow(hwnd); }
    public static void Maximize(IntPtr hwnd) { ShowWindow(hwnd, 3); SetForegroundWindow(hwnd); }
    public static void Minimize(IntPtr hwnd) => ShowWindow(hwnd, 6);
    public static void Toggle(IReadOnlyList<AppWindow> windows, IntPtr foregroundBeforeClick)
    {
        // EnumWindows returns Z order: prefer the frontmost matching window.
        var foreground = GetAncestor(foregroundBeforeClick, 3); // Capture before WPF focus/preview closing changes activation.
        foreach (var window in windows)
        {
            if (!IsWindow(window.Handle) || IsIconic(window.Handle)) continue;
            if (GetAncestor(window.Handle, 3) == foreground)
            {
                ShowWindow(window.Handle, 6); // SW_MINIMIZE preserves maximized state on restore.
                return;
            }
        }
        var target = windows.FirstOrDefault(window => IsWindow(window.Handle));
        if (target != null) Activate(target.Handle);
    }
}


