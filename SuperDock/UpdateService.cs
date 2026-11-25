using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Diagnostics;
namespace SuperDock;
// 更新源 = GitHub Releases。用户在 GitHub 发布新 Release（tag vX.Y.Z + 附上
// windock-Setup-X.Y.Z-win-x64.exe），软件手动检查更新即可发现并下载安装。
// 只在设置页触发，启动与后台不做任何自动检查。
internal record UpdateFile(string Path,string Sha256,long Size);
internal record UpdateRelease(string Version,string Url,string Sha256,long Size,string Notes,UpdateFile[]? Files=null);
internal static class UpdateService
{
    public const string GitHubOwner="hcraljl";
    public const string GitHubRepo="windock";
    static readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=TimeSpan.FromMinutes(10)};
    static readonly SemaphoreSlim gate=new(1);
    static UpdateService()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("windock-update-check");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public static string Status {get;private set;}="尚未检查";
    static string ApiUrl=>$"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    static readonly Regex Sha256Pattern=new(@"sha256[:\s`]*([0-9a-fA-F]{64})`?\s*",RegexOptions.Compiled|RegexOptions.IgnoreCase);
    sealed record GitHubAsset(string Name,long Size,[property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string Url);
    sealed record GitHubRelease(string? TagName,string? Body,GitHubAsset[] Assets);
    static UpdateRelease? Parse(string json)
    {
        var release=JsonSerializer.Deserialize<GitHubRelease>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower})??throw new InvalidDataException("更新清单为空");
        var tag=(release.TagName??"").TrimStart('v','V');
        if(!System.Version.TryParse(tag,out var version))throw new InvalidDataException($"发布版本号无效：{release.TagName}");
        var display=version.ToString(3);
        var assets=release.Assets??[];
        var setup=assets.FirstOrDefault(asset=>asset.Name.Equals($"windock-Setup-{display}-win-x64.exe",StringComparison.OrdinalIgnoreCase))
                  ??assets.FirstOrDefault(asset=>asset.Name.EndsWith("-win-x64.exe",StringComparison.OrdinalIgnoreCase))
                  ??assets.FirstOrDefault(asset=>asset.Name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase));
        if(setup==null)throw new InvalidDataException("发布缺少安装包附件");
        if(setup.Size<=0||setup.Size>268435456)throw new InvalidDataException("安装包大小无效");
        var hash=Sha256Pattern.Match(release.Body??"");
        return new UpdateRelease(display,setup.Url,hash.Success?hash.Groups[1].Value.ToUpperInvariant():"",setup.Size,release.Body??"",null);
    }
    // 手动检查：返回 null 表示已是最新（或源上还没有发布）。
    // 主路 GitHub API，限流/失败时改走网页重定向（不受 API 限额约束）。
    public static async Task<UpdateRelease?> Check()
    {
        await gate.WaitAsync();try
        {
            Status="正在检查…";
            string apiNote="";UpdateRelease? apiResult;
            try{apiResult=await CheckViaApi();}
            catch(Exception ex){apiNote=ex.Message;apiResult=null;}
            if(apiResult!=null){Status="发现新版本 "+apiResult.Version;ActivityStore.Write("检查更新",message:Status);return apiResult;}
            var fallback=await CheckViaRedirect();
            if(fallback!=null){Status="发现新版本 "+fallback.Version;ActivityStore.Write("检查更新",message:Status);return fallback;}
            if(Status=="正在检查…")Status=apiNote.Length>0?"检查失败，请稍后重试":"已是最新版本";
            if(Status=="检查失败，请稍后重试")ActivityStore.Write("更新失败",message:apiNote);
            else ActivityStore.Write("检查更新",message:Status);
            return null;
        }
        catch(Exception ex){Status="检查失败，请稍后重试";ActivityStore.Write("更新失败",message:ex.Message,error:ex);throw;}
        finally{gate.Release();}
    }
    static async Task<UpdateRelease?> CheckViaApi()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var response=await client.GetAsync(ApiUrl,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound){Status="更新源上还没有发布版本";return null;}
        if((int)response.StatusCode==403||(int)response.StatusCode==429)throw new HttpRequestException("API 限流");
        response.EnsureSuccessStatusCode();
        var json=await response.Content.ReadAsStringAsync(timeout.Token);
        var release=Parse(json);
        var current=typeof(UpdateService).Assembly.GetName().Version??new Version(0,0,0);
        bool newer=System.Version.Parse(release.Version)>current;
        Status=newer?"":"已是最新版本";
        return newer?release:null;
    }
    // 轻量检查（仅网页重定向，无 API 限额）：返回新版本的展示号，无更新/失败返回 null。
    // 供运行中 Dock 的周期提示使用，不改动 Status、不写日志（由调用方决定）。
    public static async Task<string?> QuietCheck()
    {
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response=await client.GetAsync($"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest",HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            var final=response.RequestMessage?.RequestUri?.ToString()??"";
            const string marker="/releases/tag/";
            int at=final.IndexOf(marker,StringComparison.Ordinal);
            if(at<0)return null;
            var tag=Uri.UnescapeDataString(final[(at+marker.Length)..]).TrimEnd('/');
            if(!System.Version.TryParse(tag.TrimStart('v','V'),out var version))return null;
            var current=typeof(UpdateService).Assembly.GetName().Version??new Version(0,0,0);
            return version>current?version.ToString(3):null;
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException){return null;}
    }
    // 无 API 限额的兜底：releases/latest 的最终 URL 就是 /tag/<版本>。
    static async Task<UpdateRelease?> CheckViaRedirect()
    {
        try
        {
            // 网络抖动（TLS EOF 等）重试一次。
            for(int attempt=1;;attempt++)
            {
                try
                {
                    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var response=await client.GetAsync($"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest",HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                    var final=response.RequestMessage?.RequestUri?.ToString()??"";
                    const string marker="/releases/tag/";
                    int at=final.IndexOf(marker,StringComparison.Ordinal);
                    if(response.StatusCode==System.Net.HttpStatusCode.NotFound||at<0){if(Status=="正在检查…")Status="更新源上还没有发布版本";return null;}
                    response.EnsureSuccessStatusCode();
                    var tag=Uri.UnescapeDataString(final[(at+marker.Length)..]).TrimEnd('/');
                    if(!System.Version.TryParse(tag.TrimStart('v','V'),out var version))throw new InvalidDataException($"发布版本号无效：{tag}");
                    var display=version.ToString(3);
                    bool newer=version>typeof(UpdateService).Assembly.GetName().Version!;
                    if(!newer){Status="已是最新版本";return null;}
                    Status="发现新版本 "+display;
                    var url=$"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/download/{tag}/windock-Setup-{display}-win-x64.exe";
                    // 兑底路径拿真实大小：HEAD 附件直链读取 Content-Length（不走 API 限额）。
                    long size=0;
                    try
                    {
                        using var head=new HttpRequestMessage(HttpMethod.Head,url);
                        using var probe=await client.SendAsync(head,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                        size=probe.Content.Headers.ContentLength??0;
                    }
                    catch{/* 取不到大小时下载器自动容错 */}
                    return new UpdateRelease(display,url,"",size,"（网页核对模式：发布说明与校验和不可用，按标准命名安装包下载）",null);
                }
                catch(Exception ex)when(attempt<2&&ex is HttpRequestException or IOException or System.Net.Sockets.SocketException)
                {
                    await Task.Delay(900);
                }
            }
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or InvalidDataException or System.Net.Sockets.SocketException){Status=Status.StartsWith("发现新版本")?Status:"检查失败，请稍后重试";return null;}
    }
    // 下载：最多 3 次尝试，中断后断点续传；校验失败/大小不符时清缓存重下。
    // 未知大小（兜底路径 HEAD 取不到时）跳过大小校验，仅验 PE 头。
    public static async Task<string> Download(UpdateRelease release,IProgress<int> progress,CancellationToken cancel=default)
    {
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SuperDock","updates");Directory.CreateDirectory(folder);
        var temp=Path.Combine(folder,"windock-Setup-"+release.Version+".partial");
        var destination=Path.Combine(folder,"windock-Setup-"+release.Version+".exe");
        Exception? last=null;
        for(int attempt=1;;attempt++)
        {
            try{return await DownloadAttempt(release,temp,destination,progress,cancel);}
            catch(OperationCanceledException){throw;}
            catch(Exception ex)when(ex is HttpRequestException or IOException or System.Net.Sockets.SocketException or InvalidDataException or CryptographicException)
            {
                last=ex;
                // 缓存不可信（校验失败/服务端多发了数据）：清掉重下；不完整则保留断点。
                bool corrupted=ex is CryptographicException||(ex is InvalidDataException i&&i.Message=="安装包大小不符");
                if(corrupted&&File.Exists(temp))File.Delete(temp);
                ActivityStore.Write("更新下载",message:$"第 {attempt} 次下载中断（{ex.Message}），{(corrupted?"重新下载":"断点续传")}");
                if(attempt>=3)break;
                await Task.Delay(TimeSpan.FromSeconds(attempt),cancel);
            }
        }
        if(File.Exists(temp))File.Delete(temp);
        throw last!;
    }
    static async Task<string> DownloadAttempt(UpdateRelease release,string temp,string destination,IProgress<int> progress,CancellationToken cancel)
    {
        long existing=File.Exists(temp)?new FileInfo(temp).Length:0;
        if(release.Size>0&&existing>=release.Size)existing=0;
        bool resume=existing>0;
        // 非续传的尝试一律从干净文件开始，避免与残留临时文件冲突。
        if(!resume&&File.Exists(temp))File.Delete(temp);
        using var request=new HttpRequestMessage(HttpMethod.Get,release.Url);
        if(resume)request.Headers.Range=new System.Net.Http.Headers.RangeHeaderValue(existing,null);
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();
        if(resume&&response.StatusCode!=System.Net.HttpStatusCode.PartialContent){resume=false;existing=0;if(File.Exists(temp))File.Delete(temp);}
        long total=resume?existing:0;
        await using(var input=await response.Content.ReadAsStreamAsync(cancel))await using(var output=new FileStream(temp,resume?FileMode.Append:FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
        {
            var buffer=new byte[81920];int read;
            while((read=await input.ReadAsync(buffer,cancel))>0)
            {
                total+=read;
                if(release.Size>0&&total>release.Size)throw new InvalidDataException("安装包大小不符");
                await output.WriteAsync(buffer.AsMemory(0,read),cancel);
                progress.Report(release.Size>0?(int)(total*100/release.Size):-1);
            }
        }
        if(release.Size>0&&total!=release.Size)throw new InvalidDataException("下载不完整");
        cancel.ThrowIfCancellationRequested();
        // 完整性：发布说明带 sha256 时强制校验；否则校验 PE 头（HTTPS 保障传输完整性）。
        using(var data=File.OpenRead(temp))
        {
            if(release.Sha256.Length==64)
            {
                if(!Convert.ToHexString(SHA256.HashData(data)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new CryptographicException("安装包校验失败");
            }
            else
            {
                Span<byte> header=stackalloc byte[2];data.ReadExactly(header);
                if(header[0]!='M'||header[1]!='Z')throw new InvalidDataException("安装包格式无效");
            }
        }
        File.Move(temp,destination,true);
        ActivityStore.Write("更新下载",message:"校验通过 "+release.Version);
        return destination;
    }
    public static void Install(string path)
    {
        // /RESTARTAPPLICATIONS：安装器关闭并重启被关闭的 Dock，静默更新后无需手动打开。
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true,Arguments="/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",WorkingDirectory=Path.GetDirectoryName(path)!});
        System.Windows.Application.Current.Shutdown();
    }
}
