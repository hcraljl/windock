using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperDock.Search;

public enum SearchCategory
{
    All,
    Program,
    Document,
    Spreadsheet,
    Presentation,
    Image,
    Media,
    Archive,
    Folder,
    Other
}

public sealed record EverythingHit(
    string FullPath,
    string Name,
    bool IsFolder,
    SearchCategory Category,
    long Size);

public sealed record EverythingSearchResult(
    bool Success,
    bool ServiceUnavailable,
    string Message,
    IReadOnlyList<EverythingHit> Hits);

/// <summary>
/// Everything SDK（Everything64.dll）封装：直接向 Everything 客户端发起 IPC 查询，毫秒级返回结果。
/// 需要 Everything 正在运行；未安装/未启动时返回 ServiceUnavailable。
/// </summary>
public sealed class EverythingSearchService
{
    private const uint RequestFileName = 0x00000001;
    private const uint RequestPath = 0x00000002;
    private const uint RequestSize = 0x00000010;
    private const uint SortNameAscending = 1;

    private static readonly object SyncRoot = new();

    private static readonly HashSet<string> ProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".msi", ".bat", ".cmd", ".com", ".url", ".appref-ms"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".rtf", ".txt", ".md", ".pdf", ".wps", ".odt", ".pages", ".tex", ".epub"
    };

    private static readonly HashSet<string> SpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv", ".tsv", ".et", ".ods", ".numbers"
    };

    private static readonly HashSet<string> PresentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ppt", ".pptx", ".pptm", ".pps", ".ppsx", ".dps", ".odp", ".key"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff",
        ".heic", ".heif", ".psd", ".ai", ".raw", ".cr2", ".nef", ".dng"
    };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".rmvb", ".rm", ".webm", ".m4v", ".mpg", ".mpeg", ".ts",
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".ape", ".aiff"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".cab", ".iso", ".jar", ".war", ".apk"
    };

    /// <summary>Everything 未启动时返回的 SDK 错误码。</summary>
    private const uint ErrorIpc = 2;

    public IReadOnlyList<EverythingHit> Search(string query, int maxResults, out string message, out bool serviceUnavailable)
    {
        message = string.Empty;
        serviceUnavailable = false;
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        lock (SyncRoot)
        {
            try
            {
                Everything_SetSearchW(trimmed);
                Everything_SetRequestFlags(RequestFileName | RequestPath | RequestSize);
                Everything_SetMax((uint)Math.Clamp(maxResults, 1, 4096));
                Everything_SetSort(SortNameAscending);

                if (!Everything_QueryW(true))
                {
                    var error = Everything_GetLastError();
                    Everything_CleanUp();

                    if (error == ErrorIpc)
                    {
                        // Everything 1.5 常跑在命名实例里，SDK v2 连不上；改由 es.exe -instance 查询
                        var named = EverythingRuntime.FindNamedInstance();
                        if (!string.IsNullOrEmpty(named))
                        {
                            var cli = new EverythingCliSearchService();
                            if (cli.TrySearch(trimmed, maxResults, named, out var cliHits, out var cliMessage))
                            {
                                return cliHits;
                            }

                            message = cliMessage;
                            return [];
                        }

                        serviceUnavailable = true;
                        message = "Everything 未运行";
                        return [];
                    }

                    message = $"搜索失败（错误码 {error}）";
                    return [];
                }

                var total = Everything_GetNumResults();
                var count = Math.Min(total, (uint)Math.Clamp(maxResults, 1, 4096));
                var hits = new List<EverythingHit>((int)count);
                for (uint index = 0; index < count; index++)
                {
                    var buffer = new StringBuilder(1024);
                    if (Everything_GetResultFullPathNameW(index, buffer, (uint)buffer.Capacity) == 0)
                    {
                        continue;
                    }

                    var fullPath = buffer.ToString();
                    if (string.IsNullOrWhiteSpace(fullPath))
                    {
                        continue;
                    }

                    var isFolder = Everything_IsFolderResult(index);
                    var name = Path.GetFileName(fullPath);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = fullPath;
                    }

                    long size = 0;
                    if (!isFolder)
                    {
                        Everything_GetResultSize(index, out size);
                    }

                    hits.Add(new EverythingHit(fullPath, name, isFolder, Classify(name, isFolder), size));
                }

                Everything_CleanUp();
                return hits;
            }
            catch (DllNotFoundException)
            {
                serviceUnavailable = true;
                message = "未找到 Everything SDK（Everything64.dll）";
                return [];
            }
            catch (BadImageFormatException)
            {
                serviceUnavailable = true;
                message = "Everything SDK 位数与当前进程不匹配";
                return [];
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return [];
            }
        }
    }

    /// <summary>尽量找到 Everything 主程序，供“启动 Everything”按钮使用。</summary>
    public static string? ResolveEverythingExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything.exe"),
            @"C:\Program Files\Everything\Everything.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>探测 SDK 能否连上无名实例；用于判断是否需要自动拉起索引进程。</summary>
    public static bool IsAvailable()
    {
        lock (SyncRoot)
        {
            try
            {
                Everything_SetSearchW("__superun_probe_61d2__");
                Everything_SetRequestFlags(RequestFileName);
                Everything_SetMax(1);
                Everything_SetSort(SortNameAscending);
                var available = Everything_QueryW(true);
                Everything_CleanUp();
                return available;
            }
            catch
            {
                return false;
            }
        }
    }

    public static SearchCategory Classify(string name, bool isFolder)
    {
        if (isFolder)
        {
            return SearchCategory.Folder;
        }

        var extension = Path.GetExtension(name);
        if (string.IsNullOrEmpty(extension))
        {
            return SearchCategory.Other;
        }

        if (ProgramExtensions.Contains(extension)) return SearchCategory.Program;
        if (DocumentExtensions.Contains(extension)) return SearchCategory.Document;
        if (SpreadsheetExtensions.Contains(extension)) return SearchCategory.Spreadsheet;
        if (PresentationExtensions.Contains(extension)) return SearchCategory.Presentation;
        if (ImageExtensions.Contains(extension)) return SearchCategory.Image;
        if (MediaExtensions.Contains(extension)) return SearchCategory.Media;
        if (ArchiveExtensions.Contains(extension)) return SearchCategory.Archive;
        return SearchCategory.Other;
    }

    [DllImport("Everything64.dll", EntryPoint = "Everything_SetSearchW", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetSearchW(string lpString);

    [DllImport("Everything64.dll", EntryPoint = "Everything_SetRequestFlags")]
    private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport("Everything64.dll", EntryPoint = "Everything_SetMax")]
    private static extern void Everything_SetMax(uint dwMax);

    [DllImport("Everything64.dll", EntryPoint = "Everything_SetSort")]
    private static extern void Everything_SetSort(uint dwSort);

    [DllImport("Everything64.dll", EntryPoint = "Everything_QueryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport("Everything64.dll", EntryPoint = "Everything_GetNumResults")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", EntryPoint = "Everything_GetLastError")]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll", EntryPoint = "Everything_GetResultFullPathNameW", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetResultFullPathNameW(uint dwIndex, StringBuilder wbuf, uint wbufSizeInWchars);

    [DllImport("Everything64.dll", EntryPoint = "Everything_IsFolderResult")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Everything_IsFolderResult(uint dwIndex);

    [DllImport("Everything64.dll", EntryPoint = "Everything_GetResultSize")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Everything_GetResultSize(uint dwIndex, out long lpSize);

    [DllImport("Everything64.dll", EntryPoint = "Everything_CleanUp")]
    private static extern void Everything_CleanUp();
}
