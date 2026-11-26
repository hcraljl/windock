using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
namespace SuperDock;
// 截图翻译：Windows 自带 OCR（中文引擎可识别中英文）+ 在线翻译（中↔英互译）。
internal static class Translation
{
    static readonly System.Net.Http.HttpClient Http = new(){Timeout=TimeSpan.FromSeconds(12)};
    // BitmapSource → SoftwareBitmap：经内存 PNG 流中转，规避跨 API 位图拷贝的签名差异。
    static async System.Threading.Tasks.Task<Windows.Graphics.Imaging.SoftwareBitmap> SoftwareBitmapFromImageAsync(System.Windows.Media.Imaging.BitmapSource image)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream=new System.IO.MemoryStream();
        encoder.Save(stream);
        stream.Position=0;
        var imageStream=new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await imageStream.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(stream.ToArray()));
        imageStream.Seek(0);
        var decoder=await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(imageStream);
        return await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
    }
    // 粗略判断是否以中文为主：CJK 字符占比。
    static bool MostlyChinese(string text)
    {
        int cjk=text.Count(ch=>ch>='一'&&ch<='龥'),total=text.Count(char.IsLetter);
        return total>0&&cjk*100/Math.Max(1,total)>=30;
    }
    public static async Task<(string Text,bool Ok)> RecognizeAsync(BitmapSource image)
    {
        try
        {
            var software=await SoftwareBitmapFromImageAsync(image);
            var engine=Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-Hans-CN"))??Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if(engine==null)return("",false);
            var result=await engine.RecognizeAsync(software);
            return(result.Text,true);
        }
        catch(Exception ex)when(ex is InvalidOperationException or COMException or ArgumentException){return("",false);}
    }
    // 中译英 / 英译中：按 OCR 文本语言自动选目标语言。
    public static async Task<(string Translated,string Detected,bool Ok,string Error)> TranslateAsync(string text)
    {
        string target=MostlyChinese(text)?"en":"zh-CN";
        for(int attempt=0;attempt<2;attempt++)
        {
            try
            {
                var url="https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&dt=t&tl="+target+"&q="+Uri.EscapeDataString(text);
                using var response=await Http.GetAsync(url);
                var body=await response.Content.ReadAsStringAsync();
                if(!response.IsSuccessStatusCode)throw new HttpRequestException((int)response.StatusCode+"");
                var parsed=JsonDocument.Parse(body);
                var segments=new StringBuilder();
                foreach(var line in parsed.RootElement[0].EnumerateArray())
                {
                    if(line.ValueKind!=JsonValueKind.Array||line.GetArrayLength()==0)continue;
                    segments.Append(line[0].GetString());
                }
                var detected=parsed.RootElement.ValueKind==JsonValueKind.Array&&parsed.RootElement.GetArrayLength()>2?parsed.RootElement[2].GetString()??"":"";
                var translated=segments.ToString();
                if(translated.Length==0)return(text,"",false,"翻译结果为空");
                return(translated,detected,true,"");
            }
            catch(Exception ex)when(ex is HttpRequestException or System.Net.Sockets.SocketException or JsonException or TaskCanceledException or System.Net.Http.HttpRequestException)
            {
                if(attempt==1)return("","",false,"网络翻译失败，请检查网络后重试");
                await Task.Delay(600);
            }
        }
        return("","",false,"网络翻译失败，请检查网络后重试");
    }
}
