using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
// 翻译结果卡片：贴近选区展示原文与译文，可一键复制，随选区/DPI 定位。
sealed class TranslateCard : Window
{
    TranslateCard(string original,string translated)
    {
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 翻译";
        SizeToContent=SizeToContent.Height;MaxWidth=460;Width=420;WindowStartupLocation=WindowStartupLocation.Manual;
        var panel=new StackPanel();
        panel.Children.Add(new TextBlock{Text="翻译结果",FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text});
        var originalText=new TextBlock{Text=original,FontSize=12.5,Foreground=Theme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0),MaxHeight=110};
        panel.Children.Add(originalText);
        panel.Children.Add(new TextBlock{Text=translated,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0),MaxHeight=160});
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};
        Button small(string label,Action action)=>new(){Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=28,Padding=new Thickness(12,2,12,2),FontSize=12,Foreground=Theme.Text,Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(6,0,0,0),Background=GlassWindow.Surface};
        actions.Children.Add(small("复制原文",()=>{try{System.Windows.Clipboard.SetText(original);}catch(System.Runtime.InteropServices.COMException){}}));
        actions.Children.Add(small("复制译文",()=>{try{System.Windows.Clipboard.SetText(translated);}catch(System.Runtime.InteropServices.COMException){}}));
        actions.Children.Add(small("关闭",Close));
        panel.Children.Add(actions);
        Content=new Border{CornerRadius=new CornerRadius(16),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(18,14,18,14),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=4,Opacity=.3}};
        KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
        Deactivated+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{try{if(!IsMouseOver)Close();}catch{}}));
    }
    internal static void Show(string original,string translated,Window owner,Rect selection,double dpiX,double dpiY)
    {
        var card=new TranslateCard(original,translated){Owner=owner};
        card.Loaded+=(_,_)=>{
            var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(card);
            var work=SystemParameters.WorkArea;
            double x=selection.Right*dpiX/dpi.DpiScaleX,y=(selection.Bottom*dpiY)/dpi.DpiScaleY+14;
            card.Left=Math.Clamp(x,work.Left+8,Math.Max(work.Left+8,work.Right-card.ActualWidth-8));
            card.Top=Math.Clamp(y,work.Top+8,Math.Max(work.Top+8,work.Bottom-card.ActualHeight-8));
        };
        card.Show();
    }
}
