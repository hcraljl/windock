using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace SuperDock;
internal sealed class WidgetCarousel : Border
{
    readonly List<FrameworkElement> items=[];
    readonly Grid host=new();
    readonly DispatcherTimer timer=new();
    int index;
    public WidgetCarousel(MainWindow.Settings settings,double crossSize=44)
    {
        bool vertical=settings.Edge is "Left" or "Right";Width=vertical?crossSize:118;HorizontalAlignment=HorizontalAlignment.Center;VerticalAlignment=VerticalAlignment.Center;ClipToBounds=false;
        if(settings.PerformanceWidget)items.Add(new WidgetView(settings,true,true));
        if(settings.WeatherWidget)items.Add(new WidgetView(settings,false,true));
        if(settings.ClockWidget)items.Add(new InfoWidget(false));
        if(settings.StorageWidget)items.Add(new InfoWidget(true));
        if(items.Count==0){Visibility=Visibility.Collapsed;return;}
        foreach(var item in items){item.Visibility=Visibility.Collapsed;host.Children.Add(item);}items[0].Visibility=Visibility.Visible;Child=vertical?new Viewbox{Child=host,Width=crossSize,Stretch=System.Windows.Media.Stretch.Uniform}:host;ToolTip="组件轮播 · 滚轮切换";
        timer.Interval=TimeSpan.FromSeconds(Math.Clamp(settings.WidgetIntervalSeconds,1,3600));
        timer.Tick+=(_,_)=>{if(!IsMouseOver)Next(1);};
        Loaded+=(_,_)=>{if(items.Count>1)timer.Start();};Unloaded+=(_,_)=>timer.Stop();
        PreviewMouseWheel+=(_,e)=>{Next(e.Delta<0?1:-1);timer.Stop();if(items.Count>1)timer.Start();e.Handled=true;};
    }
    void Next(int direction)
    {
        if(items.Count<2)return;items[index].Visibility=Visibility.Collapsed;index=(index+direction+items.Count)%items.Count;items[index].Visibility=Visibility.Visible;
        BeginAnimation(OpacityProperty,new DoubleAnimation(.35,1,TimeSpan.FromMilliseconds(250)));
    }
}





