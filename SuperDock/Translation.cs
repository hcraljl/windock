using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
namespace SuperDock;
// 截图翻译：参考 Umi-OCR 的本地 HTTP 接口（默认 http://127.0.0.1:1224），
// OCR 优先走本机 Umi-OCR（离线可用），未运行时回退系统自带 OCR；翻译优先走 Umi-OCR 翻译接口，回退在线翻译。
internal static class Translation
{
    static readonly System.Net.Http.HttpClient Http = new(){Timeout=TimeSpan.FromSeconds(12)};
    // 粗略判断是否以中文为主：CJK 字符占比。
    static bool MostlyChinese(string text)
    {
        int cjk=text.Count(ch=>ch>='一'&&ch<='龥'),total=text.Count(char.IsLetter);
        return total>0&&cjk*100/Math.Max(1,total)>=30;
    }
    public static async Task<(string Text,bool Ok)> RecognizeAsync(MainWindow.Settings settings,BitmapSource image)
    {
        // 1) 本机 Umi-OCR（接口不可用会立即连接失败，不阻塞）
        var (umiText,umiOk)=await UmiOcrAsync(settings.OcrApiUrl,image);
        if(umiOk&&!string.IsNullOrWhiteSpace(umiText))return(umiText.Trim(),true);
        // 2) 系统 OCR（zh-Hans-CN 语言包）
        try
        {
            var software=await SoftwareBitmapFromImageAsync(image);
            var engine=Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-Hans-CN"))??Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if(engine==null)return("",false);
            var result=await engine.RecognizeAsync(software);
            return(result.Text,true);
        }
        catch(Exception ex)when(ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException){return("",false);}
    }
    // 中译英 / 英译中：按识别文本语言自动选目标语言。
    public static async Task<(string Translated,string Detected,bool Ok,string Error)> TranslateAsync(MainWindow.Settings settings,string text)
    {
        string target=MostlyChinese(text)?"en":"zh";
        // 1) 本机 Umi-OCR 翻译接口
        var (umiText,umiOk)=await UmiTranslateAsync(settings.OcrApiUrl,text,target);
        if(umiOk&&!string.IsNullOrWhiteSpace(umiText))return(umiText,"",true,"");
        // 2) 在线翻译（两次尝试）
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
            catch(Exception ex)when(ex is HttpRequestException or System.Net.Sockets.SocketException or JsonException or TaskCanceledException)
            {
                if(attempt==1)return("","",false,"网络翻译失败，请检查网络后重试");
                await Task.Delay(600);
            }
        }
        return("","",false,"网络翻译失败，请检查网络后重试");
    }
    // Umi-OCR /api/ocr：POST {"base64": "..."} → {"code":100,"data":[{"text":"..."}]}
    static async Task<(string,bool)> UmiOcrAsync(string baseUrl,BitmapSource image)
    {
        try
        {
            var base64=ToBase64Png(image);
            using var content=new StringContent(JsonSerializer.Serialize(new Dictionary<string,object>{{"base64",base64}}),Encoding.UTF8,"application/json");
            using var response=await PostAsyncUmi(baseUrl+"/api/ocr",content);
            if(response==null)return("",false);
            var body=await response.Content.ReadAsStringAsync();
            var doc=JsonDocument.Parse(body);
            if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("code",out var code)&&code.ValueKind==JsonValueKind.Number&&code.GetInt32()==100&&doc.RootElement.TryGetProperty("data",out var data))
            {
                var sb=new StringBuilder();
                if(data.ValueKind==JsonValueKind.Array)
                {
                    foreach(var item in data.EnumerateArray())
                    {
                        if(item.ValueKind==JsonValueKind.Object&&item.TryGetProperty("text",out var t))sb.AppendLine(t.GetString());
                        else if(item.ValueKind==JsonValueKind.String)sb.AppendLine(item.GetString());
                    }
                }
                else if(data.ValueKind==JsonValueKind.String)sb.Append(data.GetString());
                return(sb.ToString(),true);
            }
            return("",false);
        }
        catch(Exception ex)when(ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException or JsonException or UriFormatException or InvalidOperationException){return("",false);}
    }
    // Umi-OCR /api/translate：POST {"text": "...", "from":"auto","to":"zh"} → {"code":100,"data":"..."}
    static async Task<(string,bool)> UmiTranslateAsync(string baseUrl,string text,string target)
    {
        try
        {
            var payload=JsonSerializer.Serialize(new Dictionary<string,object>{{"text",text},{"from","auto"},{"to",target}});
            using var content=new StringContent(payload,Encoding.UTF8,"application/json");
            using var response=await PostAsyncUmi(baseUrl+"/api/translate",content);
            if(response==null)return("",false);
            var body=await response.Content.ReadAsStringAsync();
            var doc=JsonDocument.Parse(body);
            if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("code",out var code)&&code.ValueKind==JsonValueKind.Number&&code.GetInt32()==100&&doc.RootElement.TryGetProperty("data",out var data))
            {
                if(data.ValueKind==JsonValueKind.String)return(data.GetString()??"",true);
                if(data.ValueKind==JsonValueKind.Array)
                {
                    var sb=new StringBuilder();
                    foreach(var item in data.EnumerateArray())
                    {
                        if(item.ValueKind==JsonValueKind.Object&&item.TryGetProperty("text",out var t))sb.Append(t.GetString());
                        else if(item.ValueKind==JsonValueKind.String)sb.Append(item.GetString());
                    }
                    return(sb.ToString(),true);
                }
            }
            return("",false);
        }
        catch(Exception ex)when(ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException or JsonException or UriFormatException or InvalidOperationException){return("",false);}
    }
    // Umi-OCR 接口只在本机：短超时 + 仅 localhost，避免挂住。
    static async Task<System.Net.Http.HttpResponseMessage?> PostAsyncUmi(string url,StringContent content)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.HostNameType!=UriHostNameType.Dns&&uri.Host!="localhost"&&!uri.Host.StartsWith("127."))return null;
        using var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            using var shortHttp=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(8)};
            return await shortHttp.PostAsync(uri,content,timeout.Token);
        }
        catch(Exception ex)when(ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException or InvalidOperationException){return null;}
    }
    static string ToBase64Png(BitmapSource image)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream=new MemoryStream();encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }
    // BitmapSource → SoftwareBitmap：经内存 PNG 流中转，规避跨 API 位图拷贝的签名差异。
    static async System.Threading.Tasks.Task<Windows.Graphics.Imaging.SoftwareBitmap> SoftwareBitmapFromImageAsync(BitmapSource image)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream=new MemoryStream();
        encoder.Save(stream);
        stream.Position=0;
        var imageStream=new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await imageStream.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(stream.ToArray()));
        imageStream.Seek(0);
        var decoder=await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(imageStream);
        return await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
    }
}
