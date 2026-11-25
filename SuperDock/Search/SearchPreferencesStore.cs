using System.IO;
using System.Text.Json;

namespace SuperDock.Search;

/// <summary>搜索窗口的独立偏好：预览开关与最近搜索词。</summary>
public sealed class SearchPreferences
{
    public bool PreviewEnabled { get; set; } = true;

    public List<string> History { get; set; } = [];
}

public sealed class SearchPreferencesStore
{
    private const int MaxHistory = 24;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string StorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SuperDock",
        "search-prefs.json");

    public SearchPreferences Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var preferences = JsonSerializer.Deserialize<SearchPreferences>(
                    File.ReadAllText(StorePath), JsonOptions);
                if (preferences is not null)
                {
                    preferences.History = Normalize(preferences.History);
                    return preferences;
                }
            }
        }
        catch
        {
            // 配置损坏不影响搜索窗口打开
        }

        return new SearchPreferences();
    }

    public void Save(SearchPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            preferences.History = Normalize(preferences.History);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(preferences, JsonOptions));
        }
        catch
        {
            // 磁盘不可写时静默降级为「本次会话有效」
        }
    }

    private static List<string> Normalize(List<string>? history)
    {
        return (history ?? [])
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHistory)
            .ToList();
    }
}
