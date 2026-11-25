using SuperDock.Search;

namespace SuperDock.SearchModels;

internal static class SearchGlyphs
{
    public const string All = "M4 6h16M4 12h16M4 18h16";
    public const string Program = "M4 5h16v14H4zM8 9l3 3-3 3M13 15h4";
    public const string Document = "M6 3h7l5 5v13H6zM13 3v5h5M9 14h6M9 17h6";
    public const string Spreadsheet = "M4 5h16v14H4zM4 10h16M4 15h16M10 5v14";
    public const string Presentation = "M3 4h18v11H3zM12 15v4M8 21h8";
    public const string Image = "M4 5h16v14H4zM7 15l3-3 2 2 3-4 4 5z";
    public const string Media = "M4 6h16v12H4zM10 9.5l5 2.5-5 2.5z";
    public const string Archive = "M5 4h14v16H5zM12 4v16M9 7h6M9 10h6M9 13h6";
    public const string Folder = "M3 7h6l2 2h10v9H3z";
    public const string Other = "M6 3h7l5 5v13H6z";

    public static string For(SearchCategory category) => category switch
    {
        SearchCategory.Program => Program,
        SearchCategory.Document => Document,
        SearchCategory.Spreadsheet => Spreadsheet,
        SearchCategory.Presentation => Presentation,
        SearchCategory.Image => Image,
        SearchCategory.Media => Media,
        SearchCategory.Archive => Archive,
        SearchCategory.Folder => Folder,
        SearchCategory.Other => Other,
        _ => All
    };
}
