using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperDock.Search;

/// <summary>
/// 通过官方 es.exe 查询 Everything。SDK v2 只能连无名实例，
/// 而 Everything 1.5 常跑在命名实例里（例如 1.5a / ExplorerMax），
/// 这条通道用 -instance 参数直连，避免误报「Everything 未运行」。
/// </summary>
public sealed class EverythingCliSearchService
{
    private const int QueryTimeoutMs = 8000;

    // es.exe 用 GetConsoleCP() 把宽字符转成系统 ANSI 字节，中文 Windows 上是 GBK(936)
    private static readonly Encoding OutputEncoding = CreateOutputEncoding();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetACP();

    private static Encoding CreateOutputEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding((int)GetACP());
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    public bool TrySearch(
        string query,
        int maxResults,
        string? instanceName,
        out IReadOnlyList<EverythingHit> hits,
        out string message,string sort="相关度",CancellationToken cancellationToken=default)
    {
        hits = [];
        message = string.Empty;

        var executable = EverythingRuntime.GetBundledEsExecutable();
        if (string.IsNullOrEmpty(executable))
        {
            message = "缺少 es.exe（内置搜索组件）";
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = OutputEncoding,
                StandardErrorEncoding = OutputEncoding
            };

            if (!string.IsNullOrWhiteSpace(instanceName))
            {
                start.ArgumentList.Add("-instance");
                start.ArgumentList.Add(instanceName);
            }

            if(sort!="相关度"){start.ArgumentList.Add("-sort");start.ArgumentList.Add(sort=="大小升序"?"size-ascending":"size-descending");}
            start.ArgumentList.Add("-n");
            start.ArgumentList.Add(Math.Clamp(maxResults, 1, 4096).ToString());
            start.ArgumentList.Add(query);

            using var process = Process.Start(start);
            if (process is null)
            {
                message = "无法启动 es.exe";
                return false;
            }

            using var cancellation=cancellationToken.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}});
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(QueryTimeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程已退出
                }

                message = "索引响应超时；首次建立索引可能较慢，请稍后重新输入搜索。";
                return false;
            }

            if (process.ExitCode != 0)
            {
                var reason = stderr.GetAwaiter().GetResult().Trim();
                message = string.IsNullOrWhiteSpace(reason)
                    ? $"搜索失败（es.exe 退出码 {process.ExitCode}）"
                    : reason;
                return false;
            }

            var lines = stdout.GetAwaiter().GetResult()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var result = new List<EverythingHit>(lines.Length);
            foreach (var line in lines)
            {
                var fullPath = line.Trim();
                if (fullPath.Length == 0)
                {
                    continue;
                }

                var isFolder = Directory.Exists(fullPath);
                long size = 0;
                if (!isFolder)
                {
                    try
                    {
                        size = new FileInfo(fullPath).Length;
                    }
                    catch
                    {
                        // 文件可能在索引与读取之间被删除
                    }
                }

                var name = Path.GetFileName(fullPath);
                if (string.IsNullOrEmpty(name))
                {
                    name = fullPath;
                }

                result.Add(new EverythingHit(
                    fullPath, name, isFolder,
                    EverythingSearchService.Classify(name, isFolder), size));
            }

            hits = result;
            return true;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
    }
}
