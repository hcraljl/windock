using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace SuperDock;
internal sealed class WidgetView : Border
{
    readonly MainWindow.Settings settings;
    readonly bool performance,compact;
    readonly StackPanel body=new();
    readonly DispatcherTimer timer;
    readonly CancellationTokenSource cancel=new();
    PerformanceMonitor? monitor;
    bool active,busy;
    readonly TextBlock summary=new(){Foreground=WidgetPaint.Label,FontSize=12};
    readonly Dictionary<string,(TextBlock Text,ProgressBar? Bar)> values=new();
    public WidgetView(MainWindow.Settings settings,bool performance,bool compact=false)
    {
        this.settings=settings;this.performance=performance;this.compact=compact;
        CornerRadius=new CornerRadius(14);Background=WidgetPaint.Tint(.07);Padding=new Thickness(compact?8:20);Margin=new Thickness(compact?4:0,0,compact?4:0,compact?0:12);
        Child=body;
        if(compact){Width=performance?100:110;body.Children.Add(summary);Cursor=System.Windows.Input.Cursors.Hand;MouseLeftButtonUp+=(_,_)=>OpenDetails();}
        else
        {
            body.Children.Add(new TextBlock{Text=performance?"实时性能":"城市天气",FontSize=19,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,Margin=new Thickness(0,0,0,14)});
            foreach(var key in performance?new[]{"CPU","GPU","内存","网络"}:new[]{"当前天气","风速","更新时间"})
            {
                var text=new TextBlock{Text=key+"  —",Foreground=Theme.Muted,TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,8,0,6)};body.Children.Add(text);
                ProgressBar? bar=null;
                if(performance&&key!="网络"){bar=new ProgressBar{Height=4,Minimum=0,Maximum=100,Foreground=new SolidColorBrush(Color.FromRgb(111,207,255)),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255)),BorderThickness=new Thickness(0)};body.Children.Add(bar);}
                values[key]=(text,bar);
            }
            if(!performance){var link=new TextBlock{Text="天气数据：Open-Meteo · 模型估算",FontSize=10,Foreground=Brushes.LightSlateGray,Margin=new Thickness(0,14,0,0)};body.Children.Add(link);}
        }
        timer=new DispatcherTimer{Interval=performance?TimeSpan.FromSeconds(2):TimeSpan.FromMinutes(15)};
        timer.Tick+=async(_,_)=>await Update();
        Loaded+=async(_,_)=>{active=true;if(performance)monitor=new PerformanceMonitor();timer.Start();await Update();};
        Unloaded+=(_,_)=>{active=false;timer.Stop();cancel.Cancel();if(!busy){monitor?.Dispose();monitor=null;}};
    }
    static string Rate(double value)=>value>=1024*1024?$"{value/1024/1024:0.0} MB/s":$"{value/1024:0} KB/s";
    void Set(string key,string text,double? percent=null){if(!values.TryGetValue(key,out var value))return;value.Text.Text=key+"   "+text;if(value.Bar!=null){value.Bar.Value=percent??0;value.Bar.Opacity=percent.HasValue?1:.3;}}
    async Task Update()
    {
        if(!active||busy)return;busy=true;
        try
        {
            if(performance)
            {
                var sample=await Task.Run(()=>monitor!.Read());if(!active)return;
                if(compact)summary.Text=$"CPU {sample.Cpu?.ToString("0")??"—"}%\n内存 {sample.Memory:0}%";
                else{Set("CPU",sample.Cpu.HasValue?$"{sample.Cpu:0.0}%":"采样中…",sample.Cpu);Set("GPU",sample.Gpu.HasValue?$"{sample.Gpu:0.0}% · 最繁忙引擎":"不可用 / 驱动未提供计数器",sample.Gpu);Set("内存",$"{sample.Memory:0}%",sample.Memory);Set("网络",$"↑ {Rate(sample.Upload)}    ↓ {Rate(sample.Download)}");}
            }
            else
            {
                if(string.IsNullOrWhiteSpace(settings.WeatherCity)){summary.Text="天气\n请先设置城市";Set("当前天气","请在组件设置中选择城市");return;}
                summary.Text="天气更新中…";
                var weather=await WeatherService.Read(settings.WeatherLatitude,settings.WeatherLongitude,cancel.Token);if(!active)return;
                summary.Text=$"{weather.Temperature:0}°  {weather.Description}\n{settings.WeatherCity.Split('·')[0].Trim()}";
                Set("当前天气",$"{settings.WeatherCity}\n{weather.Temperature:0.0}°C · {weather.Description}");Set("风速",$"{weather.Wind:0.0} km/h");Set("更新时间",weather.Time.Replace('T',' '));
            }
        }
        catch(Exception ex) when(ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Net.NetworkInformation.NetworkInformationException)
        {ActivityStore.Write("组件失败",performance?"性能":"天气",message:ex.Message,error:ex);if(active){summary.Text=performance?"性能暂不可用":"天气暂不可用";Set(performance?"CPU":"当前天气",performance?"采集失败，稍后重试":"联网失败，稍后重试");}}
        finally{busy=false;if(!active){monitor?.Dispose();monitor=null;}}
    }
    void OpenDetails()
    {
        var window=new Window{Title=performance?"windock · 性能":"windock · 天气",Width=430,MaxHeight=Math.Max(200,SystemParameters.WorkArea.Height-48),SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        window.Owner = Window.GetWindow(this);
        var stack=new DockPanel{LastChildFill=true};var close=new Button{Content="关闭",Style=(Style)FindResource("GlassButton"),HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,12)};close.Click+=(_,_)=>window.Close();DockPanel.SetDock(close,Dock.Top);stack.Children.Add(close);stack.Children.Add(new ScrollViewer{Content=new WidgetView(settings,performance),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(100,SystemParameters.WorkArea.Height-160)});
        window.Content=new Border{Background=(Brush)FindResource("GlassFill"),BorderBrush=(Brush)FindResource("GlassStroke"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(20),Child=stack};window.Show();
    }
}





