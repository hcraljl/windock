using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
// 提取文字结果窗口：OCR 文本展示，可全选复制，贴近选区定位。
sealed class ExtractTextWindow : Window
{
    ExtractTextWindow(string text)
    {
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 提取文字";
        SizeToContent=SizeToContent.Height;Width=400;WindowStartupLocation=WindowStartupLocation.Manual;
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock{Text="提取文字",FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text});
        var box=new TextBox{Text=text,FontSize=13,Foreground=Theme.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,10,0,0),Background=new SolidColorBrush(Color.FromArgb(14,36,54,77)),BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(8,6,8,6)};
        panel.Children.Add(box);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        Button small(string label,Action action)=>new(){Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=28,Padding=new Thickness(12,2,12,2),FontSize=12,Foreground=Theme.Text,Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(6,0,0,0),Background=GlassWindow.Surface};
        actions.Children.Add(small("复制",()=>{try{System.Windows.Clipboard.SetText(text);}catch(System.Runtime.InteropServices.COMException){}}));
        actions.Children.Add(small("关闭",Close));
        panel.Children.Add(actions);
        Content=new Border{CornerRadius=new CornerRadius(16),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(18,14,18,14),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=4,Opacity=.3}};
        KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
        Deactivated+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{try{if(!IsMouseOver)Close();}catch{}}));
        Loaded+=(_,_)=>{box.Focus();box.SelectAll();};
    }
    internal static void Show(string text,Window owner,Rect selection)
    {
        var window=new ExtractTextWindow(text){Owner=owner};
        window.Loaded+=(_,_)=>{
            var work=SystemParameters.WorkArea;
            double x=selection.Left,y=selection.Bottom+14;
            window.Left=Math.Clamp(x,work.Left+8,Math.Max(work.Left+8,work.Right-window.ActualWidth-8));
            window.Top=Math.Clamp(y,work.Top+8,Math.Max(work.Top+8,work.Bottom-window.ActualHeight-8));
        };
        window.Show();
    }
}
