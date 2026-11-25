using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SuperDock.Search;

public enum FilePreviewKind
{
    Placeholder,
    Text,
    Image,
    Info
}

/// <summary>一次预览的结果；ImageData 是原始图片字节，由 UI 线程转成 BitmapImage。</summary>
public sealed record FilePreviewResult(
    FilePreviewKind Kind,
    string Meta,
    string Text,
    byte[]? ImageData,
    string Hint);

/// <summary>
/// 不依赖 Office / 第三方库的轻量文件预览：
/// 图片直接解码，纯文本按编码读取，docx/pptx/xlsx 解包提取 OOXML 文本，
/// PDF 用系统 WinRT PdfDocument 渲染首页，其它类型退回元信息。
/// </summary>
public sealed class FilePreviewService
{
    private const int MaxTextBytes = 256 * 1024;
    private const int MaxTextChars = 12000;
    private const long MaxImageBytes = 24L * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tif", ".tiff"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".tsv", ".json", ".xml", ".log", ".ini", ".config",
        ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".rs", ".go", ".vb",
        ".html", ".htm", ".css", ".scss", ".yml", ".yaml", ".toml", ".svg", ".xaml",
        ".bat", ".cmd", ".ps1", ".sql", ".sh", ".rtf"
    };

    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".pptx", ".xlsx"
    };

    public async Task<FilePreviewResult> BuildAsync(string path, CancellationToken cancellationToken)
    {
        var meta = Describe(path);
        if (Directory.Exists(path))
        {
            return new(FilePreviewKind.Info, meta, string.Empty, null, "文件夹 · 按 Enter 打开");
        }

        if (!File.Exists(path))
        {
            return new(FilePreviewKind.Info, meta, string.Empty, null, "文件已不存在或无法访问");
        }

        var extension = Path.GetExtension(path);
        try
        {
            if (ImageExtensions.Contains(extension))
            {
                var size = new FileInfo(path).Length;
                if (size > MaxImageBytes)
                {
                    return new(FilePreviewKind.Info, meta, string.Empty, null, "图片超过 24 MB，未加载预览");
                }

                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                return new(FilePreviewKind.Image, meta, string.Empty, bytes, string.Empty);
            }

            if (TextExtensions.Contains(extension))
            {
                var text = await ReadTextAsync(path, cancellationToken);
                return new(FilePreviewKind.Text, meta, text, null,
                    string.IsNullOrEmpty(text) ? "文件内容为空" : string.Empty);
            }

            if (OfficeExtensions.Contains(extension))
            {
                var text = await Task.Run(() => ExtractOfficeText(path, extension), cancellationToken);
                return new(FilePreviewKind.Text, meta, text, null,
                    string.IsNullOrEmpty(text) ? "未能从文档中提取到文字" : "文档纯文本预览（不含排版）");
            }

            if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var page = await RenderPdfFirstPageAsync(path);
                return page is { Length: > 0 }
                    ? new(FilePreviewKind.Image, meta, string.Empty, page, "PDF 第 1 页预览")
                    : new(FilePreviewKind.Info, meta, string.Empty, null, "无法渲染 PDF 首页，可按 Enter 打开");
            }

            if (extension is ".doc" or ".ppt" or ".xls")
            {
                var text = await ExtractLegacyOfficeText(path).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); return new(FilePreviewKind.Text, meta, Limit(text), null, string.IsNullOrWhiteSpace(text) ? "文档中没有可提取的文字" : "文档纯文本预览（不含排版）");
            }

            return new(FilePreviewKind.Info, meta, string.Empty, null, "暂不支持预览此类型，可按 Enter 打开");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(FilePreviewKind.Info, meta, string.Empty, null, $"预览失败：{exception.Message}");
        }
    }

    private static string Describe(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path);
                return $"文件夹 · {dir.LastWriteTime:yyyy-MM-dd HH:mm}";
            }

            var info = new FileInfo(path);
            return $"{FormatSize(info.Length)} · {info.LastWriteTime:yyyy-MM-dd HH:mm}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (bytes.Length > MaxTextBytes)
        {
            Array.Resize(ref bytes, MaxTextBytes);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Limit(Encoding.Unicode.GetString(bytes));
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Limit(new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF'));
        }

        try
        {
            return Limit(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Limit(Encoding.GetEncoding(936).GetString(bytes));
        }
    }

    private static string ExtractOfficeText(string path, string extension)
    {
        using var archive = ZipFile.OpenRead(path);
        IEnumerable<ZipArchiveEntry> entries = extension.ToLowerInvariant() switch
        {
            ".docx" => archive.Entries.Where(entry =>
                entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)),
            ".pptx" => archive.Entries.Where(entry =>
                    entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase),
            _ => archive.Entries.Where(entry =>
                entry.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase))
        };

        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) return ExtractSpreadsheetText(archive);
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            XDocument document;
            try
            {
                document = XDocument.Parse(reader.ReadToEnd());
            }
            catch
            {
                continue;
            }

            var groups = document.Descendants()
                .Where(element => element.Name.LocalName is "p" or "si")
                .ToList();

            if (groups.Count > 0)
            {
                foreach (var group in groups)
                {
                    var line = string.Concat(group.Descendants()
                        .Where(element => element.Name.LocalName == "t")
                        .Select(element => element.Value));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        text.AppendLine(line.Trim());
                    }
                }
            }
            else
            {
                foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "t"))
                {
                    text.AppendLine(element.Value);
                }
            }

            if (text.Length > MaxTextChars)
            {
                break;
            }
        }

        return Limit(text.ToString());
    }

    private static string ExtractSpreadsheetText(ZipArchive archive)
    {
        XDocument? Read(string name) { var entry=archive.GetEntry(name);if(entry==null)return null;using var stream=entry.Open();return XDocument.Load(stream); }
        var shared=Read("xl/sharedStrings.xml")?.Descendants().Where(e=>e.Name.LocalName=="si").Select(e=>string.Concat(e.Descendants().Where(t=>t.Name.LocalName=="t").Select(t=>t.Value))).ToArray()??[];
        var names=Read("xl/workbook.xml")?.Descendants().Where(e=>e.Name.LocalName=="sheet").Select(e=>(string?)e.Attribute("name")??"工作表").ToArray()??[];
        var output=new StringBuilder();int index=0;
        foreach(var entry in archive.Entries.Where(e=>e.FullName.StartsWith("xl/worksheets/sheet",StringComparison.OrdinalIgnoreCase)&&e.FullName.EndsWith(".xml")).OrderBy(e=>e.FullName.Length).ThenBy(e=>e.FullName,StringComparer.OrdinalIgnoreCase)){
            output.AppendLine(index<names.Length?names[index]:"工作表 "+(index+1));index++;using var stream=entry.Open();var document=XDocument.Load(stream);
            foreach(var row in document.Descendants().Where(e=>e.Name.LocalName=="row")){
                foreach(var cell in row.Elements().Where(e=>e.Name.LocalName=="c")){
                    var value=cell.Elements().FirstOrDefault(e=>e.Name.LocalName=="v")?.Value??string.Concat(cell.Descendants().Where(e=>e.Name.LocalName=="t").Select(e=>e.Value));
                    if((string?)cell.Attribute("t")=="s"&&int.TryParse(value,out int n)&&n>=0&&n<shared.Length)value=shared[n];
                    output.Append(value).Append('\t');
                }output.AppendLine();if(output.Length>MaxTextChars)return Limit(output.ToString());
            }output.AppendLine();
        }return Limit(output.ToString());
    }
    private static Task<string> ExtractLegacyOfficeText(string path)
    {
        var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{dynamic? app=null,document=null;try{
            var ext=Path.GetExtension(path).ToLowerInvariant();var id=ext==".xls"?"Excel.Application":ext==".ppt"?"PowerPoint.Application":"Word.Application";
            var type=Type.GetTypeFromProgID(id)??throw new InvalidOperationException("此格式需要安装 Microsoft Office");app=Activator.CreateInstance(type)!;try{app.AutomationSecurity=3;}catch{}
            var output=new StringBuilder();
            if(ext==".xls"){app.Visible=false;app.DisplayAlerts=false;app.AskToUpdateLinks=false;document=app.Workbooks.Open(path,0,true);foreach(dynamic sheet in document.Worksheets){output.AppendLine((string)sheet.Name);object values=sheet.UsedRange.Value2;if(values is Array cells){for(int r=cells.GetLowerBound(0);r<=cells.GetUpperBound(0)&&output.Length<MaxTextChars;r++){for(int c=cells.GetLowerBound(1);c<=cells.GetUpperBound(1);c++)output.Append(cells.GetValue(r,c)).Append('\t');output.AppendLine();}}else output.AppendLine(Convert.ToString(values));if(output.Length>MaxTextChars)break;}}
            else if(ext==".ppt"){document=app.Presentations.Open(path,-1,0,0);foreach(dynamic slide in document.Slides){output.AppendLine("第 "+slide.SlideIndex+" 页");foreach(dynamic shape in slide.Shapes){if(shape.HasTextFrame!=0&&shape.TextFrame.HasText!=0)output.AppendLine((string)shape.TextFrame.TextRange.Text);}if(output.Length>MaxTextChars)break;}}
            else{app.Visible=false;app.DisplayAlerts=0;document=app.Documents.Open(path,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false);output.Append((string)document.Content.Text);}
            completion.TrySetResult(output.ToString());
        }catch(Exception ex){completion.TrySetException(ex);}finally{try{if(document!=null)document.Close(false);}catch{}try{if(app!=null)app.Quit();}catch{}}});thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
    private static async Task<byte[]?> RenderPdfFirstPageAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var document = await PdfDocument.LoadFromFileAsync(file);
        if (document.PageCount == 0)
        {
            return null;
        }

        var page = document.GetPage(0);
        var stream = new InMemoryRandomAccessStream();
        var options = new PdfPageRenderOptions { DestinationWidth = 520 };
        await page.RenderToStreamAsync(stream, options);

        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[(int)stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static string Limit(string text)
    {
        return text.Length <= MaxTextChars
            ? text
            : text[..MaxTextChars] + "\n\n… 仅显示前 12000 个字符";
    }

    private static string FormatSize(long size)
    {
        if (size <= 0)
        {
            return "0 B";
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
