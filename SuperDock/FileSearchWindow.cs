using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperDock.Search;
namespace SuperDock;

internal static class FileSearchBackend
{
    static readonly SemaphoreSlim worker=new(1);
    public static string Compose(string text,string category)=>category switch{
        "应用"=>$"<{text}> ext:exe;lnk;appref-ms",
        "文档"=>$"<{text}> ext:doc;docx;pdf;txt;md;xls;xlsx;csv;ppt;pptx;wps;et;dps;odt;rtf",
        "图片"=>$"<{text}> ext:jpg;jpeg;png;gif;bmp;webp;svg;ico;heic;psd",
        "影音"=>$"<{text}> ext:mp4;mkv;avi;mov;mp3;wav;flac;aac;webm;m4a",
        "文件夹"=>$"folder: <{text}>",_=>text};
    public static async Task<EverythingSearchResult> Query(string text,string category,int maxResults=160,string sort="相关度",CancellationToken cancellationToken=default)
    {
        if(!await worker.WaitAsync(TimeSpan.FromSeconds(24),cancellationToken))return new(false,false,"索引正在响应，请稍后重新搜索。",[]);
        var task=Task.Run(async()=>{
            var ready=await EverythingRuntime.EnsureAsync();
            if(ready.Kind==EverythingReadyKind.Failed)return new EverythingSearchResult(false,true,ready.Message,[]);
            IReadOnlyList<EverythingHit> hits;string message;bool unavailable=false;
            if(EverythingRuntime.GetBundledEsExecutable()!=null){if(!new EverythingCliSearchService().TrySearch(Compose(text,category),maxResults,ready.NamedInstance,out hits,out message,sort,cancellationToken))return new EverythingSearchResult(false,false,message,[]);}
            else hits=new EverythingSearchService().Search(Compose(text,category),maxResults,out message,out unavailable);
            return new EverythingSearchResult(message.Length==0,unavailable,message,hits);
        });
        _=task.ContinueWith(t=>{_ =t.Exception;worker.Release();},TaskScheduler.Default);
        try{return await task.WaitAsync(TimeSpan.FromSeconds(24),cancellationToken);}
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch(TimeoutException){return new(false,true,"索引响应超时；首次建立索引可能较慢，请稍后重试。",[]);}
        catch(Exception ex){ActivityStore.Write("文件搜索失败",message:ex.Message,error:ex);return new(false,true,"无法连接文件索引，请重试。",[]);}
    }
}

public partial class MainWindow
{
    static SearchViews.SearchWindow? fileSearch;
    void ShowFileSearch()
    {
        if(fileSearch is {} existing){if(existing.IsClosing){Dispatcher.BeginInvoke(new Action(ShowFileSearch));return;}if(existing.SearchTheme==settings.ThemeMode){if(existing.WindowState==WindowState.Minimized)existing.WindowState=WindowState.Normal;existing.Activate();return;}existing.Close();}
        var window=new SearchViews.SearchWindow{SearchTheme=settings.ThemeMode,PinToDock=AddEntry,ShortcutText=settings.FileSearchHotkey=="Alt+Space"?"Alt + 空格":"Ctrl + Alt + F",DataContext=new SearchModels.SearchViewModel(),Owner=this,WindowStartupLocation=WindowStartupLocation.CenterScreen};fileSearch=window;openMenus++;hideTimer.Stop();window.Closed+=(_,_)=>{if(ReferenceEquals(fileSearch,window))fileSearch=null;openMenus--;ScheduleHide();};window.Show();window.Activate();
        var dpi=VisualTreeHelper.GetDpi(window);var area=PhysicalWorkArea;DesktopIntegration.MovePhysical(window,area.Left+(area.Width-window.ActualWidth*dpi.DpiScaleX)/2,area.Top+(area.Height-window.ActualHeight*dpi.DpiScaleY)/2);
    }
}
