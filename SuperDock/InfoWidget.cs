using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace SuperDock;
internal sealed class InfoWidget : Border
{
    readonly TextBlock label=new(){Foreground=Brushes.White,FontSize=12};
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(15)};
    readonly bool storage;
    public InfoWidget(bool storage)
    {
        this.storage=storage;timer.Interval=TimeSpan.FromSeconds(storage?15:1);Width=110;CornerRadius=new CornerRadius(12);Background=new SolidColorBrush(Color.FromArgb(18,255,255,255));Padding=new Thickness(8);Margin=new Thickness(4,0,4,0);Child=label;Cursor=System.Windows.Input.Cursors.Hand;
        ToolTip=storage?"系统磁盘 · 点击打开存储设置":"时钟与日历";
        timer.Tick+=(_,_)=>Update();Loaded+=(_,_)=>{Update();timer.Start();};Unloaded+=(_,_)=>timer.Stop();MouseLeftButtonUp+=(_,_)=>Open();
    }
    void Update()
    {
        if(!storage){label.Text=DateTime.Now.ToString("HH:mm\nMM/dd ddd");return;}
        try{var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);label.Text=$"{drive.Name.TrimEnd('\\')} 剩余\n{drive.AvailableFreeSpace/1073741824d:0.#} GB";}catch(IOException){label.Text="磁盘\n不可用";}
    }
    void Open()
    {
        if(storage){try{Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){GlassDialog.Show("无法打开存储设置。");}return;}
        var window=new Window{Title="日历",SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var body=new StackPanel();var close=new Button{Content="关闭",Style=(Style)Application.Current.FindResource("GlassButton"),HorizontalAlignment=HorizontalAlignment.Right};close.Click+=(_,_)=>window.Close();body.Children.Add(close);body.Children.Add(new Calendar{Background=Theme.Card,Foreground=Theme.Text,BorderBrush=Theme.Stroke,SelectedDate=DateTime.Today,DisplayDate=DateTime.Today,Margin=new Thickness(0,12,0,0)});window.Content=new Border{Background=Theme.Fill,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(18),Child=body};bool closing=false;window.Closing+=(_,_)=>closing=true;window.Deactivated+=(_,_)=>{if(!closing)window.Dispatcher.BeginInvoke(new Action(()=>{if(!closing&&!window.IsActive&&window.IsVisible)window.Close();}));};window.Show();
    }
}



