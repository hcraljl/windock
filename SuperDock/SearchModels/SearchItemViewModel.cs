using System.IO;
using SuperDock.Search;

namespace SuperDock.SearchModels;

public sealed class SearchItemViewModel
{
    public SearchItemViewModel(EverythingHit hit)
    {
        Size=hit.Size;FullPath = hit.FullPath;
        Name = hit.Name;
        IsFolder = hit.IsFolder;
        Category = hit.Category;
        Directory = Path.GetDirectoryName(hit.FullPath) ?? string.Empty;

        var extension = Path.GetExtension(hit.Name);
        HasExecutableIcon = !hit.IsFolder
                            && (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase));

        GlyphData = SearchGlyphs.For(hit.Category);
        SizeText = hit.IsFolder ? "文件夹" : FormatSize(hit.Size);
    }

    public long Size {get;}
    public string FullPath { get; }

    public string Name { get; }

    public bool IsFolder { get; }

    public SearchCategory Category { get; }

    public string Directory { get; }

    public bool HasExecutableIcon { get; }

    public bool ShowGlyph => !HasExecutableIcon;

    public string GlyphData { get; }

    public string SizeText { get; }

    public string ToolTip => FullPath;

    private static string FormatSize(long size)
    {
        if (size <= 0)
        {
            return string.Empty;
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }
}
