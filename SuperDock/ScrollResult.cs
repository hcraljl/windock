using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;
// 滚动截图结果窗口：长图滚动预览 + 复制/保存/贴图/关闭。
sealed class ScrollResult : Window
{
    readonly BitmapSource image;
    readonly MainWindow.Settings settings;
    ScrollResult(BitmapSource image,MainWindow.Settings settings)
    {
        this.image=image;this.settings=settings;
        Title="windock · 滚动截图";Width=560;Height=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;Background=GlassWindow.Surface;Foreground=Theme.Text;ResizeMode=ResizeMode.CanResize;
        var viewer=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brushes.Transparent};
        var preview=new Image{Source=image,Stretch=Stretch.None,HorizontalAlignment=HorizontalAlignment.Center};
        RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.HighQuality);
        viewer.Content=preview;
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        Button small(string label,Action action)
        {
            var button=new Button{Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=30,MinWidth=64,Padding=new Thickness(14,2,14,2),FontSize=12,Foreground=Theme.Text,Cursor=Cursors.Hand,Margin=new Thickness(8,0,0,0),Background=GlassWindow.Surface};
            button.Click+=(_,_)=>action();
            return button;
        }
        actions.Children.Add(small("复制",Copy));
        actions.Children.Add(small("保存",Save));
        actions.Children.Add(small("贴到屏幕",Pin));
        actions.Children.Add(small("关闭",Close));
        var layout=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(actions,Dock.Bottom);
        layout.Children.Add(actions);layout.Children.Add(viewer);
        Content=new Border{Padding=new Thickness(18,14,18,14),Child=layout};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
    }
    void Copy(){try{System.Windows.Clipboard.SetImage(image);}catch(System.Runtime.InteropServices.COMException){}}
    void Pin()=>PinnedImage.Pin(image,settings);
    void Save()
    {
        var thread=new System.Threading.Thread(()=>
        {
            try
            {
                var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="windock-scroll-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"};
                var pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if(!string.IsNullOrEmpty(pictures)&&Directory.Exists(pictures))dialog.InitialDirectory=pictures;
                if(dialog.ShowDialog()==true)
                {
                    using var stream=File.Create(dialog.FileName);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
                    ActivityStore.Write("滚动截图",message:"已保存 "+dialog.FileName);
                }
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Dispatcher.BeginInvoke(new Action(()=>GlassDialog.Show("保存失败："+ex.Message)));
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
    internal static void Show(BitmapSource image,MainWindow.Settings settings)
    {
        if(image.CanFreeze&&!image.IsFrozen)image.Freeze();
        new ScrollResult(image,settings).Show();
    }
}
